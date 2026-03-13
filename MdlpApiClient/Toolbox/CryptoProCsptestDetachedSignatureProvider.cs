namespace MdlpApiClient.Toolbox
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;

    internal class CryptoProCsptestDetachedSignatureProvider : IDetachedSignatureProvider
    {
        private readonly string csptestPath;
        private readonly string containerPin;
        private readonly int timeoutMs;

        public CryptoProCsptestDetachedSignatureProvider(string csptestPath, string containerPin, int timeoutMs)
        {
            this.csptestPath = csptestPath ?? string.Empty;
            this.containerPin = containerPin ?? string.Empty;
            this.timeoutMs = timeoutMs > 0 ? timeoutMs : 30000;
        }

        public string ProviderName => "CryptoProCsptest";

        public bool TryComputeDetachedSignature(
            X509Certificate2 certificate,
            string textToSign,
            out string signature,
            out Exception error)
        {
            signature = null;
            error = null;

            if (certificate == null)
            {
                error = new ArgumentNullException(nameof(certificate));
                return false;
            }

            if (textToSign == null)
            {
                error = new ArgumentNullException(nameof(textToSign));
                return false;
            }

            var resolvedCsptestPath = ResolveCsptestPath(csptestPath);
            if (string.IsNullOrWhiteSpace(resolvedCsptestPath))
            {
                error = new FileNotFoundException("CryptoPro csptest tool not found.");
                return false;
            }

            var thumbprint = NormalizeCertificateIdentifier(certificate.Thumbprint);
            if (string.IsNullOrWhiteSpace(thumbprint))
            {
                error = new InvalidOperationException("Certificate thumbprint is empty.");
                return false;
            }

            var fileSuffix = Guid.NewGuid().ToString("N");
            var inputFile = Path.Combine(Path.GetTempPath(), "mdlp-sign-" + fileSuffix + ".txt");
            var outputFile = Path.Combine(Path.GetTempPath(), "mdlp-sign-" + fileSuffix + ".p7s");

            try
            {
                File.WriteAllText(inputFile, textToSign, new UTF8Encoding(false));

                var args =
                    "-sfsign -sign -detached -add -base64 " +
                    "-in " + QuoteArg(inputFile) + " " +
                    "-out " + QuoteArg(outputFile) + " " +
                    "-my " + QuoteArg(thumbprint);

                if (!string.IsNullOrWhiteSpace(containerPin))
                {
                    args += " -password " + QuoteArg(containerPin);
                }

                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = resolvedCsptestPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(resolvedCsptestPath) ?? Environment.CurrentDirectory,
                }))
                {
                    if (process == null)
                    {
                        error = new InvalidOperationException("Failed to start csptest process.");
                        return false;
                    }

                    if (!process.WaitForExit(timeoutMs))
                    {
                        TryKillProcess(process);
                        error = new TimeoutException("csptest process timeout.");
                        return false;
                    }

                    if (process.ExitCode != 0)
                    {
                        error = new InvalidOperationException("csptest process exit code: " + process.ExitCode);
                        return false;
                    }
                }

                if (!File.Exists(outputFile))
                {
                    error = new FileNotFoundException("csptest did not create output signature file.", outputFile);
                    return false;
                }

                var rawBase64 = File.ReadAllText(outputFile);
                if (string.IsNullOrWhiteSpace(rawBase64))
                {
                    error = new InvalidOperationException("csptest produced empty output.");
                    return false;
                }

                var sb = new StringBuilder(rawBase64.Length);
                foreach (var ch in rawBase64)
                {
                    if (!char.IsWhiteSpace(ch))
                    {
                        sb.Append(ch);
                    }
                }

                signature = sb.ToString();
                if (string.IsNullOrWhiteSpace(signature))
                {
                    error = new InvalidOperationException("csptest output contains no base64 data.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
            finally
            {
                TryDeleteFile(inputFile);
                TryDeleteFile(outputFile);
            }
        }

        internal static string ResolveCsptestPath(string configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            {
                return configuredPath;
            }

            var fromEnv = Environment.GetEnvironmentVariable("MDLP_CRYPTOPRO_CSPTEST_PATH");
            if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
            {
                return fromEnv;
            }

            var fromPath = ResolveFromPathEnvironment();
            if (!string.IsNullOrWhiteSpace(fromPath))
            {
                return fromPath;
            }

            var knownLocation = ResolveFromKnownInstallLocations();
            if (!string.IsNullOrWhiteSpace(knownLocation))
            {
                return knownLocation;
            }

            return null;
        }

        private static string ResolveFromPathEnvironment()
        {
            var pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathValue))
            {
                return null;
            }

            foreach (var directory in pathValue.Split(Path.PathSeparator))
            {
                var normalizedDirectory = directory.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(normalizedDirectory) || !Directory.Exists(normalizedDirectory))
                {
                    continue;
                }

                foreach (var executableName in GetExecutableNames())
                {
                    var candidate = Path.Combine(normalizedDirectory, executableName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static string ResolveFromKnownInstallLocations()
        {
            foreach (var candidate in GetKnownInstallCandidates())
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetKnownInstallCandidates()
        {
            if (OperatingSystem.IsWindows())
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (!string.IsNullOrWhiteSpace(programFiles))
                {
                    yield return Path.Combine(programFiles, "Crypto Pro", "CSP", "csptest.exe");
                }

                var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                if (!string.IsNullOrWhiteSpace(programFilesX86))
                {
                    yield return Path.Combine(programFilesX86, "Crypto Pro", "CSP", "csptest.exe");
                }

                yield break;
            }

            var unixBases = new[]
            {
                "/opt/cprocsp/bin",
                "/opt/cprocsp/sbin/amd64",
                "/opt/cprocsp/sbin/aarch64"
            };

            var unixArchitectures = new[]
            {
                "amd64",
                "x86_64",
                "ia32",
                "aarch64",
                "arm64",
                "mac64"
            };

            var unixToolNames = new[] { "csptest", "csptestf" };

            foreach (var basePath in unixBases)
            {
                foreach (var toolName in unixToolNames)
                {
                    yield return Path.Combine(basePath, toolName);
                }

                foreach (var architecture in unixArchitectures)
                {
                    foreach (var toolName in unixToolNames)
                    {
                        yield return Path.Combine(basePath, architecture, toolName);
                    }
                }
            }
        }

        private static IEnumerable<string> GetExecutableNames()
        {
            if (OperatingSystem.IsWindows())
            {
                return new[] { "csptest.exe", "csptestf.exe", "csptest", "csptestf" };
            }

            return new[] { "csptest", "csptestf", "csptest.exe", "csptestf.exe" };
        }

        private static string NormalizeCertificateIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToUpperInvariant(c));
                }
            }

            return sb.ToString();
        }

        private static string QuoteArg(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static void TryDeleteFile(string filePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to delete temporary file '" + filePath + "': " + ex.Message);
            }
        }

        private static void TryKillProcess(Process process)
        {
            try
            {
                process.Kill();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to kill csptest process: " + ex.Message);
            }
        }
    }
}

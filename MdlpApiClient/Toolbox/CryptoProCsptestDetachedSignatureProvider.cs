namespace MdlpApiClient.Toolbox
{
    using System;
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

            var resolvedCsptestPath = ResolveCsptestPath();
            if (string.IsNullOrWhiteSpace(resolvedCsptestPath))
            {
                error = new FileNotFoundException("csptest.exe not found.");
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
                        error = new InvalidOperationException("Failed to start csptest.exe process.");
                        return false;
                    }

                    if (!process.WaitForExit(timeoutMs))
                    {
                        TryKillProcess(process);
                        error = new TimeoutException("csptest.exe timeout.");
                        return false;
                    }

                    if (process.ExitCode != 0)
                    {
                        error = new InvalidOperationException("csptest.exe exit code: " + process.ExitCode);
                        return false;
                    }
                }

                if (!File.Exists(outputFile))
                {
                    error = new FileNotFoundException("csptest.exe did not create output signature file.", outputFile);
                    return false;
                }

                var rawBase64 = File.ReadAllText(outputFile);
                if (string.IsNullOrWhiteSpace(rawBase64))
                {
                    error = new InvalidOperationException("csptest.exe produced empty output.");
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
                    error = new InvalidOperationException("csptest.exe output contains no base64 data.");
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

        private string ResolveCsptestPath()
        {
            if (!string.IsNullOrWhiteSpace(csptestPath) && File.Exists(csptestPath))
            {
                return csptestPath;
            }

            var fromEnv = Environment.GetEnvironmentVariable("MDLP_CRYPTOPRO_CSPTEST_PATH");
            if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
            {
                return fromEnv;
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                var candidate = Path.Combine(programFiles, "Crypto Pro", "CSP", "csptest.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                var candidate = Path.Combine(programFilesX86, "Crypto Pro", "CSP", "csptest.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
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

namespace MdlpApiClient.Probe;

using System.Diagnostics;
using System.Text;

internal sealed class MdlpSigner
{
    private readonly string _thumbprint;
    private readonly string _csptestPath;
    private readonly string _containerPin;
    private readonly TimeSpan _timeout;

    public MdlpSigner(
        string thumbprint,
        string csptestPath,
        string containerPin,
        TimeSpan? timeout = null)
    {
        this._thumbprint = NormalizeThumbprint(thumbprint);
        this._csptestPath = ResolveCsptestPath(csptestPath)
            ?? throw new FileNotFoundException(
                "csptest executable not found. Set --csptest-path or MDLP_CRYPTOPRO_CSPTEST_PATH.");
        this._containerPin = containerPin ?? string.Empty;
        this._timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public string CsptestPath => _csptestPath;

    public async Task<string> SignAuthCodeAsync(string authCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authCode))
        {
            throw new ArgumentException("Authentication code is empty.", nameof(authCode));
        }

        var fileSuffix = Guid.NewGuid().ToString("N");
        var inputFile = Path.Combine(Path.GetTempPath(), "mdlp-sign-" + fileSuffix + ".txt");
        var signatureFile = Path.Combine(Path.GetTempPath(), "mdlp-sign-" + fileSuffix + ".p7s");

        try
        {
            await File.WriteAllTextAsync(inputFile, authCode, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);

            var startInfo = new ProcessStartInfo
            {
                FileName = _csptestPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_csptestPath) ?? Environment.CurrentDirectory,
            };

            startInfo.ArgumentList.Add("-sfsign");
            startInfo.ArgumentList.Add("-sign");
            startInfo.ArgumentList.Add("-detached");
            startInfo.ArgumentList.Add("-add");
            startInfo.ArgumentList.Add("-base64");
            startInfo.ArgumentList.Add("-in");
            startInfo.ArgumentList.Add(inputFile);
            startInfo.ArgumentList.Add("-out");
            startInfo.ArgumentList.Add(signatureFile);
            startInfo.ArgumentList.Add("-my");
            startInfo.ArgumentList.Add(_thumbprint);
            if (!string.IsNullOrWhiteSpace(_containerPin))
            {
                startInfo.ArgumentList.Add("-password");
                startInfo.ArgumentList.Add(_containerPin);
            }

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start csptest process.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);
                throw new TimeoutException($"csptest timed out after {_timeout.TotalSeconds:0} seconds.");
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"csptest failed with exit code {process.ExitCode}. stderr: {stderr}; stdout: {stdout}");
            }

            if (!File.Exists(signatureFile))
            {
                throw new FileNotFoundException("csptest did not create detached signature file.", signatureFile);
            }

            var rawBase64 = await File.ReadAllTextAsync(signatureFile, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(rawBase64))
            {
                throw new InvalidOperationException("csptest produced empty output.");
            }

            var builder = new StringBuilder(rawBase64.Length);
            foreach (var ch in rawBase64)
            {
                if (!char.IsWhiteSpace(ch))
                {
                    builder.Append(ch);
                }
            }

            var signature = builder.ToString();
            if (string.IsNullOrWhiteSpace(signature))
            {
                throw new InvalidOperationException("csptest output contains no base64 data.");
            }

            return signature;
        }
        finally
        {
            TryDeleteFile(inputFile);
            TryDeleteFile(signatureFile);
        }
    }

    private static string? ResolveCsptestPath(string configuredPath)
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

        foreach (var candidate in GetKnownInstallCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ResolveFromPathEnvironment()
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
            "/opt/cprocsp/sbin/aarch64",
        };

        var unixArchitectures = new[]
        {
            "amd64",
            "x86_64",
            "ia32",
            "aarch64",
            "arm64",
            "mac64",
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

    private static string NormalizeThumbprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Thumbprint is empty.", nameof(value));
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
        }

        var normalized = builder.ToString();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Thumbprint does not contain alphanumeric characters.", nameof(value));
        }

        return normalized;
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}

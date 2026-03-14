namespace MdlpApiClient.Probe;

using MdlpApiClient;
using System.Globalization;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

internal static class Program
{
    private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
    private const string DefaultClientId1 = "22d12250-6cf3-4a87-b439-f698cfddc498";
    private const string DefaultClientSecret1 = "3deb0ba1-26f2-4516-b652-931fe832e3ff";
    private const string DefaultUserStarter1 = "starter_resident_1";
    private const string DefaultUserPassword1 = "password";
    private const string DefaultSandboxThumbprint1 = "10E4921908D24A0D1AD94A29BD0EF51696C6D8DA";

    private static async Task<int> Main(string[] args)
    {
        var options = ParseArgs(args);
        if (HasFlag(options, "help") || HasFlag(options, "h"))
        {
            PrintHelp();
            return 0;
        }

        DumpRuntime();
        DumpTransportEnvironment();

        var operation = GetOption(options, "operation", "doc-size").Trim().ToLowerInvariant();
        var authMode = GetOption(options, "auth", "resident").Trim().ToLowerInvariant();

        var baseUrl = GetOption(options, "base-url", EnvOrDefault("MDLP_TEST_API_BASE_URL", MdlpClient.SandboxApiHttps));
        var clientId = GetOption(options, "client-id", EnvOrDefault("MDLP_CLIENT_ID_1", DefaultClientId1));
        var clientSecret = GetOption(options, "client-secret", EnvOrDefault("MDLP_CLIENT_SECRET_1", DefaultClientSecret1));
        var userIdDefault = operation == "token"
            ? EnvOrDefault("MDLP_USER_ID_1", EnvOrDefault("MDLP_USER_STARTER_1", DefaultUserStarter1))
            : authMode == "resident"
                ? EnvOrDefault("MDLP_SANDBOX_USER_THUMBPRINT_1", DefaultSandboxThumbprint1)
                : EnvOrDefault("MDLP_USER_STARTER_1", DefaultUserStarter1);
        var userId = GetOption(options, "user-id", userIdDefault);
        var password = GetOption(options, "password", EnvOrDefault("MDLP_USER_PASSWORD_1", DefaultUserPassword1));
        var signThumbprint = GetOption(
            options,
            "sign-thumbprint",
            EnvOrDefault("MDLP_SIGN_THUMBPRINT", EnvOrDefault("MDLP_SANDBOX_USER_THUMBPRINT_1", DefaultSandboxThumbprint1)));
        var csptestPath = GetOption(
            options,
            "csptest-path",
            EnvOrDefault("MDLP_CRYPTOPRO_CSPTEST_PATH", string.Empty));
        var cryptoProPin = GetOption(
            options,
            "cryptopro-pin",
            EnvOrDefault("MDLP_CRYPTOPRO_PIN", string.Empty));

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(userId))
        {
            Console.WriteLine("ERROR: Missing required parameters. Use --help for usage.");
            return 2;
        }

        try
        {
            if (operation == "token")
            {
                return await RunSignedCodeTokenFlowAsync(
                    baseUrl,
                    clientId,
                    clientSecret,
                    userId,
                    signThumbprint,
                    csptestPath,
                    cryptoProPin);
            }

            if (authMode == "resident")
            {
                DumpMatchingCertificates(userId);
            }

            CredentialsBase credentials;
            if (authMode == "resident")
            {
                credentials = new ResidentCredentials
                {
                    ClientID = clientId,
                    ClientSecret = clientSecret,
                    UserID = userId,
                };
            }
            else if (authMode == "nonresident")
            {
                credentials = new NonResidentCredentials
                {
                    ClientID = clientId,
                    ClientSecret = clientSecret,
                    UserID = userId,
                    Password = password,
                };
            }
            else
            {
                Console.WriteLine("ERROR: Unsupported auth mode. Use resident or nonresident.");
                return 2;
            }

            using var client = new MdlpClient(credentials, baseUrl)
            {
                ApplicationName = "MdlpApiClient.Probe",
                Tracer = Trace,
            };

            if (operation == "doc-size")
            {
                var size = client.GetLargeDocumentSize();
                Console.WriteLine($"RESULT: doc-size={size}");
                Console.WriteLine($"RESULT: signature-size={client.SignatureSize}");
                return 0;
            }

            Console.WriteLine("ERROR: Unsupported operation. Use doc-size or token.");
            return 2;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: Operation failed");
            PrintExceptionTree(ex);
            return 1;
        }
    }

    private static async Task<int> RunSignedCodeTokenFlowAsync(
        string baseUrl,
        string clientId,
        string clientSecret,
        string userId,
        string signThumbprint,
        string csptestPath,
        string cryptoProPin)
    {
        if (string.IsNullOrWhiteSpace(signThumbprint))
        {
            Console.WriteLine("ERROR: Missing signing thumbprint. Set --sign-thumbprint or MDLP_SIGN_THUMBPRINT.");
            return 2;
        }

        Console.WriteLine("== Signed Code Auth ==");
        Console.WriteLine($"base-url={baseUrl}");
        Console.WriteLine($"user-id={userId}");
        Console.WriteLine($"sign-thumbprint={signThumbprint}");
        var signer = new MdlpSigner(signThumbprint, csptestPath, cryptoProPin);
        Console.WriteLine($"csptest-path={signer.CsptestPath}");

        using var http = new HttpClient(new HttpClientHandler(), disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(100),
        };

        var authPayload = new
        {
            client_id = clientId,
            client_secret = clientSecret,
            user_id = userId,
            auth_type = "SIGNED_CODE",
        };

        using var authResponse = await http.PostAsJsonAsync(BuildApiUrl(baseUrl, "auth"), authPayload);
        var authContent = await authResponse.Content.ReadAsStringAsync();
        if (!authResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("ERROR: /auth failed");
            Console.WriteLine($"status={(int)authResponse.StatusCode} {authResponse.ReasonPhrase}");
            Console.WriteLine($"body={authContent}");
            return 1;
        }

        if (!TryGetJsonProperty(authContent, "code", out var code))
        {
            Console.WriteLine("ERROR: /auth response does not contain 'code'.");
            Console.WriteLine($"body={authContent}");
            return 1;
        }

        Console.WriteLine($"AUTH: code-length={code.Length}");
        var signature = await signer.SignAuthCodeAsync(code);
        Console.WriteLine($"AUTH: signature-length={signature.Length}");

        var tokenPayload = new
        {
            code,
            signature,
        };

        using var tokenResponse = await http.PostAsJsonAsync(BuildApiUrl(baseUrl, "token"), tokenPayload);
        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
        if (!tokenResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("ERROR: /token failed");
            Console.WriteLine($"status={(int)tokenResponse.StatusCode} {tokenResponse.ReasonPhrase}");
            Console.WriteLine($"body={tokenContent}");
            return 1;
        }

        if (!TryGetJsonProperty(tokenContent, "token", out var token))
        {
            Console.WriteLine("ERROR: /token response does not contain 'token'.");
            Console.WriteLine($"body={tokenContent}");
            return 1;
        }

        Console.WriteLine($"TOKEN: {token}");
        return 0;
    }

    private static string BuildApiUrl(string baseUrl, string endpoint)
    {
        var normalizedBase = baseUrl.TrimEnd('/');
        if (normalizedBase.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedBase + "/" + endpoint;
        }

        return normalizedBase + "/api/v1/" + endpoint;
    }

    private static bool TryGetJsonProperty(string json, string propertyName, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(propertyName, out var element))
            {
                return false;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                value = element.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(value);
            }

            value = element.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    private static void Trace(string format, object[] args)
    {
        try
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, format, args));
        }
        catch
        {
            Console.WriteLine(format);
        }
    }

    private static void DumpRuntime()
    {
        Console.WriteLine("== Runtime ==");
        Console.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"ProcessArchitecture: {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"OSArchitecture: {RuntimeInformation.OSArchitecture}");
    }

    private static void DumpTransportEnvironment()
    {
        Console.WriteLine("== Transport Environment ==");
        PrintEnv("MDLP_USE_CRYPTOPRO_STDIO_PROXY");
        PrintEnv("MDLP_CRYPTOPRO_STDIO_DOTNET_PATH");
        PrintEnv("MDLP_CRYPTOPRO_STDIO_PROXY_PATH");
        PrintEnv("MDLP_ALLOW_INTERACTIVE_SIGNING");
        PrintEnv("MDLP_ALLOW_CRYPTOPRO_CLI_FALLBACK");
        PrintEnv("MDLP_CRYPTOPRO_CSPTEST_PATH");
        PrintEnv("MDLP_CRYPTOPRO_PIN", redact: true);
    }

    private static void DumpMatchingCertificates(string identifier)
    {
        Console.WriteLine("== Certificate Lookup ==");
        Console.WriteLine($"Lookup key: {identifier}");

        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            Console.WriteLine($"Store: {location}\\My");
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);

            var matches = store.Certificates
                .Where(c => c.HasPrivateKey)
                .Where(c => MatchesCertificate(c, identifier))
                .ToList();

            if (matches.Count == 0)
            {
                Console.WriteLine("  no matching certificate with private key");
                continue;
            }

            foreach (var cert in matches)
            {
                Console.WriteLine($"  thumbprint={cert.Thumbprint}");
                Console.WriteLine($"  subject={cert.Subject}");
                Console.WriteLine($"  notBefore={cert.NotBefore:s}");
                Console.WriteLine($"  notAfter={cert.NotAfter:s}");
                Console.WriteLine($"  hasPrivateKey={cert.HasPrivateKey}");
            }
        }
    }

    private static bool MatchesCertificate(X509Certificate2 certificate, string identifier)
    {
        var normalized = NormalizeIdentifier(identifier);
        var thumbprintMatches = string.Equals(
            NormalizeIdentifier(certificate.Thumbprint),
            normalized,
            StringComparison.OrdinalIgnoreCase);

        var serialMatches = string.Equals(
            NormalizeIdentifier(certificate.SerialNumber),
            normalized,
            StringComparison.OrdinalIgnoreCase);

        var subject = certificate.Subject;
        var subjectName = certificate.SubjectName.Name;
        var nameMatches = subject.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0 ||
            subjectName.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0;

        return thumbprintMatches || serialMatches || nameMatches;
    }

    private static string NormalizeIdentifier(string value)
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

    private static void PrintEnv(string name, bool redact = false)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            Console.WriteLine($"{name}=<empty>");
            return;
        }

        Console.WriteLine(redact ? $"{name}=<set>" : $"{name}={value}");
    }

    private static void PrintExceptionTree(Exception ex)
    {
        var depth = 0;
        var current = ex;
        while (current != null)
        {
            Console.WriteLine($"[{depth}] {current.GetType().FullName}");
            Console.WriteLine($"[{depth}] Message: {current.Message}");
            Console.WriteLine($"[{depth}] HResult: 0x{current.HResult:X8}");
            current = current.InnerException;
            depth++;
        }

        Console.WriteLine("== Full Exception ==");
        Console.WriteLine(ex.ToString());
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var result = new Dictionary<string, string>(KeyComparer);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var keyValue = arg.Substring(2);
            var eqIndex = keyValue.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = keyValue.Substring(0, eqIndex);
                var value = keyValue.Substring(eqIndex + 1);
                result[key] = value;
                continue;
            }

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                result[keyValue] = args[i + 1];
                i++;
            }
            else
            {
                result[keyValue] = "true";
            }
        }

        return result;
    }

    private static bool HasFlag(Dictionary<string, string> options, string key)
    {
        return options.ContainsKey(key);
    }

    private static string GetOption(Dictionary<string, string> options, string key, string fallback)
    {
        if (options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return fallback;
    }

    private static string EnvOrDefault(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("MdlpApiClient.Probe");
        Console.WriteLine("Usage:");
        Console.WriteLine("  --operation doc-size|token");
        Console.WriteLine("  --auth resident|nonresident");
        Console.WriteLine("  --base-url <url>");
        Console.WriteLine("  --client-id <id>");
        Console.WriteLine("  --client-secret <secret>");
        Console.WriteLine("  --user-id <thumbprint|subject|user>");
        Console.WriteLine("  --password <password> (for nonresident)");
        Console.WriteLine("  --sign-thumbprint <thumbprint> (for token)");
        Console.WriteLine("  --csptest-path <path-to-csptest.exe> (for token, optional)");
        Console.WriteLine("  --cryptopro-pin <container-pin> (for token, optional)");
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --project MdlpApiClient.Probe -- --auth resident --operation doc-size --user-id 1DF0...");
        Console.WriteLine("  dotnet run --project MdlpApiClient.Probe -- --auth nonresident --operation doc-size --user-id starter_resident_1 --password password");
        Console.WriteLine("  dotnet run --project MdlpApiClient.Probe -- --operation token --base-url https://sb.mdlp.crpt.ru/api/v1 --client-id <id> --client-secret <secret> --user-id <sandbox-user-id> --sign-thumbprint 1DF0... --csptest-path \"C:\\Program Files\\Crypto Pro\\CSP\\csptest.exe\"");
    }
}

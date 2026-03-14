namespace MdlpApiClient.Tests
{
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using NUnit.Framework;
    using MdlpApiClient.Toolbox;
    using System.Net.Http;
    using System.Linq;
    using System.Runtime.Serialization;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Runtime.InteropServices;

    [TestFixture]
    public class UnitTestsBase : IDisposable
    {
        private static string EnvOrDefault(string name, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(name);
            var resolved = string.IsNullOrWhiteSpace(value) ? fallback : value;
            return string.Equals(name, "MDLP_TEST_API_BASE_URL", StringComparison.OrdinalIgnoreCase)
                ? NormalizeLegacySandboxBaseUrl(resolved)
                : resolved;
        }

        private static string NormalizeLegacySandboxBaseUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return value.Replace("https://api.sb.mdlp.crpt.ru/api/v1/", MdlpClient.SandboxApiHttps, StringComparison.OrdinalIgnoreCase)
                .Replace("http://api.sb.mdlp.crpt.ru/api/v1/", MdlpClient.SandboxApiHttp, StringComparison.OrdinalIgnoreCase)
                .Replace("https://api.sb.mdlp.crpt.ru/api/v1", MdlpClient.SandboxApiHttps.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                .Replace("http://api.sb.mdlp.crpt.ru/api/v1", MdlpClient.SandboxApiHttp.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        private static bool EnvBoolOrDefault(string name, bool fallback)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "on":
                    return true;

                case "0":
                case "false":
                case "no":
                case "off":
                    return false;

                default:
                    return fallback;
            }
        }

        // MDLP test stage data
        public static readonly string SystemID1 = EnvOrDefault("MDLP_SYSTEM_ID_1", "57663471-2d09-4832-9b76-78095fbd0d43"); // идентификатор субъекта обращения
        public static readonly string ClientID1 = EnvOrDefault("MDLP_CLIENT_ID_1", "22d12250-6cf3-4a87-b439-f698cfddc498"); // идентификатор учетной системы
        public static readonly string ClientSecret1 = EnvOrDefault("MDLP_CLIENT_SECRET_1", "3deb0ba1-26f2-4516-b652-931fe832e3ff"); // секретный ключ учетной системы
        public static readonly string UserStarter1 = EnvOrDefault("MDLP_USER_STARTER_1", "starter_resident_1"); // имя тестового пользователя
        public static readonly string UserPassword1 = EnvOrDefault("MDLP_USER_PASSWORD_1", "password"); // пароль тестового пользователя

        public static readonly string SystemID2 = EnvOrDefault("MDLP_SYSTEM_ID_2", "86325e0c-9a23-4547-ad8a-219b4fc7fd03");
        public static readonly string ClientID2 = EnvOrDefault("MDLP_CLIENT_ID_2", "2cabd9b7-6042-40d8-97c2-8627f5704aa1");
        public static readonly string ClientSecret2 = EnvOrDefault("MDLP_CLIENT_SECRET_2", "1713da9a-2042-465c-80ba-4da4dca3323d");
        public static readonly string UserStarter2 = EnvOrDefault("MDLP_USER_STARTER_2", "starter_resident_2");
        public static readonly string UserPassword2 = EnvOrDefault("MDLP_USER_PASSWORD_2", "password");
        public static readonly string TestDocumentID = EnvOrDefault("MDLP_TEST_DOCUMENT_ID", "cdeeb2af-bebc-44d6-ad78-4ceb1709b314"); // "60786bb4-fcb5-4587-b703-d0147e3f9d1c";
        public static readonly string TestDocRequestID = EnvOrDefault("MDLP_TEST_DOC_REQUEST_ID", "97dad8f1-ef1d-4339-9938-18f129200e5d"); // "528700e0-f967-4ddb-995d-5c6c7b73bcc9";
        public static readonly string TestTicketID = EnvOrDefault("MDLP_TEST_TICKET_ID", "9d08e171-9ffc-4dce-b1da-d8e2472540ea"); // "e6afe4b3-4cb3-43af-b94e-83fcc358f4b7", TestDocumentID;

        // Custom test data, can be overridden by environment variables.
        public static readonly string TestApiBaseUrl = EnvOrDefault("MDLP_TEST_API_BASE_URL", MdlpClient.SandboxApiHttps);
        public static readonly string TestCertificateSubjectName = EnvOrDefault("MDLP_CERT_SUBJECT_NAME", @"Тестовый УКЭП им. Юрия Гагарина");
        public static readonly string TestCertificateThumbprint = EnvOrDefault("MDLP_CERT_THUMBPRINT", "0a22506a31c3c0c3c16939213e48cdd5d0c03d90");
        public static readonly string TestCertificateSerialNumber = EnvOrDefault("MDLP_CERT_SERIAL_NUMBER", string.Empty);
        public static readonly string CryptoProCsptestPath = EnvOrDefault("MDLP_CRYPTOPRO_CSPTEST_PATH", string.Empty);
        public static readonly string CryptoProContainerPin = EnvOrDefault("MDLP_CRYPTOPRO_PIN", string.Empty);
        public static readonly string TestUserThumbprint = EnvOrDefault("MDLP_USER_THUMBPRINT", TestCertificateThumbprint);
        public static readonly string SandboxUserThumbprint1 = EnvOrDefault("MDLP_SANDBOX_USER_THUMBPRINT_1", "10E4921908D24A0D1AD94A29BD0EF51696C6D8DA");
        public static readonly string SandboxUserThumbprint2 = EnvOrDefault("MDLP_SANDBOX_USER_THUMBPRINT_2", "CC5D2B6C6457DED657D7EB7C388585D03ADDCBC8");
        public static readonly string TestUserID = EnvOrDefault("MDLP_TEST_USER_ID", "7ae327e3f8b19c0a1101979b4a4b8772cf52219f"); // получен при регистрации
        public static readonly bool SkipSandboxTestsWhenUnavailable = EnvBoolOrDefault("MDLP_SKIP_SANDBOX_TESTS_WHEN_UNAVAILABLE", true);
        public static readonly bool EnableLegacyIntegrationTests = EnvBoolOrDefault("MDLP_ENABLE_LEGACY_INTEGRATION_TESTS", false);
        public static readonly string LegacyIntegrationTestsAllowRaw = EnvOrDefault("MDLP_ENABLE_LEGACY_INTEGRATION_TESTS_ALLOW", string.Empty);

        private static readonly HashSet<string> LegacyIntegrationFixtureNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AuthenticationTests",
            "ApiTestsChapter5",
            "ApiTestsChapter6",
            "ApiTestsChapter7",
            "ApiTestsChapter8",
            "ApiTestsDocuments",
            "ApiTestsMisc",
            "SandboxTests",
        };

        private static readonly HashSet<string> RestoredLegacyIntegrationTests = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Chapter 5: document operations
            "MdlpApiClient.Tests.ApiTestsChapter5.Chapter5_06_CancelSendDocument",
            "MdlpApiClient.Tests.ApiTestsChapter5.Chapter5_07_GetOutcomeDocuments",
            "MdlpApiClient.Tests.ApiTestsChapter5.Chapter5_08_GetIncomeDocuments",
            "MdlpApiClient.Tests.ApiTestsChapter5.Chapter5_13_GetSignature",
            // Note: Chapter5_05 (GET /documents/doc_size) and Chapter5_14 (skzkm-traces)
            // currently return 404 on the sandbox and are kept frozen.
            // Chapter 7: registries (EGRUL, EGRIP, RAFP, FIAS, ESKLP, licenses, addresses, etc.)
            "MdlpApiClient.Tests.ApiTestsChapter7.Chapter7_01_1_GetEgrulRegistryEntry",
            "MdlpApiClient.Tests.ApiTestsChapter7.Chapter7_02_1_GetEgripRegistryEntry",
            "MdlpApiClient.Tests.ApiTestsChapter7.Chapter7_03_1_GetRafpRegistryEntry",
            "MdlpApiClient.Tests.ApiTestsChapter7.Chapter7_05_3_GetFiasAddress",
            "MdlpApiClient.Tests.ApiTestsChapter7.Chapter7_06_1_GetProductionLicenses",
            "MdlpApiClient.Tests.ApiTestsChapter7.Chapter7_06_2_GetProductionLicenses",
            "MdlpApiClient.Tests.ApiTestsChapter7.Chapter7_07_1_GetPharmacyLicenses",
            "MdlpApiClient.Tests.ApiTestsChapter7.Chapter7_07_2_GetPharmacyLicenses",
            "MdlpApiClient.Tests.ApiTestsChapter7.Chapter7_08_1_GetCurrentAddresses",
            "MdlpApiClient.Tests.ApiTestsChapter7.Chapter7_09_1_GetCountries",
            "MdlpApiClient.Tests.ApiTestsChapter7.Chapter7_09_2_GetRegions",
            "MdlpApiClient.Tests.ApiTestsChapter7.Chapter7_10_1_GetEsklpInfo",
            "MdlpApiClient.Tests.ApiTestsChapter7.Chapter7_11_1_GetCustomsPoints",
            // Note: Chapter7_05_1 (GET /reestr/fias/addrobj) and Chapter7_05_2 (GET /reestr/fias/house)
            // are [Ignore]d because those endpoints were removed from the MDLP API.
            // Chapter 6: user management, account systems, rights
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_01_01_RegisterAccountSystem",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_01_02_RegisterUser",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_01_03_RegisterUser",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_01_04_GetUserInfo",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_01_05_GetCurrentLanguage",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_01_06_UpdateUserProfile",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_01_07_GetCurrentUserInfo",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_01_08_SetCurrentLanguage",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_01_09_GetCurrentCertificates",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_01_10_GetUserCertificates",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_01_11_GetAccountSystem",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_03_01_DeleteUser",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_03_02_DeleteAccountSystem",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_04_01_AddUserCertificate",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_04_02_DeleteUserCertificate",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_05_01_ChangeUserPassword",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_06_01_GetRights",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_06_02_GetCurrentRights",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_06_03456789_CreateUpdateDeleteRightsGroup",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_06_11_GetRightsGroups",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_07_02_GetUsers",
            "MdlpApiClient.Tests.ApiTestsChapter6.Chapter6_08_02_GetAccountSystems",
            // Chapter 8: registries and member info (read-only/stable subset)
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_01_2_GetBranches",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_01_3_GetBranch",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_01_4_RegisterBranch",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_02_2_GetWarehouses",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_02_3_GetWarehouse",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_02_4_RegisterWarehouse",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_02_5_GetAvailableAddresses",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_03_1_GetSgtins",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_03_2_GetSgtins",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_03_2_GetSgtins_EmptyListIsNotAllowed",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_03_3_GetPublicSgtins",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_03_3_GetPublicSgtins_EmptyListIsNotAllowed",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_03_4_GetSgtin",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_03_5_GetSgtinsOnHold",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_03_6_GetSgtinsKktAwaitingWithdrawal",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_03_7_GetSgtinsDeviceAwaitingWithdrawal",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_04_1_GetSsccHierarchy_Found",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_04_1_GetSsccHierarchy_NotFound",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_04_2_GetSsccSgtins",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_04_2_GetSsccSgtinsNoImmediateSgtins",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_04_3_GetSsccFullHierarchy",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_05_1_GetCurrentMedProducts",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_05_2_GetCurrentMedProduct",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_05_3_GetPublicMedProducts",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_05_4_GetPublicMedProduct",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_06_2_GetForeignCounterparties",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_07_1_AddTrustedPartners",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_07_2_DeleteTrustedPartners",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_07_3_GetTrustedPartners",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_08_1_GetForeignPartners",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_08_1_GetLocalPartners",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_09_1_GetCurrentMember",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_09_2_UpdateCurrentMember",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_09_3_GetCurrentBillingInfo",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_10_1_GetEmissionDevices",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_10_2_GetWithdrawalDevices",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_11_1_GetVirtualStorage",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_12_1_GetPausedCirculationDecisions",
            "MdlpApiClient.Tests.ApiTestsChapter8.Chapter8_12_2_GetPausedCirculationSgtins",
        };

        private static readonly string[] LegacyIntegrationAllowPatterns = (LegacyIntegrationTestsAllowRaw ?? string.Empty)
            .Split(new[] { ',', ';', '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        private static readonly object SandboxAvailabilitySync = new object();
        private static readonly HttpClient SandboxProbeClient = CreateSandboxProbeClient();
        private static bool? sandboxIsAvailable;
        private static string sandboxAvailabilityDetails;

        private static HttpClient CreateSandboxProbeClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };

            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        static UnitTestsBase()
        {
            // detect CI runner environment
            var ci = Environment.GetEnvironmentVariable("GITLAB_CI") != null;
            if (ci)
            {
                TestContext.Progress.WriteLine("Running unit tests on CI server.");
            }

            // // register old ServiceStack library that has limits
            // if (typeof(ServiceStack.Text.JsonSerializer).Assembly.GetName().Version <= new Version("4.0.33.0"))
            // {
            //     var licenseKey = Environment.GetEnvironmentVariable("SERVICE_STACK4_LICENSE");
            //     ServiceStack.Licensing.RegisterLicense(licenseKey);
            // }

            // for continuous integration: use certificates installed on the local machine
            // for unit tests run inside Visual Studio: use current user's certificates
            GostCryptoHelpers.DefaultStoreLocation = ci ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;

            // Some local CSP containers require UI interaction when signing (common on ARM64 setups).
            // Enable interactive fallback by default for local runs; can be overridden by env variable.
            GostCryptoHelpers.AllowInteractiveSigning = EnvBoolOrDefault("MDLP_ALLOW_INTERACTIVE_SIGNING", !ci);
            GostCryptoHelpers.AllowCryptoProCliSigningFallback = EnvBoolOrDefault("MDLP_ALLOW_CRYPTOPRO_CLI_FALLBACK", !ci);
            GostCryptoHelpers.CryptoProCsptestPath = CryptoProCsptestPath;
            GostCryptoHelpers.CryptoProContainerPin = CryptoProContainerPin;
        }

        public UnitTestsBase()
        {
            WriteLine("====> {0} <====", GetType().Name);
        }

        protected static void RequireSandboxAvailabilityOrIgnore()
        {
            if (!SkipSandboxTestsWhenUnavailable)
            {
                return;
            }

            string details;
            if (!IsSandboxAvailable(out details))
            {
                Assert.Ignore("Sandbox API is unavailable in current environment. " + details);
            }
        }

        private static bool IsSandboxAvailable(out string details)
        {
            lock (SandboxAvailabilitySync)
            {
                if (!sandboxIsAvailable.HasValue)
                {
                    ProbeSandboxAvailability();
                }

                details = sandboxAvailabilityDetails;
                return sandboxIsAvailable.GetValueOrDefault(false);
            }
        }

        private static void ProbeSandboxAvailability()
        {
            var probeUrl = (TestApiBaseUrl ?? MdlpClient.SandboxApiHttps).TrimEnd('/') + "/documents/doc_size";
            var primaryProbeFailureDetails = string.Empty;

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, probeUrl))
                using (var response = SandboxProbeClient.Send(request))
                {
                    sandboxIsAvailable = true;
                    sandboxAvailabilityDetails = "Probe response: HTTP " + (int)response.StatusCode;
                }

                return;
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode.HasValue)
                {
                    sandboxIsAvailable = true;
                    sandboxAvailabilityDetails = "Probe response: HTTP " + (int)ex.StatusCode.Value;
                    return;
                }

                primaryProbeFailureDetails = "Probe failed with HttpRequestException: " + ex.Message;
            }
            catch (TaskCanceledException ex)
            {
                primaryProbeFailureDetails = "Probe failed with timeout: " + ex.Message;
            }
            catch (Exception ex)
            {
                primaryProbeFailureDetails = "Probe failed with " + ex.GetType().Name + ": " + ex.Message;
            }

            string cryptoProPreflightDetails;
            if (TryProbeSandboxAvailabilityWithCryptoProSspi(probeUrl, out cryptoProPreflightDetails))
            {
                sandboxIsAvailable = true;
                sandboxAvailabilityDetails = primaryProbeFailureDetails + " Fallback succeeded: " + cryptoProPreflightDetails;
                return;
            }

            sandboxIsAvailable = false;
            sandboxAvailabilityDetails = primaryProbeFailureDetails + " " + cryptoProPreflightDetails;
        }

        private static bool TryProbeSandboxAvailabilityWithCryptoProSspi(string probeUrl, out string details)
        {
            details = "CryptoPro SSP preflight skipped.";

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                details = "CryptoPro SSP preflight skipped: Windows only.";
                return false;
            }

            if (!Uri.TryCreate(probeUrl, UriKind.Absolute, out var uri))
            {
                details = "CryptoPro SSP preflight skipped: invalid probe URL '" + probeUrl + "'.";
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                details = "CryptoPro SSP preflight skipped: endpoint is not HTTPS.";
                return false;
            }

            var csptestPath = ResolveCsptestPath();
            if (string.IsNullOrWhiteSpace(csptestPath))
            {
                details = "CryptoPro SSP preflight skipped: csptest.exe is not found.";
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = csptestPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(csptestPath) ?? Environment.CurrentDirectory,
            };

            startInfo.ArgumentList.Add("-tlsc");
            startInfo.ArgumentList.Add("-server");
            startInfo.ArgumentList.Add(uri.Host);
            startInfo.ArgumentList.Add("-port");
            startInfo.ArgumentList.Add((uri.IsDefaultPort ? 443 : uri.Port).ToString());
            startInfo.ArgumentList.Add("-file");
            startInfo.ArgumentList.Add(string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "/" : uri.AbsolutePath);
            startInfo.ArgumentList.Add("-exchange");
            startInfo.ArgumentList.Add("3");
            startInfo.ArgumentList.Add("-cpsspi");
            startInfo.ArgumentList.Add("-forcecheck");
            startInfo.ArgumentList.Add("-nosave");

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        details = "CryptoPro SSP preflight failed: csptest process did not start.";
                        return false;
                    }

                    if (!process.WaitForExit(15000))
                    {
                        TryKillProcess(process);
                        details = "CryptoPro SSP preflight failed: timeout after 15 seconds.";
                        return false;
                    }

                    var stdout = process.StandardOutput.ReadToEnd().Trim();
                    var stderr = process.StandardError.ReadToEnd().Trim();
                    if (process.ExitCode == 0)
                    {
                        details = "CryptoPro SSP preflight OK via '" + csptestPath + "'.";
                        return true;
                    }

                    details = "CryptoPro SSP preflight failed with exit code " + process.ExitCode + ". stderr: " + stderr + "; stdout: " + stdout;
                    return false;
                }
            }
            catch (Exception ex)
            {
                details = "CryptoPro SSP preflight failed with " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static string ResolveCsptestPath()
        {
            var configuredPath = CryptoProCsptestPath;
            if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            {
                return configuredPath;
            }

            foreach (var candidate in new[]
            {
                Environment.GetEnvironmentVariable("MDLP_CRYPTOPRO_CSPTEST_PATH"),
                @"C:\Program Files\Crypto Pro\CSP\csptest.exe",
                @"C:\Program Files (x86)\Crypto Pro\CSP\csptest.exe",
            })
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }

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

                foreach (var executableName in new[] { "csptest.exe", "csptestf.exe", "csptest", "csptestf" })
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

        private static void TryKillProcess(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        public virtual void Dispose()
        {
            WriteLine("<==== {0}.Dispose() ====>", GetType().Name);
        }

        private static bool IsLegacyIntegrationFixture()
        {
            var className = TestContext.CurrentContext?.Test?.ClassName;
            if (string.IsNullOrWhiteSpace(className))
            {
                return false;
            }

            var lastDot = className.LastIndexOf('.');
            var shortName = lastDot >= 0 ? className.Substring(lastDot + 1) : className;
            return LegacyIntegrationFixtureNames.Contains(shortName);
        }

        private static bool IsAllowedLegacyIntegrationTest()
        {
            var test = TestContext.CurrentContext?.Test;
            if (test == null)
            {
                return false;
            }

            var fullName = test.FullName ?? string.Empty;
            if (RestoredLegacyIntegrationTests.Contains(fullName))
            {
                return true;
            }

            if (LegacyIntegrationAllowPatterns.Length == 0)
            {
                return false;
            }

            var testName = test.Name ?? string.Empty;
            foreach (var pattern in LegacyIntegrationAllowPatterns)
            {
                if (fullName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    testName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        [SetUp]
        public void SetupBeforeEachTest()
        {
            if (!EnableLegacyIntegrationTests && IsLegacyIntegrationFixture() && !IsAllowedLegacyIntegrationTest())
            {
                Assert.Ignore("Legacy integration tests are temporarily disabled. " +
                    "Set MDLP_ENABLE_LEGACY_INTEGRATION_TESTS=true to run them all, or " +
                    "MDLP_ENABLE_LEGACY_INTEGRATION_TESTS_ALLOW=<pattern> to run one by one.");
            }

            WriteLine("------> {0} <------", TestContext.CurrentContext.Test.MethodName);
        }

        protected void WriteLine()
        {
        #if TRACE
            TestContext.Progress.WriteLine();
        #endif
        }

        protected void WriteLine(string format = "", params object[] args)
        {
        #if TRACE
            if (args != null && args.Length == 0)
            {
                // avoid formatting curly braces if no arguments are given
                TestContext.Progress.WriteLine(format);
                return;
            }

            TestContext.Progress.WriteLine(format, args);
        #endif
        }

        /// <summary>
        /// Asserts that all required data members are not empty.
        /// </summary>
        /// <typeparam name="T">The type of the data contract.</typeparam>
        /// <param name="dataContract">The instance of the data contract.</param>
        protected void AssertRequiredItems<T>(IEnumerable<T> dataContract)
        {
            Assert.NotNull(dataContract);
            foreach (var item in dataContract)
            {
                AssertRequired(item);
            }
        }

        private Type[] SkipTypesWhenCheckingForRequiredProperties = new[]
        {
            typeof(int), typeof(long), typeof(short), typeof(sbyte),
            typeof(uint), typeof(ulong), typeof(ushort), typeof(byte),
            typeof(decimal), typeof(float),typeof(double), typeof(bool)
        };

        /// <summary>
        /// Asserts that all required data members are not empty.
        /// </summary>
        /// <typeparam name="T">The type of the data contract.</typeparam>
        /// <param name="dataContract">The instance of the data contract.</param>
        protected void AssertRequired<T>(T dataContract)
        {
            Assert.NotNull(dataContract);

            // numeric properties can be 0, and boolean can be false, that's ok
            var requiredMembers =
                from p in typeof(T).GetProperties()
                let dm = p.GetCustomAttributes(typeof(DataMemberAttribute), false)
                    .OfType<DataMemberAttribute>()
                    .FirstOrDefault()
                where dm != null && dm.IsRequired == true
                where !SkipTypesWhenCheckingForRequiredProperties.Contains(p.PropertyType)
                select p;

            var required = requiredMembers.ToArray();
            if (!required.Any())
            {
                return;
            }

            foreach (var p in required)
            {
                var value = p.GetValue(dataContract);
                var defaultValue = p.PropertyType.IsClass ? null : Activator.CreateInstance(p.PropertyType);
                Assert.AreNotEqual(value, defaultValue, "Property " + p.DeclaringType.Name + "." + p.Name + " is not set");
            }
        }
    }
}

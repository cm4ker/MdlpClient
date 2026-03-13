namespace MdlpApiClient.Toolbox
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;

    /// <summary>
    /// Cryptographic utilities for GOST provider.
    /// </summary>
    public class GostCryptoHelpers
    {
        /// <summary>
        /// For the unit tests, set this to the StoreLocation.CurrentUser.
        /// For the production code, keep it set to the StoreLocation.LocalMachine.
        /// Only Administrator or LocalSystem accounts can access the LocalMachine stores.
        /// </summary>
        public static StoreLocation DefaultStoreLocation = StoreLocation.LocalMachine;

        /// <summary>
        /// Allows retrying signature generation in non-silent mode when the CSP key container requires UI interaction.
        /// Keep this disabled in headless production environments.
        /// </summary>
        public static bool AllowInteractiveSigning = false;

        /// <summary>
        /// Allows fallback signing through CryptoPro command line tool (csptest.exe)
        /// when .NET SignedCms cannot access the key container.
        /// </summary>
        public static bool AllowCryptoProCliSigningFallback = false;

        /// <summary>
        /// Optional custom path to csptest.exe.
        /// If not set, default CryptoPro locations are probed.
        /// </summary>
        public static string CryptoProCsptestPath = string.Empty;

        /// <summary>
        /// Optional PIN for key container access in csptest fallback mode.
        /// </summary>
        public static string CryptoProContainerPin = string.Empty;

        /// <summary>
        /// Timeout for csptest execution in milliseconds.
        /// </summary>
        public static int CryptoProCliTimeoutMs = 30000;

        internal static Func<IDetachedSignatureProvider> SignedCmsSignatureProviderFactory =
            () => new DotNetSignedCmsDetachedSignatureProvider(AllowInteractiveSigning);

        internal static Func<IDetachedSignatureProvider> CryptoProCliSignatureProviderFactory =
            () => new CryptoProCsptestDetachedSignatureProvider(CryptoProCsptestPath, CryptoProContainerPin, CryptoProCliTimeoutMs);

        static GostCryptoHelpers()
        {
            AllowInteractiveSigning = EnvBoolOrDefault("MDLP_ALLOW_INTERACTIVE_SIGNING", false);
            AllowCryptoProCliSigningFallback = EnvBoolOrDefault("MDLP_ALLOW_CRYPTOPRO_CLI_FALLBACK", true);
            CryptoProCsptestPath = Environment.GetEnvironmentVariable("MDLP_CRYPTOPRO_CSPTEST_PATH") ?? string.Empty;
            CryptoProContainerPin = Environment.GetEnvironmentVariable("MDLP_CRYPTOPRO_PIN") ?? string.Empty;

            var timeoutValue = Environment.GetEnvironmentVariable("MDLP_CRYPTOPRO_CLI_TIMEOUT_MS");
            int timeout;
            if (int.TryParse(timeoutValue, out timeout) && timeout > 0)
            {
                CryptoProCliTimeoutMs = timeout;
            }
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

        /// <summary>
        /// Checks if GOST cryptoprovider CryptoPro is installed.
        /// </summary>
        public static bool IsGostCryptoProviderInstalled()
        {
            return !string.IsNullOrWhiteSpace(
                CryptoProCsptestDetachedSignatureProvider.ResolveCsptestPath(CryptoProCsptestPath));
        }

        /// <summary>
        /// Looks for the GOST certificate with a private key using the subject name or a thumbprint.
        /// Returns null, if certificate is not found, the algorithm isn't GOST-compliant, or the private key is not associated with it.
        /// </summary>
        public static X509Certificate2 FindCertificate(string cnameOrThumbprint, StoreName storeName = StoreName.My, StoreLocation? storeLocation = null)
        {
            // avoid returning any certificate
            if (string.IsNullOrWhiteSpace(cnameOrThumbprint))
            {
                return null;
            }

            var normalizedIdentifier = NormalizeCertificateIdentifier(cnameOrThumbprint);

            // try preferred location first, then fallback to the other one
            var locations = storeLocation.HasValue
                ? new[] { storeLocation.Value }
                : new[]
                {
                    DefaultStoreLocation,
                    DefaultStoreLocation == StoreLocation.CurrentUser ? StoreLocation.LocalMachine : StoreLocation.CurrentUser
                };

            foreach (var location in locations)
            {
                using (var store = new X509Store(storeName, location))
                {
                    store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);

                    foreach (var certificate in store.Certificates)
                    {
                        if (!certificate.HasPrivateKey || !IsLikelyGostCertificate(certificate))
                        {
                            continue;
                        }

                        var subjectName = certificate.SubjectName.Name ?? string.Empty;
                        var subject = certificate.Subject ?? string.Empty;
                        var nameMatches = subjectName.IndexOf(cnameOrThumbprint, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          subject.IndexOf(cnameOrThumbprint, StringComparison.OrdinalIgnoreCase) >= 0;

                        // compare thumbprint/serial numbers in normalized form
                        var thumbprintMatches = StringComparer.OrdinalIgnoreCase.Equals(
                            NormalizeCertificateIdentifier(certificate.Thumbprint),
                            normalizedIdentifier);

                        var serialMatches = StringComparer.OrdinalIgnoreCase.Equals(
                            NormalizeCertificateIdentifier(certificate.SerialNumber),
                            normalizedIdentifier);

                        if (nameMatches || thumbprintMatches || serialMatches)
                        {
                            return certificate;
                        }
                    }
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

        private static bool IsLikelyGostCertificate(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                return false;
            }

            var publicKeyOid = certificate.PublicKey?.Oid?.Value;
            if (IsGostOid(publicKeyOid))
            {
                return true;
            }

            var signatureOid = certificate.SignatureAlgorithm?.Value;
            return IsGostOid(signatureOid);
        }

        private static bool IsGostOid(string oid)
        {
            return !string.IsNullOrWhiteSpace(oid) &&
                oid.StartsWith("1.2.643.", StringComparison.Ordinal);
        }

        private static bool IsSilentContextError(Exception ex)
        {
            var current = ex;
            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.Message) &&
                    current.Message.IndexOf("silent", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        /// <summary>
        /// Signs the message with a GOST digital signature and returns the detached signature (CMS format, base64 encoding).
        /// Detached signature is a CMS message, that doesn't contain the original signed data: only the signature and the certificates.
        /// </summary>
        public static string ComputeDetachedSignature(X509Certificate2 certificate, string textToSign)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (textToSign == null)
            {
                throw new ArgumentNullException(nameof(textToSign));
            }

            if (!certificate.HasPrivateKey)
            {
                throw new CryptographicException("Certificate does not have an associated private key.");
            }

            string signature;
            Exception signedCmsError;
            var signedCmsProvider = SignedCmsSignatureProviderFactory();
            if (signedCmsProvider.TryComputeDetachedSignature(certificate, textToSign, out signature, out signedCmsError))
            {
                return signature;
            }

            Exception cliError = null;
            if (AllowCryptoProCliSigningFallback)
            {
                var cliProvider = CryptoProCliSignatureProviderFactory();
                if (cliProvider.TryComputeDetachedSignature(certificate, textToSign, out signature, out cliError))
                {
                    return signature;
                }
            }

            var errorMessage = "Failed to compute detached GOST signature. " +
                "Ensure the private key container is accessible to the current user";

            if (!AllowInteractiveSigning || !IsSilentContextError(signedCmsError))
            {
                errorMessage += " and does not require interactive UI prompts.";
            }
            else
            {
                errorMessage += " and can be used in interactive mode.";
            }

            if (AllowCryptoProCliSigningFallback && cliError != null && !string.IsNullOrWhiteSpace(cliError.Message))
            {
                errorMessage += " CryptoPro CLI fallback failed: " + cliError.Message;
            }

            throw new CryptographicException(errorMessage, signedCmsError ?? cliError);
        }
    }
}

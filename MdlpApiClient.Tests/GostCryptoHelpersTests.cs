namespace MdlpApiClient.Tests
{
    using MdlpApiClient.Toolbox;
    using NUnit.Framework;
    using System.Security.Cryptography.X509Certificates;

    [TestFixture]
    public class GostCryptoHelpersTests : UnitTestsBase
    {
        private X509Certificate2 GetTestCertificate()
        {
            var cert = GostCryptoHelpers.FindCertificate(TestCertificateThumbprint);
            if (cert != null)
            {
                return cert;
            }

            if (!string.IsNullOrWhiteSpace(TestCertificateSerialNumber))
            {
                cert = GostCryptoHelpers.FindCertificate(TestCertificateSerialNumber);
                if (cert != null)
                {
                    return cert;
                }
            }

            return GostCryptoHelpers.FindCertificate(TestCertificateSubjectName);
        }

        [Test]
        public void GostCryproProviderIsInstalled()
        {
            Assert.IsTrue(GostCryptoHelpers.IsGostCryptoProviderInstalled());
        }

        [Test]
        public void CertificateWithPrivateKeyIsLoaded()
        {
            var cert = GetTestCertificate();
            Assert.IsNotNull(cert,
                "GOST certificate was not found. Configure MDLP_CERT_THUMBPRINT / MDLP_CERT_SERIAL_NUMBER / MDLP_CERT_SUBJECT_NAME.");
            Assert.IsTrue(cert.HasPrivateKey, "Certificate does not have an associated private key.");
        }

        [Test]
        public void CertificateCanBeUsedToComputeDetachedCmsSignature()
        {
            var cert = GetTestCertificate();
            var sign = GostCryptoHelpers.ComputeDetachedSignature(cert, "Привет!");
            Assert.IsNotNull(sign);
            Assert.IsTrue(sign.StartsWith("MII"));
            Assert.IsTrue(sign.Length > 1000);
        }
    }
}

namespace MdlpApiClient.Toolbox
{
    using GostCryptography.Pkcs;
    using System;
    using System.Security.Cryptography.Pkcs;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;

    internal class GostSignedCmsDetachedSignatureProvider : IDetachedSignatureProvider
    {
        private readonly bool allowInteractiveSigning;

        public GostSignedCmsDetachedSignatureProvider(bool allowInteractiveSigning)
        {
            this.allowInteractiveSigning = allowInteractiveSigning;
        }

        public string ProviderName => "GostSignedCms";

        public bool TryComputeDetachedSignature(
            X509Certificate2 certificate,
            string textToSign,
            out string signature,
            out Exception error)
        {
            signature = null;
            error = null;

            try
            {
                var message = Encoding.UTF8.GetBytes(textToSign);

                var signedCms = new GostSignedCms(new ContentInfo(message), true);
                var signer = new CmsSigner(certificate);

                signedCms.ComputeSignature(signer, silent: !allowInteractiveSigning);
                signature = Convert.ToBase64String(signedCms.Encode());
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }
    }
}

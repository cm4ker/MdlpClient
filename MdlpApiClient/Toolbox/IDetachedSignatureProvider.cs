namespace MdlpApiClient.Toolbox
{
    using System;
    using System.Security.Cryptography.X509Certificates;

    internal interface IDetachedSignatureProvider
    {
        string ProviderName { get; }

        bool TryComputeDetachedSignature(
            X509Certificate2 certificate,
            string textToSign,
            out string signature,
            out Exception error);
    }
}

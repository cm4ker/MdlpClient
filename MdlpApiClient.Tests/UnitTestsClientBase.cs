namespace MdlpApiClient.Tests
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    public class UnitTestsClientBase : UnitTestsBase
    {
        public UnitTestsClientBase()
        {
            Client = CreateClient();
        }

        [SetUp]
        public void EnsureSandboxIsAvailable()
        {
            RequireSandboxAvailabilityOrIgnore();
        }

        protected MdlpClient Client { get; private set; }

        protected virtual MdlpClient CreateClient()
        {
            // NonResidentCredentials (password-based) are no longer valid on sandbox.
            // Use ResidentCredentials (SIGNED_CODE via GOST cert) — same as ApiTestsChapter5.
            var client = new MdlpClient(credentials: new ResidentCredentials
            {
                ClientID = ClientID1,
                ClientSecret = ClientSecret1,
                UserID = TestUserThumbprint,
            },
            baseUrl: TestApiBaseUrl)
            {
                Tracer = WriteLine,
            };

            client.Client.RemoteCertificateValidationCallback += (sender, certificate, chain, errors) => true;
            return client;
        }

        public override void Dispose()
        {
            if (Client != null)
            {
                Client.Tracer = null;
                Client.Dispose();
                Client = null;
            }

            base.Dispose();
        }
    }
}

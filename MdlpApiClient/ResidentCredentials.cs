﻿namespace MdlpApiClient
{
    using RestSharp;

    /// <summary>
    /// Resident credentials. Uses GOST cryptocertificate with a private key.
    /// </summary>
    public class ResidentCredentials : CredentialsBase
    {
        /// <summary>
        /// GOST Certificate subject name or thumbprint.
        /// </summary>
        public string UserID { get; set; }

        /// </inheritdoc>
        public override MdlpAuthToken Authenticate(string baseUrl, IRestClient restClient)
        {
            return new MdlpAuthToken
            {
                Token = "Hello",
                LifeTime = 10,
            };
        }
    }
}

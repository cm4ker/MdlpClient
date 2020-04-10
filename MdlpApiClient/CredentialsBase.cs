﻿namespace MdlpApiClient
{
    using RestSharp;

    /// <summary>
    /// MDLP REST API credentials base class.
    /// </summary>
    public abstract class CredentialsBase
    {
        /// <summary>
        /// Client identifier.
        /// </summary>
        public string ClientID { get; set; }

        /// <summary>
        /// Client secret.
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// Performs authentication, returns access token with a limited lifetime.
        /// </summary>
        /// <param name="baseUrl">Base URL of the API endpoint.</param>
        /// <param name="restClient">REST client to perform API calls.</param>
        /// <returns><see cref="MdlpAuthToken"/> instance.</returns>
        public abstract MdlpAuthToken Authenticate(string baseUrl, IRestClient restClient);
    }
}

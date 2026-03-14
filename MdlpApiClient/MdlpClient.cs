namespace MdlpApiClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Xml;
    using MdlpApiClient.DataContracts;
    using MdlpApiClient.Serialization;
    using MdlpApiClient.Toolbox;
    using RestSharp;
    using RestSharp.Serialization;

    /// <summary>
    /// MDLP REST API client.
    /// </summary>
    public partial class MdlpClient : IDisposable
    {
        private const string UseCryptoProHttpHandlerEnvName = "MDLP_USE_CRYPTOPRO_HTTP_HANDLER";
        private const string UseCryptoProStdioProxyEnvName = "MDLP_USE_CRYPTOPRO_STDIO_PROXY";
        private const string CryptoProStdioDotnetPathEnvName = "MDLP_CRYPTOPRO_STDIO_DOTNET_PATH";
        private const string CryptoProStdioProxyPathEnvName = "MDLP_CRYPTOPRO_STDIO_PROXY_PATH";
        private const string LegacyDotnetX64PathEnvName = "MDLP_DOTNET_X64_PATH";
        private const string SkipServerCertificateValidationEnvName = "MDLP_CRYPTOPRO_HTTP_HANDLER_INSECURE_SKIP_CERT_VALIDATION";
        private const string ForceTls12EnvName = "MDLP_CRYPTOPRO_HTTP_HANDLER_FORCE_TLS12";

        /// <summary>
        /// Stage API HTTP URL.
        /// </summary>
        public const string StageApiHttp = "http://api.stage.mdlp.crpt.ru/api/v1/";

        /// <summary>
        /// Stage API HTTPS URL.
        /// </summary>
        public const string StageApiHttps = "https://api.stage.mdlp.crpt.ru/api/v1/";

        /// <summary>
        /// Sandbox API HTTP URL.
        /// </summary>
        public const string SandboxApiHttp = "http://sb.mdlp.crpt.ru/api/v1/";

        /// <summary>
        /// Sandbox API HTTPS URL.
        /// </summary>
        public const string SandboxApiHttps = "https://sb.mdlp.crpt.ru/api/v1/";

        /// <summary>
        /// Initializes a new instance of the MDLP REST API client.
        /// </summary>
        /// <param name="credentials">Credentials used for authentication.</param>
        /// <param name="client"><see cref="IRestClient"/> instance.</param>
        public MdlpClient(CredentialsBase credentials, IRestClient client)
        {
            Credentials = credentials;
            Serializer = new ServiceStackSerializer();
            BaseUrl = NormalizeLegacySandboxBaseUrl(client.BaseUrl?.ToString() ?? StageApiHttp);
            Limiter = new RequestRateLimiter();

            // set up REST client
            Client = client;
            if (!string.Equals(Client.BaseUrl?.ToString(), BaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                Client.BaseUrl = new Uri(BaseUrl, UriKind.Absolute);
            }

            Client.Authenticator = new CredentialsAuthenticator(this, credentials);
            Client.Encoding = Encoding.UTF8;
            Client.ThrowOnDeserializationError = false;
            Client.UseSerializer(() => Serializer);
        }

        /// <summary>
        /// Initializes a new instance of the MDLP REST API client.
        /// </summary>
        /// <param name="credentials">Credentials used for authentication.</param>
        /// <param name="baseUrl">Base URL of the API endpoint.</param>
        public MdlpClient(CredentialsBase credentials, string baseUrl = StageApiHttp)
            : this(credentials, new RestClient(NormalizeLegacySandboxBaseUrl(baseUrl ?? StageApiHttp)))
        {
        }

        private static string NormalizeLegacySandboxBaseUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return value.Replace("https://api.sb.mdlp.crpt.ru/api/v1/", SandboxApiHttps, StringComparison.OrdinalIgnoreCase)
                .Replace("http://api.sb.mdlp.crpt.ru/api/v1/", SandboxApiHttp, StringComparison.OrdinalIgnoreCase)
                .Replace("https://api.sb.mdlp.crpt.ru/api/v1", SandboxApiHttps.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                .Replace("http://api.sb.mdlp.crpt.ru/api/v1", SandboxApiHttp.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            IsDisposed = true;
            if (IsAuthenticated)
            {
                Logout();
            }
        }

        /// <summary>
        /// Gets or sets the application name.
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Gets base API URL.
        /// </summary>
        public string BaseUrl { get; private set; }

        private ServiceStackSerializer Serializer { get; set; }

        private RequestRateLimiter Limiter { get; set; }

        /// <summary>
        /// Gets the overridden values of delays between requests.
        /// </summary>
        /// <remarks>
        /// Sample usage:
        /// client.RequestDelays[nameof(client.SendDocument)] = TimeSpan.FromSeconds(5);
        /// </remarks>
        public Dictionary<string, TimeSpan> RequestDelays => Limiter.RequestDelays;

        private void RequestRate(double seconds, [CallerMemberName]string methodName = null)
        {
            var correction = IsAuthenticated ? 0.3 : 2;
            Limiter.Delay(TimeSpan.FromSeconds(seconds + correction), methodName);
        }

        /// <summary>
        /// Gets the <see cref="IRestClient"/> instance.
        /// </summary>
        public IRestClient Client { get; private set; }

        private CredentialsBase Credentials { get; set; }

        private X509Certificate2 userCertificate;

        /// <summary>
        /// Gets a value indicating whether the client is disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        internal bool IsAuthenticated { get; private set; }

        /// <summary>
        /// X.509 certificate of the resident user (if applicable).
        /// </summary>
        internal X509Certificate2 UserCertificate
        {
            set { userCertificate = value; }
            get
            {
                if (userCertificate == null)
                {
                    userCertificate = GostCryptoHelpers.FindCertificate(Credentials.UserID);
                }

                return userCertificate;
            }
        }

        /// <summary>
        /// Computes the detached digital signature of the given text.
        /// </summary>
        /// <param name="textToSign">Text to sign.</param>
        /// <returns>Detached signature in CMS format and base64 encoding.</returns>
        private string ComputeSignature(string textToSign)
        {
            if (UserCertificate == null)
            {
                return null;
            }

            return GostCryptoHelpers.ComputeDetachedSignature(UserCertificate, textToSign);
        }

        private void PrepareRequest(IRestRequest request, string apiMethodName)
        {
            // use request parameters to store additional properties, not really used by the requests
            request.AddParameter(ApiTimestampParameterName, DateTime.Now.Ticks, ParameterType.UrlSegment);
            request.AddParameter(ApiTickCountParameterName, Environment.TickCount.ToString(), ParameterType.UrlSegment);
            if (!string.IsNullOrWhiteSpace(apiMethodName))
            {
                request.AddHeader(ApiMethodNameHeaderName, apiMethodName);
            }

            // trace requests and responses
            if (Tracer != null)
            {
                request.OnBeforeRequest = http => Trace(http, request);
                request.OnBeforeDeserialization = resp => Trace(resp);
            }
        }

        private void ThrowOnFailure(IRestResponse response)
        {
            if (!response.IsSuccessful)
            {
                // try to find the non-empty error message
                var errorMessage = response.ErrorMessage;
                var contentMessage = response.Content;
                var errorResponse = default(ErrorResponse);
                if (response.ContentType != null)
                {
                    // Text/plain;charset=UTF-8 => text/plain
                    var contentType = response.ContentType.ToLower().Trim();
                    var semicolonIndex = contentType.IndexOf(';');
                    if (semicolonIndex >= 0)
                    {
                        contentType = contentType.Substring(0, semicolonIndex).Trim();
                    }

                    // Try to deserialize error response DTO
                    if (Serializer.SupportedContentTypes.Contains(contentType))
                    {
                        errorResponse = Serializer.Deserialize<ErrorResponse>(response);
                        contentMessage = string.Join(". ", new[]
                        {
                            errorResponse.Error,
                            errorResponse.Message,
                            errorResponse.Description,
                        }
                        .Distinct()
                        .Where(m => !string.IsNullOrWhiteSpace(m)));
                    }
                    else if (response.ContentType.ToLower().Contains("html"))
                    {
                        // Try to parse HTML
                        contentMessage = HtmlHelper.ExtractText(response.Content);
                    }
                    else
                    {
                        // Return as is assuming text/plain content
                        contentMessage = response.Content;
                    }
                }

                // HTML->XML deserialization errors are meaningless
                if (response.ErrorException is XmlException && errorMessage == response.ErrorException.Message)
                {
                    errorMessage = contentMessage;
                }

                // empty error message is meaningless
                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    errorMessage = contentMessage;
                }

                // finally, throw it
                throw new MdlpException(response.StatusCode, errorMessage, errorResponse, response.ErrorException);
            }
        }

        private void ThrowOnFailure(CryptoProCpHttpResponse response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (response.IsSuccessful)
            {
                return;
            }

            // try to find the non-empty error message
            var errorMessage = response.ErrorMessage;
            var contentMessage = response.Content;
            var errorResponse = default(ErrorResponse);
            if (!string.IsNullOrWhiteSpace(response.ContentType))
            {
                // Text/plain;charset=UTF-8 => text/plain
                var contentType = response.ContentType.ToLower().Trim();
                var semicolonIndex = contentType.IndexOf(';');
                if (semicolonIndex >= 0)
                {
                    contentType = contentType.Substring(0, semicolonIndex).Trim();
                }

                // Try to deserialize error response DTO
                if (Serializer.SupportedContentTypes.Contains(contentType))
                {
                    errorResponse = Serializer.Deserialize<ErrorResponse>(response.Content);
                    if (errorResponse != null)
                    {
                        contentMessage = string.Join(". ", new[]
                        {
                            errorResponse.Error,
                            errorResponse.Message,
                            errorResponse.Description,
                        }
                        .Distinct()
                        .Where(m => !string.IsNullOrWhiteSpace(m)));
                    }
                }
                else if (response.ContentType.ToLower().Contains("html"))
                {
                    // Try to parse HTML
                    contentMessage = HtmlHelper.ExtractText(response.Content);
                }
                else
                {
                    // Return as is assuming text/plain content
                    contentMessage = response.Content;
                }
            }

            // empty error message is meaningless
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                errorMessage = contentMessage;
            }

            // finally, throw it
            throw new MdlpException(response.StatusCode, errorMessage, errorResponse, response.ErrorException);
        }

        private static bool IsFlagEnabled(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "on":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsMdlpHost(Uri uri)
        {
            if (uri == null || string.IsNullOrWhiteSpace(uri.Host))
            {
                return false;
            }

            return uri.Host.Equals("mdlp.crpt.ru", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".mdlp.crpt.ru", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveStdioProxyDotnetPath()
        {
            var configuredPath = Environment.GetEnvironmentVariable(CryptoProStdioDotnetPathEnvName);
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                configuredPath = Environment.GetEnvironmentVariable(LegacyDotnetX64PathEnvName);
            }

            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return "dotnet";
            }

            if (Directory.Exists(configuredPath))
            {
                var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
                var executablePath = Path.Combine(configuredPath, executableName);
                return File.Exists(executablePath) ? executablePath : configuredPath;
            }

            return configuredPath;
        }

        private static string ResolveStdioProxyPath()
        {
            var configuredPath = Environment.GetEnvironmentVariable(CryptoProStdioProxyPathEnvName);
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                if (Directory.Exists(configuredPath))
                {
                    var directoryCandidate = ResolveStdioProxyPathFromDirectory(configuredPath);
                    if (!string.IsNullOrWhiteSpace(directoryCandidate))
                    {
                        return directoryCandidate;
                    }
                }

                if (File.Exists(configuredPath))
                {
                    return configuredPath;
                }
            }

            var baseDirectory = AppContext.BaseDirectory;
            var binCandidate = ResolveStdioProxyPathFromDirectory(baseDirectory);
            if (!string.IsNullOrWhiteSpace(binCandidate))
            {
                return binCandidate;
            }

            var repositoryRoot = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", ".."));
            var aotCandidate = ResolveStdioProxyPathFromDirectory(Path.Combine(repositoryRoot, "MdlpApiClient.StdioProxy", "aot", "win-x64"));
            if (!string.IsNullOrWhiteSpace(aotCandidate))
            {
                return aotCandidate;
            }

            var debugCandidate = ResolveStdioProxyPathFromDirectory(Path.Combine(repositoryRoot, "MdlpApiClient.StdioProxy", "bin", "Debug", "net8.0"));
            if (!string.IsNullOrWhiteSpace(debugCandidate))
            {
                return debugCandidate;
            }

            var releaseCandidate = ResolveStdioProxyPathFromDirectory(Path.Combine(repositoryRoot, "MdlpApiClient.StdioProxy", "bin", "Release", "net8.0"));
            if (!string.IsNullOrWhiteSpace(releaseCandidate))
            {
                return releaseCandidate;
            }

            return null;
        }

        private static string ResolveStdioProxyPathFromDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return null;
            }

            var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "MdlpApiClient.StdioProxy.exe"
                : "MdlpApiClient.StdioProxy";

            var executableCandidate = Path.Combine(directory, executableName);
            if (File.Exists(executableCandidate))
            {
                return executableCandidate;
            }

            var dllCandidate = Path.Combine(directory, "MdlpApiClient.StdioProxy.dll");
            if (File.Exists(dllCandidate))
            {
                return dllCandidate;
            }

            return null;
        }

        private int GetClientTimeoutMs()
        {
            var timeoutProperty = Client.GetType().GetProperty("Timeout");
            if (timeoutProperty?.GetValue(Client) is int timeout && timeout > 0)
            {
                return timeout;
            }

            return 100000;
        }

        private Uri BuildRequestUri(IRestRequest request)
        {
            var buildUriMethod = Client.GetType().GetMethod("BuildUri", new[] { typeof(IRestRequest) });
            if (buildUriMethod != null)
            {
                var builtUri = buildUriMethod.Invoke(Client, new object[] { request }) as Uri;
                if (builtUri != null)
                {
                    return builtUri;
                }
            }

            if (string.IsNullOrWhiteSpace(BaseUrl))
            {
                return null;
            }

            var baseUri = new Uri(BaseUrl, UriKind.Absolute);
            var resource = request?.Resource ?? string.Empty;
            return string.IsNullOrWhiteSpace(resource) ? baseUri : new Uri(baseUri, resource);
        }

        private bool UseCryptoProHttpHandler(IRestRequest request)
        {
            var flag = Environment.GetEnvironmentVariable(UseCryptoProHttpHandlerEnvName);
            if (!IsFlagEnabled(flag))
            {
                return false;
            }

            // libcore runtime assets in the bundled package are currently x64-only.
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            {
                return false;
            }

            var requestUri = BuildRequestUri(request);
            if (requestUri == null)
            {
                return false;
            }

            if (!string.Equals(requestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return IsMdlpHost(requestUri);
        }

        private bool TryExecuteWithCryptoProStdioProxy(IRestRequest request, out CryptoProCpHttpResponse response)
        {
            response = null;

            var flag = Environment.GetEnvironmentVariable(UseCryptoProStdioProxyEnvName);
            if (!IsFlagEnabled(flag))
            {
                return false;
            }

            var uri = BuildRequestUri(request);
            if (uri == null ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !IsMdlpHost(uri))
            {
                return false;
            }

            var dotnetPath = ResolveStdioProxyDotnetPath();
            var proxyPath = ResolveStdioProxyPath();
            if (string.IsNullOrWhiteSpace(proxyPath))
            {
                return false;
            }

            // RestSharp would invoke authenticator before each request. Do the same here.
            Client.Authenticator?.Authenticate(Client, request);

            var timeout = TimeSpan.FromMilliseconds(GetClientTimeoutMs());
            var skipServerCertificateValidation = IsFlagEnabled(Environment.GetEnvironmentVariable(SkipServerCertificateValidationEnvName));
            var forceTls12 = IsFlagEnabled(Environment.GetEnvironmentVariable(ForceTls12EnvName));

            var transport = new CryptoProStdioHttpTransport(
                timeout,
                dotnetPath,
                proxyPath,
                skipServerCertificateValidation,
                forceTls12);

            response = transport.Send(uri, request, Serializer);
            return true;
        }

        private bool TryExecuteWithCryptoProHttpHandler(IRestRequest request, out CryptoProCpHttpResponse response)
        {
            response = null;
            if (!UseCryptoProHttpHandler(request))
            {
                return false;
            }

            // RestSharp would invoke authenticator before each request. Do the same here.
            Client.Authenticator?.Authenticate(Client, request);

            var uri = BuildRequestUri(request);
            var timeout = TimeSpan.FromMilliseconds(GetClientTimeoutMs());
            var transport = new CryptoProCpHttpTransport(timeout);
            response = transport.Send(uri, request, Serializer);
            return true;
        }

        private bool TryExecuteWithCryptoProTransport(IRestRequest request, out CryptoProCpHttpResponse response)
        {
            if (TryExecuteWithCryptoProStdioProxy(request, out response))
            {
                return true;
            }

            return TryExecuteWithCryptoProHttpHandler(request, out response);
        }

        /// <summary>
        /// Executes the given request and checks the result.
        /// </summary>
        /// <typeparam name="T">Response type.</typeparam>
        /// <param name="request">The request to execute.</param>
        /// <param name="apiMethodName">Strong-typed REST API method name, for tracing.</param>
        internal T Execute<T>(IRestRequest request, string apiMethodName)
        {
            PrepareRequest(request, apiMethodName);
            if (TryExecuteWithCryptoProTransport(request, out var cryptoProResponse))
            {
                Trace(cryptoProResponse, request);
                ThrowOnFailure(cryptoProResponse);
                return Serializer.Deserialize<T>(cryptoProResponse.Content);
            }

            var response = Client.Execute<T>(request);
            ThrowOnFailure(response);
            return response.Data;
        }

        /// <summary>
        /// Executes the given request and checks the result.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <param name="apiMethodName">Strong-typed REST API method name, for tracing.</param>
        internal void Execute(IRestRequest request, string apiMethodName)
        {
            PrepareRequest(request, apiMethodName);
            if (TryExecuteWithCryptoProTransport(request, out var cryptoProResponse))
            {
                Trace(cryptoProResponse, request);
                ThrowOnFailure(cryptoProResponse);
                return;
            }

            var response = Client.Execute(request);

            // there is no body deserialization step, so we need to trace
            Trace(response);
            ThrowOnFailure(response);
        }

        /// <summary>
        /// Executes the given request and checks the result.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <param name="apiMethodName">Strong-typed REST API method name, for tracing.</param>
        internal string ExecuteString(IRestRequest request, string apiMethodName)
        {
            PrepareRequest(request, apiMethodName);
            if (TryExecuteWithCryptoProTransport(request, out var cryptoProResponse))
            {
                Trace(cryptoProResponse, request);
                ThrowOnFailure(cryptoProResponse);
                return cryptoProResponse.Content;
            }

            var response = Client.Execute(request);

            // there is no body deserialization step, so we need to trace
            Trace(response);
            ThrowOnFailure(response);
            return response.Content;
        }

        /// <summary>
        /// Performs GET request.
        /// </summary>
        /// <typeparam name="T">Response type.</typeparam>
        /// <param name="url">Resource url.</param>
        /// <param name="parameters">IRestRequest parameters.</param>
        /// <param name="apiMethodName">Strong-typed REST API method name, for tracing.</param>
        public T Get<T>(string url, Parameter[] parameters = null, [CallerMemberName] string apiMethodName = null)
        {
            var request = new RestRequest(url, Method.GET, DataFormat.Json);
            if (!parameters.IsNullOrEmpty())
            {
                request.AddOrUpdateParameters(parameters);
            }

            return Execute<T>(request, apiMethodName);
        }

        /// <summary>
        /// Performs GET request and returns a string.
        /// </summary>
        /// <param name="url">Resource url.</param>
        /// <param name="parameters">IRestRequest parameters.</param>
        /// <param name="apiMethodName">Strong-typed REST API method name, for tracing.</param>
        public string Get(string url, Parameter[] parameters = null, [CallerMemberName] string apiMethodName = null)
        {
            var request = new RestRequest(url, Method.GET, DataFormat.Json);
            if (!parameters.IsNullOrEmpty())
            {
                request.AddOrUpdateParameters(parameters);
            }

            return ExecuteString(request, apiMethodName);
        }

        /// <summary>
        /// Performs POST request.
        /// </summary>
        /// <typeparam name="T">Response type.</typeparam>
        /// <param name="url">Resource url.</param>
        /// <param name="body">Request body, to be serialized as JSON.</param>
        /// <param name="parameters">IRestRequest parameters.</param>
        /// <param name="apiMethodName">Strong-typed REST API method name, for tracing.</param>
        public T Post<T>(string url, object body, Parameter[] parameters = null, [CallerMemberName] string apiMethodName = null)
        {
            var request = new RestRequest(url, Method.POST, DataFormat.Json);
            request.AddJsonBody(body);
            if (!parameters.IsNullOrEmpty())
            {
                request.AddOrUpdateParameters(parameters);
            }

            return Execute<T>(request, apiMethodName);
        }

        /// <summary>
        /// Performs POST request.
        /// </summary>
        /// <param name="url">Resource url.</param>
        /// <param name="body">Request body, to be serialized as JSON.</param>
        /// <param name="parameters">IRestRequest parameters.</param>
        /// <param name="apiMethodName">Strong-typed REST API method name, for tracing.</param>
        public void Post(string url, object body, Parameter[] parameters = null, [CallerMemberName] string apiMethodName = null)
        {
            var request = new RestRequest(url, Method.POST, DataFormat.Json);
            request.AddJsonBody(body);
            if (!parameters.IsNullOrEmpty())
            {
                request.AddOrUpdateParameters(parameters);
            }

            Execute(request, apiMethodName);
        }

        /// <summary>
        /// Performs PUT request.
        /// </summary>
        /// <param name="url">Resource url.</param>
        /// <param name="body">Request body, to be serialized as JSON.</param>
        /// <param name="parameters">IRestRequest parameters.</param>
        /// <param name="apiMethodName">Strong-typed REST API method name, for tracing.</param>
        public void Put(string url, object body, Parameter[] parameters = null, [CallerMemberName] string apiMethodName = null)
        {
            var request = new RestRequest(url, Method.PUT, DataFormat.Json);
            request.AddJsonBody(body);
            if (!parameters.IsNullOrEmpty())
            {
                request.AddOrUpdateParameters(parameters);
            }

            Execute(request, apiMethodName);
        }

        /// <summary>
        /// Performs PUT request.
        /// </summary>
        /// <param name="url">Resource url.</param>
        /// <param name="body">Request body, serialized as string.</param>
        /// <param name="apiMethodName">Strong-typed REST API method name, for tracing.</param>
        public void Put(string url, string body, [CallerMemberName] string apiMethodName = null)
        {
            var request = new RestRequest(url, Method.PUT, DataFormat.None);
            request.AddParameter(string.Empty, body, ParameterType.RequestBody);
            Execute(request, apiMethodName);
        }

        /// <summary>
        /// Performs DELETE request.
        /// </summary>
        /// <param name="url">Resource url.</param>
        /// <param name="body">Request body, serialized as string.</param>
        /// <param name="parameters">IRestRequest parameters.</param>
        /// <param name="apiMethodName">Strong-typed REST API method name, for tracing.</param>
        public void Delete(string url, object body, Parameter[] parameters = null, [CallerMemberName] string apiMethodName = null)
        {
            var request = new RestRequest(url, Method.DELETE, DataFormat.Json);
            if (body != null)
            {
                request.AddJsonBody(body);
            }

            if (!parameters.IsNullOrEmpty())
            {
                request.AddOrUpdateParameters(parameters);
            }

            Execute(request, apiMethodName);
        }
    }
}

namespace MdlpApiClient.Toolbox
{
    using CryptoPro.Net.Http;
    using CryptoPro.Net.Security;
    using RestSharp;
    using RestSharp.Serialization;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;

    internal sealed class CryptoProCpHttpTransport
    {
        private const string SkipServerCertificateValidationEnvName = "MDLP_CRYPTOPRO_HTTP_HANDLER_INSECURE_SKIP_CERT_VALIDATION";
        private const string ForceTls12EnvName = "MDLP_CRYPTOPRO_HTTP_HANDLER_FORCE_TLS12";

        private readonly TimeSpan timeout;

        public CryptoProCpHttpTransport(TimeSpan timeout)
        {
            this.timeout = timeout > TimeSpan.Zero ? timeout : TimeSpan.FromSeconds(100);
        }

        public CryptoProCpHttpResponse Send(Uri uri, IRestRequest request, IRestSerializer serializer)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!CpHttpHandler.IsSupported)
            {
                return CreateTransportError(uri, new PlatformNotSupportedException("CpHttpHandler is not supported on the current platform."));
            }

            try
            {
                using (var handler = CreateHandler())
                using (var client = new HttpClient(handler, true))
                using (var message = new HttpRequestMessage(ToHttpMethod(request.Method), uri))
                {
                    client.Timeout = timeout;

                    AddHeaders(message, request.Parameters);
                    AddBody(message, request, serializer);

                    using (var response = client.SendAsync(message).GetAwaiter().GetResult())
                    {
                        var content = response.Content == null
                            ? string.Empty
                            : response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        return new CryptoProCpHttpResponse
                        {
                            StatusCode = response.StatusCode,
                            ContentType = response.Content?.Headers?.ContentType?.ToString(),
                            Content = content,
                            Headers = ReadHeaders(response),
                            ResponseUri = response.RequestMessage?.RequestUri ?? uri,
                            ErrorMessage = null,
                            ErrorException = null,
                            IsSuccessful = response.IsSuccessStatusCode,
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return CreateTransportError(uri, ex);
            }
        }

        private static CpHttpHandler CreateHandler()
        {
            var handler = new CpHttpHandler();

            var skipServerCertificateValidation = IsFlagEnabled(Environment.GetEnvironmentVariable(SkipServerCertificateValidationEnvName));
            var forceTls12 = IsFlagEnabled(Environment.GetEnvironmentVariable(ForceTls12EnvName));
            if (!skipServerCertificateValidation && !forceTls12)
            {
                return handler;
            }

            var options = new CpSslClientAuthenticationOptions();
            if (skipServerCertificateValidation)
            {
                options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
                options.CertificateRevocationCheckMode = X509RevocationMode.NoCheck;
            }

            if (forceTls12)
            {
                options.EnabledSslProtocols = SslProtocols.Tls12;
            }

            handler.SslOptions = options;
            return handler;
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

        private static CryptoProCpHttpResponse CreateTransportError(Uri uri, Exception ex)
        {
            return new CryptoProCpHttpResponse
            {
                StatusCode = default(HttpStatusCode),
                ContentType = null,
                Content = string.Empty,
                Headers = new List<Tuple<string, object>>(),
                ResponseUri = uri,
                ErrorMessage = ex?.Message,
                ErrorException = ex,
                IsSuccessful = false,
            };
        }

        private static HttpMethod ToHttpMethod(Method method)
        {
            switch (method)
            {
                case Method.GET:
                    return HttpMethod.Get;
                case Method.POST:
                    return HttpMethod.Post;
                case Method.PUT:
                    return HttpMethod.Put;
                case Method.DELETE:
                    return HttpMethod.Delete;
                case Method.HEAD:
                    return HttpMethod.Head;
                case Method.PATCH:
                    return new HttpMethod("PATCH");
                case Method.OPTIONS:
                    return HttpMethod.Options;
                default:
                    return new HttpMethod(method.ToString().ToUpperInvariant());
            }
        }

        private static void AddHeaders(HttpRequestMessage message, IList<Parameter> parameters)
        {
            if (parameters == null)
            {
                return;
            }

            foreach (var header in parameters.Where(p => p.Type == ParameterType.HttpHeader))
            {
                if (string.IsNullOrWhiteSpace(header.Name) || header.Value == null)
                {
                    continue;
                }

                if (header.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
                    header.Name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                    header.Name.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = Convert.ToString(header.Value, CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                message.Headers.TryAddWithoutValidation(header.Name, value);
            }
        }

        private static void AddBody(HttpRequestMessage message, IRestRequest request, IRestSerializer serializer)
        {
            var body = request.Body;
            if (body == null || body.Value == null)
            {
                return;
            }

            if (body.Value is byte[] bytes)
            {
                message.Content = new ByteArrayContent(bytes);
            }
            else
            {
                var text = body.Value as string;
                if (text == null)
                {
                    text = serializer.Serialize(body.Value);
                }

                message.Content = new StringContent(text ?? string.Empty, Encoding.UTF8);
            }

            var contentType = ResolveContentType(request);
            if (!string.IsNullOrWhiteSpace(contentType) &&
                MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
            {
                message.Content.Headers.ContentType = mediaType;
            }
        }

        private static string ResolveContentType(IRestRequest request)
        {
            var bodyType = request.Body?.ContentType;
            if (!string.IsNullOrWhiteSpace(bodyType))
            {
                return bodyType;
            }

            var contentTypeHeader = request.Parameters
                .Where(p => p.Type == ParameterType.HttpHeader)
                .FirstOrDefault(p => p.Name != null && p.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));

            if (contentTypeHeader?.Value != null)
            {
                var raw = Convert.ToString(contentTypeHeader.Value, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return raw;
                }
            }

            return "application/json";
        }

        private static List<Tuple<string, object>> ReadHeaders(HttpResponseMessage response)
        {
            var headers = new List<Tuple<string, object>>();

            foreach (var item in response.Headers)
            {
                headers.Add(Tuple.Create(item.Key, (object)string.Join(", ", item.Value)));
            }

            if (response.Content != null)
            {
                foreach (var item in response.Content.Headers)
                {
                    headers.Add(Tuple.Create(item.Key, (object)string.Join(", ", item.Value)));
                }
            }

            return headers;
        }
    }

    internal sealed class CryptoProCpHttpResponse
    {
        public HttpStatusCode StatusCode { get; set; }

        public string ContentType { get; set; }

        public string Content { get; set; }

        public List<Tuple<string, object>> Headers { get; set; }

        public Uri ResponseUri { get; set; }

        public string ErrorMessage { get; set; }

        public Exception ErrorException { get; set; }

        public bool IsSuccessful { get; set; }
    }
}

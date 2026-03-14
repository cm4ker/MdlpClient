namespace MdlpApiClient.Toolbox
{
    using RestSharp;
    using RestSharp.Serialization;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;

    internal sealed class CryptoProStdioHttpTransport
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        private readonly TimeSpan timeout;
        private readonly string dotnetPath;
        private readonly string proxyPath;
        private readonly bool skipServerCertificateValidation;
        private readonly bool forceTls12;

        public CryptoProStdioHttpTransport(
            TimeSpan timeout,
            string dotnetPath,
            string proxyPath,
            bool skipServerCertificateValidation,
            bool forceTls12)
        {
            this.timeout = timeout > TimeSpan.Zero ? timeout : TimeSpan.FromSeconds(100);
            this.dotnetPath = dotnetPath;
            this.proxyPath = proxyPath;
            this.skipServerCertificateValidation = skipServerCertificateValidation;
            this.forceTls12 = forceTls12;
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

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            if (string.IsNullOrWhiteSpace(proxyPath))
            {
                return CreateTransportError(uri, new InvalidOperationException("MDLP_CRYPTOPRO_STDIO_PROXY_PATH is not configured."));
            }

            if (RequiresDotnetHost(proxyPath) && string.IsNullOrWhiteSpace(dotnetPath))
            {
                return CreateTransportError(uri, new InvalidOperationException("MDLP_CRYPTOPRO_STDIO_DOTNET_PATH is not configured for proxy DLL mode."));
            }

            try
            {
                var payload = BuildRequestPayload(uri, request, serializer);
                var requestJson = JsonSerializer.Serialize(payload, JsonOptions);
                var responseJson = InvokeProxy(requestJson);

                if (string.IsNullOrWhiteSpace(responseJson))
                {
                    return CreateTransportError(uri, new InvalidOperationException("Stdio proxy returned an empty response."));
                }

                var responsePayload = JsonSerializer.Deserialize<ProxyHttpResponsePayload>(responseJson, JsonOptions);
                if (responsePayload == null)
                {
                    return CreateTransportError(uri, new InvalidOperationException("Stdio proxy returned invalid JSON response."));
                }

                return ConvertResponse(uri, responsePayload);
            }
            catch (Exception ex)
            {
                return CreateTransportError(uri, ex);
            }
        }

        private string InvokeProxy(string requestJson)
        {
            var timeoutMs = checked((int)Math.Min(timeout.TotalMilliseconds, int.MaxValue));
            var useDotnetHost = RequiresDotnetHost(proxyPath);
            var proxyDirectory = Path.GetDirectoryName(Path.GetFullPath(proxyPath));

            var startInfo = new ProcessStartInfo
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = string.IsNullOrWhiteSpace(proxyDirectory) ? Environment.CurrentDirectory : proxyDirectory,
            };

            if (useDotnetHost)
            {
                startInfo.FileName = dotnetPath;
                startInfo.ArgumentList.Add(proxyPath);
            }
            else
            {
                startInfo.FileName = proxyPath;
            }

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();

                process.StandardInput.Write(requestJson);
                process.StandardInput.Close();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(timeoutMs))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception killException)
                    {
                        Debug.WriteLine(killException);
                    }

                    throw new TimeoutException($"Stdio proxy timed out after {timeoutMs} ms.");
                }

                var stdout = outputTask.GetAwaiter().GetResult();
                var stderr = errorTask.GetAwaiter().GetResult();

                if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
                {
                    throw new InvalidOperationException($"Stdio proxy exited with code {process.ExitCode}. {stderr}".Trim());
                }

                return stdout;
            }
        }

        private static bool RequiresDotnetHost(string path)
        {
            return string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);
        }

        private ProxyHttpRequestPayload BuildRequestPayload(Uri uri, IRestRequest request, IRestSerializer serializer)
        {
            var bodyBytes = ResolveBodyBytes(request, serializer);
            return new ProxyHttpRequestPayload
            {
                Method = request.Method.ToString().ToUpperInvariant(),
                Uri = uri.AbsoluteUri,
                Headers = ReadHeaders(request.Parameters),
                ContentType = ResolveContentType(request),
                BodyBase64 = bodyBytes == null || bodyBytes.Length == 0 ? null : Convert.ToBase64String(bodyBytes),
                SkipServerCertificateValidation = skipServerCertificateValidation,
                ForceTls12 = forceTls12,
                TimeoutMs = checked((int)Math.Min(timeout.TotalMilliseconds, int.MaxValue)),
            };
        }

        private static byte[] ResolveBodyBytes(IRestRequest request, IRestSerializer serializer)
        {
            var body = request?.Body;
            if (body == null || body.Value == null)
            {
                return null;
            }

            var bodyValue = body.Value;

            if (bodyValue is byte[] bytes)
            {
                return bytes;
            }

            var text = bodyValue as string;
            if (text == null)
            {
                text = serializer.Serialize(bodyValue);
            }

            return Encoding.UTF8.GetBytes(text ?? string.Empty);
        }

        private static List<ProxyHeaderPayload> ReadHeaders(IList<Parameter> parameters)
        {
            if (parameters == null)
            {
                return new List<ProxyHeaderPayload>();
            }

            var headers = new List<ProxyHeaderPayload>();
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

                headers.Add(new ProxyHeaderPayload
                {
                    Name = header.Name,
                    Value = value,
                });
            }

            return headers;
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

        private static CryptoProCpHttpResponse ConvertResponse(Uri requestUri, ProxyHttpResponsePayload payload)
        {
            var headers = payload.Headers == null
                ? new List<Tuple<string, object>>()
                : payload.Headers
                    .Where(h => !string.IsNullOrWhiteSpace(h.Name))
                    .Select(h => Tuple.Create(h.Name, (object)(h.Value ?? string.Empty)))
                    .ToList();

            var responseUri = requestUri;
            if (!string.IsNullOrWhiteSpace(payload.ResponseUri) && Uri.TryCreate(payload.ResponseUri, UriKind.Absolute, out var absoluteUri))
            {
                responseUri = absoluteUri;
            }

            var errorException = string.IsNullOrWhiteSpace(payload.ErrorMessage)
                ? null
                : new InvalidOperationException(payload.ErrorMessage);

            return new CryptoProCpHttpResponse
            {
                StatusCode = payload.StatusCode <= 0 ? default(HttpStatusCode) : (HttpStatusCode)payload.StatusCode,
                ContentType = payload.ContentType,
                Content = payload.Content ?? string.Empty,
                Headers = headers,
                ResponseUri = responseUri,
                ErrorMessage = payload.ErrorMessage,
                ErrorException = errorException,
                IsSuccessful = payload.IsSuccessful,
            };
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

        internal sealed class ProxyHttpRequestPayload
        {
            public string Method { get; set; }

            public string Uri { get; set; }

            public string ContentType { get; set; }

            public string BodyBase64 { get; set; }

            public List<ProxyHeaderPayload> Headers { get; set; }

            public bool SkipServerCertificateValidation { get; set; }

            public bool ForceTls12 { get; set; }

            public int TimeoutMs { get; set; }
        }

        internal sealed class ProxyHttpResponsePayload
        {
            public int StatusCode { get; set; }

            public string ContentType { get; set; }

            public string Content { get; set; }

            public List<ProxyHeaderPayload> Headers { get; set; }

            public string ResponseUri { get; set; }

            public string ErrorMessage { get; set; }

            public bool IsSuccessful { get; set; }
        }

        internal sealed class ProxyHeaderPayload
        {
            public string Name { get; set; }

            public string Value { get; set; }
        }
    }
}

using System.IO;
using Newtonsoft.Json;
using RestSharp.Serialization.Json;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace MdlpApiClient.Serialization
{
    using System;
    using RestSharp;
    using RestSharp.Serialization;

    /// <summary>
    /// ServiceStack.Text.v4.0.33-based serializer.
    /// </summary>
    internal class ServiceStackSerializer : IRestSerializer
    {
        static ServiceStackSerializer()
        {
            // // use custom serialization only for our own types
            // JsConfig<CustomDate>.SerializeFn = c => c;
            // JsConfig<CustomDate>.DeSerializeFn = s => CustomDate.Parse(s);
            // JsConfig<CustomDateTime>.SerializeFn = c => c;
            // JsConfig<CustomDateTime>.DeSerializeFn = s => CustomDateTime.Parse(s);
            // JsConfig<CustomDateTimeSpace>.SerializeFn = c => c;
            // JsConfig<CustomDateTimeSpace>.DeSerializeFn = s => CustomDateTimeSpace.Parse(s);
        }

        public string[] SupportedContentTypes
        {
            get
            {
                return new[]
                {
                    "application/json", "text/json", "text/x-json", "text/javascript", "*+json"
                };
            }
        }

        public DataFormat DataFormat
        {
            get { return DataFormat.Json; }
        }

        private string contentType = "application/json";

        public string ContentType
        {
            get { return contentType; }
            set { contentType = value; }
        }

        internal T Deserialize<T>(string content)
        {
            var js = JsonSerializer.CreateDefault();
            var tr = new StringReader(content);

            return js.Deserialize<T>(new JsonTextReader(tr));
        }

        public T Deserialize<T>(IRestResponse response)
        {
            return Deserialize<T>(response.Content);
        }

        public string Serialize(Parameter bodyParameter)
        {
            return Serialize(bodyParameter.Value);
        }

        public string Serialize(object obj)
        {
            var js = JsonSerializer.CreateDefault();
            js.DateFormatHandling = DateFormatHandling.IsoDateFormat;
            js.DateFormatString = "yyyy-MM-ddTHH\\:mm\\:ss.fffffffzzz";

            return js.Serialize(obj);
        }
    }


    public static class JsonNetHelper
    {
        public static string Serialize(this JsonSerializer s, object value)
        {
            var tw = new StringWriter();

            s.Serialize(tw, value);

            return tw.GetStringBuilder().ToString();
        }
    }
}
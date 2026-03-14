using System.Collections.Generic;
using MdlpApiClient.DataContracts;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
        private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffzzz";

        private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

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

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var resolver = new DefaultJsonTypeInfoResolver();
            resolver.Modifiers.Add(ApplyDataContractConventions);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                TypeInfoResolver = resolver,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            options.Converters.Add(new FlexibleDateTimeConverter(DateTimeFormat));
            options.Converters.Add(new CustomDateConverter());
            options.Converters.Add(new CustomDateTimeConverter());
            options.Converters.Add(new CustomDateTimeSpaceConverter());
            options.Converters.Add(new FlexibleBoolConverter());
            options.Converters.Add(new FlexibleIntConverter());

            return options;
        }

        private static void ApplyDataContractConventions(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }

            var hasDataContract = typeInfo.Type.GetCustomAttribute<DataContractAttribute>() != null;
            var hiddenProperties = new List<JsonPropertyInfo>();

            foreach (var property in typeInfo.Properties)
            {
                var member = property.AttributeProvider as MemberInfo;
                if (member == null)
                {
                    continue;
                }

                if (member.GetCustomAttribute<IgnoreDataMemberAttribute>() != null)
                {
                    hiddenProperties.Add(property);
                    continue;
                }

                var dataMember = member.GetCustomAttribute<DataMemberAttribute>();
                if (dataMember != null)
                {
                    if (!string.IsNullOrWhiteSpace(dataMember.Name))
                    {
                        property.Name = dataMember.Name;
                    }

                    continue;
                }

                if (hasDataContract)
                {
                    hiddenProperties.Add(property);
                }
            }

            foreach (var property in hiddenProperties)
            {
                typeInfo.Properties.Remove(property);
            }
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
            if (string.IsNullOrWhiteSpace(content))
            {
                return default(T);
            }

            return JsonSerializer.Deserialize<T>(content, SerializerOptions);
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
            return JsonSerializer.Serialize(obj, SerializerOptions);
        }
    }

    internal sealed class CustomDateConverter : JsonConverter<CustomDate>
    {
        public override CustomDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return new CustomDate();
            if (reader.TokenType == JsonTokenType.String) return CustomDate.Parse(reader.GetString());
            return new CustomDate();
        }

        public override void Write(Utf8JsonWriter writer, CustomDate value, JsonSerializerOptions options)
        {
            if (value == null || !value.DateTime.HasValue) writer.WriteNullValue();
            else writer.WriteStringValue(value.ToString());
        }
    }

    internal sealed class CustomDateTimeConverter : JsonConverter<CustomDateTime>
    {
        public override CustomDateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return new CustomDateTime();
            if (reader.TokenType == JsonTokenType.String) return CustomDateTime.Parse(reader.GetString());
            return new CustomDateTime();
        }

        public override void Write(Utf8JsonWriter writer, CustomDateTime value, JsonSerializerOptions options)
        {
            if (value == null || !value.DateTime.HasValue) writer.WriteNullValue();
            else writer.WriteStringValue(value.ToString());
        }
    }

    internal sealed class CustomDateTimeSpaceConverter : JsonConverter<CustomDateTimeSpace>
    {
        public override CustomDateTimeSpace Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return new CustomDateTimeSpace();
            if (reader.TokenType == JsonTokenType.String) return CustomDateTimeSpace.Parse(reader.GetString());
            return new CustomDateTimeSpace();
        }

        public override void Write(Utf8JsonWriter writer, CustomDateTimeSpace value, JsonSerializerOptions options)
        {
            if (value == null || !value.DateTime.HasValue) writer.WriteNullValue();
            else writer.WriteStringValue(value.ToString());
        }
    }

    internal sealed class FlexibleBoolConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.True) return true;
            if (reader.TokenType == JsonTokenType.False) return false;
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (bool.TryParse(s, out var b)) return b;
                if (s == "1") return true;
                if (s == "0") return false;
            }
            if (reader.TokenType == JsonTokenType.Number) return reader.GetInt32() != 0;
            return false;
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
            writer.WriteBooleanValue(value);
    }

    internal sealed class FlexibleIntConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number) return reader.GetInt32();
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (int.TryParse(s, out var i)) return i;
            }
            return 0;
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value);
    }

    internal sealed class FlexibleDateTimeConverter : JsonConverter<DateTime>
    {
        private readonly string format;

        public FlexibleDateTimeConverter(string format)
        {
            this.format = format;
        }

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Handle Unix timestamp in milliseconds returned as a JSON number
            if (reader.TokenType == JsonTokenType.Number)
            {
                var ms = reader.GetInt64();
                return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("DateTime value must be a JSON string.");
            }

            var raw = reader.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return default(DateTime);
            }

            DateTime dateTime;
            if (DateTime.TryParseExact(
                raw,
                new[]
                {
                    "yyyy-MM-ddTHH:mm:ss.fffffffzzz",
                    "yyyy-MM-ddTHH:mm:ss.fffffffK",
                    "yyyy-MM-ddTHH:mm:ss.fffK",
                    "yyyy-MM-ddTHH:mm:ssK",
                    "yyyy-MM-ddTHH:mm:ss",
                    "yyyy-MM-dd HH:mm:ss",
                    "yyyy-MM-dd",
                    "dd-MM-yyyy HH:mm:ss",
                    "dd-MM-yyyy",
                },
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out dateTime))
            {
                return dateTime;
            }

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dateTime))
            {
                return dateTime;
            }

            throw new JsonException("Failed to parse DateTime value: " + raw);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            var normalized = value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Local)
                : value;

            writer.WriteStringValue(normalized.ToString(format, CultureInfo.InvariantCulture));
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;

namespace Bloomn
{
    public class BloomFilterState
    {
        public string ApiVersion { get; set; } = "v1";

        public BloomFilterParameters? Parameters { get; set; }

        public List<byte[]>? BitArrays { get; set; }

        public List<BloomFilterState>? Children { get; set; }

        public long Count { get; set; }

        public string Serialize()
        {
            try
            {
                return System.Text.Json.JsonSerializer.Serialize(this, typeof(BloomFilterState), JsonSerializerOptions);
            }
            catch (Exception ex)
            {
                throw new BloomFilterException(BloomFilterExceptionCode.InvalidSerializedState, "Could not serialize state.", ex);
            }
        }

        public static BloomFilterState Deserialize(string serialized)
        {
            BloomFilterState? state;
            try
            {
                state = System.Text.Json.JsonSerializer.Deserialize<BloomFilterState>(serialized, JsonSerializerOptions);
            }
            catch (Exception ex)
            {
                throw new BloomFilterException(BloomFilterExceptionCode.InvalidSerializedState, "Could not deserialize state.", ex);
            }

            if (state == null)
            {
                throw new BloomFilterException(BloomFilterExceptionCode.InvalidSerializedState, "Deserialization returned null.");
            }

            return state;
        }

        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = {new StateJsonSerializer()}
        };

        private class StateJsonSerializer : System.Text.Json.Serialization.JsonConverter<BloomFilterState>
        {
            public override BloomFilterState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var result = new BloomFilterState();
                reader.Read();

                for (; reader.TokenType != JsonTokenType.EndObject; reader.Read())
                {
                    var propertyName = reader.GetString();
                    reader.Read();
                    switch (propertyName)
                    {
                        case "count":
                            result.Count = reader.GetInt64();
                            break;
                        case "parameters":
                            result.Parameters = JsonSerializer.Deserialize<BloomFilterParameters>(ref reader, options);
                            break;
                        case "bits":
                            reader.Read();
                            result.BitArrays = new List<byte[]>();
                            for (; reader.TokenType != JsonTokenType.EndArray; reader.Read())
                            {
                                var bits = reader.GetBytesFromBase64();
                                result.BitArrays.Add(bits);
                            }

                            break;
                        case "children":
                            result.Children = JsonSerializer.Deserialize<List<BloomFilterState>>(ref reader, options);
                            break;
                    }
                }

                return result;
            }

            public override void Write(Utf8JsonWriter writer, BloomFilterState value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteString("apiVersion", value.ApiVersion);
                writer.WriteNumber("count", value.Count);
                writer.WritePropertyName("parameters");
                var parameters = value.Parameters ?? new BloomFilterParameters("unknown");

                JsonSerializer.Serialize(writer, parameters, options);

                if (value.BitArrays != null && value.BitArrays.Any())
                {
                    writer.WritePropertyName("bits");
                    writer.WriteStartArray();
                    foreach (var bitArray in value.BitArrays)
                    {
                        writer.WriteBase64StringValue(bitArray);
                    }

                    writer.WriteEndArray();
                }

                if (value.Children != null && value.Children.Any())
                {
                    writer.WritePropertyName("children");
                    writer.WriteStartArray();
                    foreach (var child in value.Children)
                    {
                        JsonSerializer.Serialize(writer, child, options);
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }
        }
    }
}
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
        public string? Id { get; set; }

        public string ApiVersion { get; set; } = "v1";

        public BloomFilterParameters? Parameters { get; set; }

        public List<byte[]>? BitArrays { get; set; }

        public List<BloomFilterState>? Children { get; set; }

        public long Count { get; set; }

        public string Serialize()
        {
            return System.Text.Json.JsonSerializer.Serialize(this, typeof(BloomFilterState), JsonSerializerOptions);
        }

        public static BloomFilterState Deserialize(string serialized)
        {
            var state = System.Text.Json.JsonSerializer.Deserialize<BloomFilterState>(serialized, JsonSerializerOptions);
            if (state == null)
            {
                throw new SerializationException("Deserialization returned null.");
            }

            return state;
        }

        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions()
        {
            Converters = { new StateJsonSerializer() }
        };

        private class StateJsonSerializer : System.Text.Json.Serialization.JsonConverter<BloomFilterState>
        {
            public override BloomFilterState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var result = new BloomFilterState();

                while (reader.Read())
                {
                    var propertyName = reader.GetString();
                    switch (propertyName)
                    {
                        case "id": 
                            result.Id = reader.GetString();
                            break;
                        case "count":
                            result.Count = reader.GetInt64();
                            break;
                        case "parameters":
                            result.Parameters = JsonSerializer.Deserialize<BloomFilterParameters>(ref reader, options);
                            break;
                        case "bits":
                            reader.Read();
                            result.BitArrays = new List<byte[]>();
                            while (reader.TokenType != JsonTokenType.EndArray)
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
                writer.WriteString("id", value.Id);
                writer.WriteString("apiVersion", value.ApiVersion);
                writer.WriteNumber("count", value.Count);
                writer.WritePropertyName("parameters");
                var parameters = value.Parameters ?? new BloomFilterParameters(value.Id ?? "unknown");

                JsonSerializer.Serialize(writer, parameters);

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
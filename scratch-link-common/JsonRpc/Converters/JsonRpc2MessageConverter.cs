// <copyright file="JsonRpc2MessageConverter.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.JsonRpc.Converters;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Converter for deserializing JSON-RPC Request, Notification, and Response objects.
/// This converter will inspect the incoming message to determine which kind of object it is, then delegate to deserializing that kind of object.
/// </summary>
internal class JsonRpc2MessageConverter : JsonConverter<JsonRpc2Message>
{
    /// <summary>
    /// Reads and converts the JSON to a specific kind of message (Request, Notification, or Response).
    /// </summary>
    /// <inheritdoc/>
    public override JsonRpc2Message Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var discriminatorReader = reader;
        var discriminator = JsonSerializer.Deserialize<MessageTypeDiscriminator>(ref discriminatorReader);

        var guessedType = discriminator.GuessMessageType();
        if (guessedType == null)
        {
            throw new JsonException("could not identify JSON-RPC message type");
        }

        // WARNING: implementing CanConvert() too permissively in this class can turn this Deserialize call into infinite recursion!
        return (JsonRpc2Message)JsonSerializer.Deserialize(ref reader, guessedType, options);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, JsonRpc2Message value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, options);

    private class MessageTypeDiscriminator : JsonRpc2Message
    {
        [JsonPropertyName("error")]
        public JsonRpc2Error Error { get; set; }

        public Type GuessMessageType()
        {
            if (this.Error != null || this.ExtraProperties.ContainsKey("result"))
            {
                return typeof(JsonRpc2Response);
            }

            if (this.ExtraProperties.ContainsKey("method"))
            {
                return typeof(JsonRpc2Request);
            }

            return null;
        }
    }
}

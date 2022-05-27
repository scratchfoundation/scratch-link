// <copyright file="EncodingHelpers.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using System.Text;
using System.Text.Json;
using ScratchLink.JsonRpc;

/// <summary>
/// Helpers for interacting with Scratch Link's message buffers.
/// A Scratch Link message buffer has a <c>message</c> property and optionally an <c>encoding</c> property.
/// If the encoding property is missing, null, or empty, the message is a Unicode string.
/// If the encoding property is "base64" then the message is a string in Base64 format.
/// No other encodings are supported at this time.
/// </summary>
public static class EncodingHelpers
{
    /// <summary>
    /// Decode the "message" property of <paramref name="jsonBuffer"/> into bytes.
    /// If <paramref name="jsonBuffer"/> has an "encoding" property, use that encoding.
    /// Otherwise, assume the message is Unicode text.
    /// </summary>
    /// <param name="jsonBuffer">A JSON object containing a "message" property and optionally an "encoding" property.</param>
    /// <returns>An array of bytes containing the decoded data.</returns>
    /// <exception cref="JsonRpc2Exception">Thrown if the "message" property is missing or the message could not be decoded.</exception>
    public static byte[] DecodeBuffer(JsonElement jsonBuffer)
    {
        if (!jsonBuffer.TryGetProperty("message", out var jsonMessage) || jsonMessage.ValueKind != JsonValueKind.String)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("missing or invalid 'message' property"));
        }

        jsonBuffer.TryGetProperty("encoding", out var encoding);

        if (encoding.ValueEquals("base64"))
        {
            if (jsonMessage.TryGetBytesFromBase64(out var messageBytes))
            {
                return messageBytes;
            }

            throw new JsonRpc2Exception(JsonRpc2Error.ParseError("failed to parse base64 message"));
        }
        else if (encoding.ValueKind == JsonValueKind.Undefined || encoding.ValueKind == JsonValueKind.Null)
        {
            // message is a Unicode string with no additional encoding
            return Encoding.UTF8.GetBytes(jsonMessage.GetString());
        }

        throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams($"unsupported encoding: {encoding}"));
    }

    /// <summary>
    /// Encode <paramref name="data"/> using <paramref name="encoding"/> and return the result as a string.
    /// </summary>
    /// <param name="data">The bytes to encode.</param>
    /// <param name="encoding">The encoding format, or <see cref="null"/> to "encode" UTF-8 data as a Unicode string.</param>
    /// <returns>A string containing the encoded data.</returns>
    /// <exception cref="JsonRpc2Exception">Thrown if the data could not be encoded, including if the encoding is not supported.</exception>
    public static string EncodeBuffer(byte[] data, string encoding)
    {
        switch (encoding)
        {
            case "base64":
                return Convert.ToBase64String(data);
            case null:
                return Encoding.UTF8.GetString(data);
            default:
                throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams($"unsupported encoding: {encoding}"));
        }
    }
}

// <copyright file="JsonRpc2ValueConverter.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.JsonRpc.Converters;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// JSON value converter geared toward JSON-RPC 2.0 usage.
/// Attempts to convert values to the simplest / smallest types possible.
/// Inspired by "ObjectToInferredTypesConverter" from the .NET documentation.
/// </summary>
internal class JsonRpc2ValueConverter : JsonConverter<object>
{
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);

    /// <inheritdoc/>
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.True => true,
        JsonTokenType.False => false,
        JsonTokenType.Null => null,
        JsonTokenType.Number when reader.TryGetSByte(out sbyte x) => x,
        JsonTokenType.Number when reader.TryGetByte(out byte x) => x,
        JsonTokenType.Number when reader.TryGetInt16(out short x) => x,
        JsonTokenType.Number when reader.TryGetUInt16(out ushort x) => x,
        JsonTokenType.Number when reader.TryGetInt32(out int x) => x,
        JsonTokenType.Number when reader.TryGetUInt32(out uint x) => x,
        JsonTokenType.Number when reader.TryGetInt64(out long x) => x,
        JsonTokenType.Number when reader.TryGetUInt64(out ulong x) => x,
        JsonTokenType.Number when reader.TryGetSingle(out float x) => x,
        JsonTokenType.Number when reader.TryGetDouble(out double x) => x,
        JsonTokenType.Number when reader.TryGetDecimal(out decimal x) => x,
        JsonTokenType.String when reader.TryGetGuid(out Guid x) => x,
        JsonTokenType.String when reader.TryGetBytesFromBase64(out byte[] x) => x,
        JsonTokenType.String => reader.GetString() !,
        _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
    };
}

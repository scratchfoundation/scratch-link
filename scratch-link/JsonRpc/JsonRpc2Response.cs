// <copyright file="JsonRpc2Response.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.JsonRpc;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Data class representing a JSON-RPC 2.0 Response object.
/// Either "result" or "error" should be filled, not both.
/// </summary>
internal class JsonRpc2Response
{
    /// <summary>
    /// Gets the JSON RPC version string (always "2.0").
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    [JsonPropertyOrder(-100)]
    public string JsonRPC { get; } = "2.0";

    /// <summary>
    /// Gets or sets the successful result of the corresponding Request.
    /// This is REQUIRED on success and MUST NOT exist if there was an error.
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object Result { get; set; }

    /// <summary>
    /// Gets or sets an object describing an error triggered by the corresponding Request.
    /// This is REQUIRED on error and MUST NOT exist if there was no error.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpc2Error Error { get; set; }

    /// <summary>
    /// Gets or sets the response ID, which must match the ID of the corresponding Request. May be a string or integer.
    /// </summary>
    [JsonPropertyName("id")]
    public JsonElement Id { get; set; }
}

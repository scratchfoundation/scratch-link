// <copyright file="JsonRpc2Response.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.JsonRpc;

using System.Text.Json.Serialization;

/// <summary>
/// Data class representing a JSON-RPC 2.0 Response object.
/// Either "result" or "error" should be filled, not both.
/// </summary>
internal class JsonRpc2Response : JsonRpc2Message
{
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
    /// Gets a value indicating whether or not this is a valid JSON-RPC Request object.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public override bool IsValid => base.IsValid && ((this.Result != null) || (this.Error != null));
}

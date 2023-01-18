// <copyright file="JsonRpc2Request.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.JsonRpc;

using System.Text.Json.Serialization;

/// <summary>
/// Data class representing a JSON-RPC 2.0 Request object.
/// If the "id" property is null, this is a Notification object.
/// </summary>
internal class JsonRpc2Request : JsonRpc2Message
{
    /// <summary>
    /// Gets or sets the name of the method being called.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets the parameters being passed. May be an Array (positional parameters), an Object (named parameters), or absent.
    /// </summary>
    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object Params { get; set; }

    /// <summary>
    /// Gets a value indicating whether or not this is a valid JSON-RPC Request object.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public override bool IsValid => base.IsValid && !string.IsNullOrEmpty(this.Method);
}

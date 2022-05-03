// <copyright file="JsonRpc2Message.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.JsonRpc;

using System.Text.Json.Serialization;

/// <summary>
/// Base class for JSON-RPC 2.0 root message objects: Request, Notification, and Response.
/// A Request object must have a non-null "Method" and null "Result" and "Error".
/// A Notification object is a Request with a null "Id" property.
/// A Response object must have a non-null "Id" and either "Result" or "Error" (but not both).
/// </summary>
internal class JsonRpc2Message
{
    /// <summary>
    /// The JSON-RPC version supported ("2.0").
    /// </summary>
    public const string JsonRpcVersion = "2.0";

    /// <summary>
    /// Gets the JSON RPC version string.
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    [JsonPropertyOrder(-100)]
    public string JsonRPC { get; init; } = JsonRpcVersion;

    /// <summary>
    /// Gets or sets the request / response ID.
    /// May be a string or integer for a Request or Response object.
    /// May be null for a Notification object.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object Id { get; set; }

    /// <summary>
    /// Gets or sets a dictionary to contain additional properties not part of the static C# type.
    /// </summary>
    [JsonExtensionData]
    [JsonPropertyOrder(100)]
    public Dictionary<string, object> ExtraProperties { get; set; }

    /// <summary>
    /// Gets a value indicating whether or not this is a valid JSON-RPC message.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public virtual bool IsValid => this.JsonRPC == JsonRpcVersion;
}

// <copyright file="Request.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.JsonRpc;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

/// <summary>
/// Data class representing a JSON-RPC 2.0 Request object.
/// </summary>
internal class Request
{
    /// <summary>
    /// Gets the JSON RPC version string (always "2.0").
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRPC { get; private set; }

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
    public JsonElement? Params { get; set; }

    /// <summary>
    /// Gets or sets the request ID. May be a string, integer, or absent.
    /// If null, this is a notification instead of a request.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Id { get; set; }
}

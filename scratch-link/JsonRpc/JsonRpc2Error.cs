// <copyright file="JsonRpc2Error.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.JsonRpc;

using System.Text.Json.Serialization;

/// <summary>
/// Data class representing a JSON-RPC 2.0 Error object.
/// </summary>
internal class JsonRpc2Error
{
    /// <summary>
    /// Gets or sets the numeric error code for this error.
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// Gets or sets a string providing a short description of the error.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets an optional value containing additional information about the error.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object Data { get; set; }

    /// <summary>
    /// Creates an Error object representing a Parse Error.
    /// </summary>
    /// <param name="data">An optional value containing additional information about the error.</param>
    /// <returns>A new Error object.</returns>
    public static JsonRpc2Error ParseError(object data = null)
    {
        return new JsonRpc2Error { Code = -32700, Message = "Parse Error", Data = data };
    }

    /// <summary>
    /// Creates an Error object representing an Invalid Request error.
    /// </summary>
    /// <param name="data">An optional value containing additional information about the error.</param>
    /// <returns>A new Error object.</returns>
    public static JsonRpc2Error InvalidRequest(object data = null)
    {
        return new JsonRpc2Error { Code = -32600, Message = "Invalid Request", Data = data };
    }

    /// <summary>
    /// Creates an Error object representing a Method Not Found error.
    /// </summary>
    /// <param name="data">An optional value containing additional information about the error.</param>
    /// <returns>A new Error object.</returns>
    public static JsonRpc2Error MethodNotFound(object data = null)
    {
        return new JsonRpc2Error { Code = -32601, Message = "Method Not Found", Data = data };
    }

    /// <summary>
    /// Creates an Error object representing an Invalid Params error.
    /// </summary>
    /// <param name="data">An optional value containing additional information about the error.</param>
    /// <returns>A new Error object.</returns>
    public static JsonRpc2Error InvalidParams(object data = null)
    {
        return new JsonRpc2Error { Code = -32602, Message = "Invalid Params", Data = data };
    }

    /// <summary>
    /// Creates an Error object representing an Internal Error.
    /// </summary>
    /// <param name="data">An optional value containing additional information about the error.</param>
    /// <returns>A new Error object.</returns>
    public static JsonRpc2Error InternalError(object data = null)
    {
        return new JsonRpc2Error { Code = -32603, Message = "Internal Error", Data = data };
    }

    /// <summary>
    /// Creates an Error object representing a Server Error.
    /// </summary>
    /// <param name="code">A numeric code for this error. Should be between -32000 and -32099, inclusive.</param>
    /// <param name="data">An optional value containing additional information about the error.</param>
    /// <returns>A new Error object.</returns>
    public static JsonRpc2Error ServerError(int code, object data = null)
    {
        return new JsonRpc2Error { Code = code, Message = "Server Error", Data = data };
    }

    /// <summary>
    /// Creates an Error object representing an Application Error.
    /// </summary>
    /// <param name="data">An optional value containing additional information about the error.</param>
    /// <returns>A new Error object.</returns>
    public static JsonRpc2Error ApplicationError(object data = null)
    {
        return new JsonRpc2Error { Code = -32500, Message = "Application Error", Data = data };
    }
}

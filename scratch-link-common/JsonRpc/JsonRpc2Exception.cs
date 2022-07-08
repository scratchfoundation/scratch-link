// <copyright file="JsonRpc2Exception.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.JsonRpc;

using System;

/// <summary>
/// Exception class to hold a JSON-RPC 2.0 error.
/// </summary>
internal class JsonRpc2Exception : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpc2Exception"/> class to report a <see cref="JsonRpc2Error"/>.
    /// </summary>
    /// <param name="error">The JSON-RPC error object to report.</param>
    public JsonRpc2Exception(JsonRpc2Error error)
    {
        this.Error = error;
    }

    /// <summary>
    /// Gets or sets the <see cref="JsonRpc2Error"/> object associated with the thrown error.
    /// </summary>
    public JsonRpc2Error Error { get; set; }
}

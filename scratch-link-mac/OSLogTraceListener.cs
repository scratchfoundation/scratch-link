// <copyright file="OSLogTraceListener.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac;

using System;
using System.Diagnostics;
using CoreFoundation;

/// <summary>
/// Implements a <see cref="TraceListener"/> to <see cref="OSLog"/> adapter.
/// </summary>
public class OSLogTraceListener : TraceListener
{
    /// <inheritdoc/>
    public override void Write(string message)
    {
        OSLog.Default.Log(message);
    }

    /// <inheritdoc/>
    public override void WriteLine(string message)
    {
        OSLog.Default.Log(message);
    }
}

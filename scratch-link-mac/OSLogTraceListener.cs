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
    private const string LogSubsystem = "org.scratch.link";
    private const string LogCategory = "app";

    private readonly OSLog log;

    /// <summary>
    /// Initializes a new instance of the <see cref="OSLogTraceListener"/> class.
    /// </summary>
    public OSLogTraceListener()
    {
        this.log = new OSLog(LogSubsystem, LogCategory);
    }

    /// <inheritdoc/>
    public override void Write(string message)
    {
        this.log.Log(message);
    }

    /// <inheritdoc/>
    public override void WriteLine(string message)
    {
        this.log.Log(message);
    }
}

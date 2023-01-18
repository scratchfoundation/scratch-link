// <copyright file="RfcommChannelOpenCompleteEventArgs.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac.BT.Rfcomm;

using System;

/// <summary>
/// Event args for when opening an RFCOMM channel has completed (succeeded or failed).
/// </summary>
public class RfcommChannelOpenCompleteEventArgs : RfcommChannelEventArgs
{
    /// <summary>
    /// Gets or sets the error encountered while attempting to open the RFCOMM channel, if any.
    /// </summary>
    public IOReturn Error { get; set; }
}

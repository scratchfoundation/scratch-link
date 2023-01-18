// <copyright file="RfcommChannelEventArgs.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac.BT.Rfcomm;

using System;
using IOBluetooth;

/// <summary>
/// Generic event args for an RFCOMM channel event.
/// </summary>
public class RfcommChannelEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the channel associated with the event.
    /// </summary>
    public RfcommChannel Channel { get; set; }
}

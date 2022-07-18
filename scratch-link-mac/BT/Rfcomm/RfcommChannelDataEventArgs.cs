// <copyright file="RfcommChannelDataEventArgs.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac.BT.Rfcomm;

using System;

/// <summary>
/// Event args for receiving data through an RFCOMM channel.
/// </summary>
public class RfcommChannelDataEventArgs : RfcommChannelEventArgs
{
    /// <summary>
    /// Gets or sets the data received through the RFCOMM channel.
    /// </summary>
    public byte[] Data { get; set; }
}

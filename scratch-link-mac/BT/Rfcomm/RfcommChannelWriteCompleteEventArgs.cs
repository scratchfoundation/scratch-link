// <copyright file="RfcommChannelWriteCompleteEventArgs.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac.BT.Rfcomm;

using System;

/// <summary>
/// Event args for the completion of an RFCOMM write.
/// </summary>
public class RfcommChannelWriteCompleteEventArgs : RfcommChannelEventArgs
{
    /// <summary>
    /// Gets or sets the reference constant.
    /// </summary>
    public IntPtr RefCon { get; set; }

    /// <summary>
    /// Gets or sets an <c>IOReturn</c> value indicating whether an error occurred during the write.
    /// </summary>
    public IOReturn Error { get; set; }
}

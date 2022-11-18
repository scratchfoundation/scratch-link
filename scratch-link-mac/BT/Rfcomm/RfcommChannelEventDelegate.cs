// <copyright file="RfcommChannelEventDelegate.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac.BT.Rfcomm;

using System;
using IOBluetooth;
using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Converts <see cref="RfcommChannelDelegate"/> callbacks into events.
/// </summary>
public class RfcommChannelEventDelegate : RfcommChannelDelegate
{
    /// <summary>
    /// Event triggered when the RFCOMM channel has closed.
    /// </summary>
    public event EventHandler<RfcommChannelEventArgs> RfcommChannelClosedEvent;

    /// <summary>
    /// Event triggered when the RFCOMM channel's control signals have changed.
    /// </summary>
    public event EventHandler<RfcommChannelEventArgs> RfcommChannelControlSignalsChangedEvent;

    /// <summary>
    /// Event triggered when the RFCOMM channel has received data.
    /// </summary>
    public event EventHandler<RfcommChannelDataEventArgs> RfcommChannelDataEvent;

    /// <summary>
    /// Event triggered when the RFCOMM channel's flow control has changed.
    /// </summary>
    public event EventHandler<RfcommChannelEventArgs> RfcommChannelFlowControlChangedEvent;

    /// <summary>
    /// Event triggered when opening the RFCOMM has completed (succeeded or failed).
    /// </summary>
    public event EventHandler<RfcommChannelOpenCompleteEventArgs> RfcommChannelOpenCompleteEvent;

    /// <summary>
    /// Event triggered when there is space in the RFCOMM channel's queue.
    /// </summary>
    public event EventHandler<RfcommChannelEventArgs> RfcommChannelQueueSpaceAvailableEvent;

    /// <summary>
    /// Event triggered when a write to the RFCOMM channel has completed.
    /// </summary>
    public event EventHandler<RfcommChannelWriteCompleteEventArgs> RfcommChannelWriteCompleteEvent;

    /// <summary>
    /// Callback triggered when the RFCOMM channel has closed.
    /// </summary>
    /// <param name="rfcommChannel">The RFCOMM channel for which this callback is being called.</param>
    public override void RfcommChannelClosed(RfcommChannel rfcommChannel)
    {
        if (this.RfcommChannelClosedEvent == null)
        {
            return;
        }

        this.RfcommChannelClosedEvent.Invoke(rfcommChannel, new RfcommChannelEventArgs { Channel = rfcommChannel });
    }

    /// <summary>
    /// Callback triggered when the RFCOMM channel's control signals have changed.
    /// </summary>
    /// <param name="rfcommChannel">The RFCOMM channel for which this callback is being called.</param>
    public override void RfcommChannelControlSignalsChanged(RfcommChannel rfcommChannel)
    {
        if (this.RfcommChannelControlSignalsChangedEvent == null)
        {
            return;
        }

        this.RfcommChannelControlSignalsChangedEvent.Invoke(rfcommChannel, new RfcommChannelEventArgs { Channel = rfcommChannel });
    }

    /// <summary>
    /// Callback triggered when the RFCOMM channel has received data.
    /// </summary>
    /// <param name="rfcommChannel">The RFCOMM channel for which this callback is being called.</param>
    /// <param name="dataPointer">A pointer to the data received.</param>
    /// <param name="dataLength">The number of bytes of data received.</param>
    public override void RfcommChannelData(RfcommChannel rfcommChannel, IntPtr dataPointer, nuint dataLength)
    {
        if (this.RfcommChannelDataEvent == null)
        {
            return;
        }

        var output = new byte[dataLength];
        Marshal.Copy(dataPointer, output, 0, (int)dataLength);
        this.RfcommChannelDataEvent.Invoke(rfcommChannel, new RfcommChannelDataEventArgs
        {
            Channel = rfcommChannel,
            Data = output,
        });
    }

    /// <summary>
    /// Callback triggered when the RFCOMM channel's flow control has changed.
    /// </summary>
    /// <param name="rfcommChannel">The RFCOMM channel for which this callback is being called.</param>
    public override void RfcommChannelFlowControlChanged(RfcommChannel rfcommChannel)
    {
        if (this.RfcommChannelFlowControlChangedEvent == null)
        {
            return;
        }

        this.RfcommChannelFlowControlChangedEvent.Invoke(rfcommChannel, new RfcommChannelEventArgs { Channel = rfcommChannel });
    }

    /// <summary>
    /// Callback triggered when opening the RFCOMM has completed (succeeded or failed).
    /// </summary>
    /// <param name="rfcommChannel">The RFCOMM channel for which this callback is being called.</param>
    /// <param name="error">An <c>IOReturn</c> whether opening the channel succeeded or failed.</param>
    public override void RfcommChannelOpenComplete(RfcommChannel rfcommChannel, int error)
    {
        if (this.RfcommChannelOpenCompleteEvent == null)
        {
            return;
        }

        this.RfcommChannelOpenCompleteEvent.Invoke(rfcommChannel, new RfcommChannelOpenCompleteEventArgs
        {
            Channel = rfcommChannel,
            Error = (IOReturn)error,
        });
    }

    /// <summary>
    /// Callback triggered when there is space in the RFCOMM channel's queue.
    /// </summary>
    /// <param name="rfcommChannel">The RFCOMM channel for which this callback is being called.</param>
    public override void RfcommChannelQueueSpaceAvailable(RfcommChannel rfcommChannel)
    {
        if (this.RfcommChannelQueueSpaceAvailableEvent == null)
        {
            return;
        }

        this.RfcommChannelQueueSpaceAvailableEvent.Invoke(rfcommChannel, new RfcommChannelEventArgs { Channel = rfcommChannel });
    }

    /// <summary>
    /// Callback triggered when a write to the RFCOMM channel has completed.
    /// </summary>
    /// <param name="rfcommChannel">The RFCOMM channel for which this callback is being called.</param>
    /// <param name="refcon">The "reference constant" passed when initiating this write.</param>
    /// <param name="error">The error encountered during this write, if any.</param>
    public override void RfcommChannelWriteComplete(RfcommChannel rfcommChannel, IntPtr refcon, int error)
    {
        if (this.RfcommChannelWriteCompleteEvent == null)
        {
            return;
        }

        this.RfcommChannelWriteCompleteEvent.Invoke(rfcommChannel, new RfcommChannelWriteCompleteEventArgs
        {
            Channel = rfcommChannel,
            RefCon = refcon,
            Error = (IOReturn)error,
        });
    }
}

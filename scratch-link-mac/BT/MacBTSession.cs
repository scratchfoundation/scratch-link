// <copyright file="MacBTSession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac.BT;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CoreBluetooth;
using Fleck;
using Foundation;
using IOBluetooth;
using ScratchLink.BT;
using ScratchLink.Extensions;
using ScratchLink.JsonRpc;
using ScratchLink.Mac.BT.Rfcomm;

/// <summary>
/// Implements a BT session on MacOS.
/// </summary>
internal class MacBTSession : BTSession<BluetoothDevice, BluetoothDeviceAddress>
{
    private readonly DeviceInquiry inquiry = new ();
    private readonly SemaphoreSlim channelLock = new (1);

    private DeviceClassMajor searchClassMajor;
    private DeviceClassMinor searchClassMinor;

    private RfcommChannel connectedChannel;

    /// <summary>
    /// Initializes a new instance of the <see cref="MacBTSession"/> class.
    /// </summary>
    /// <param name="webSocket">The web socket.</param>
    public MacBTSession(IWebSocketConnection webSocket)
        : base(webSocket)
    {
        ObjCRuntime.Dlfcn.dlopen("/System.Library/Frameworks/IOBluetooth.framework/IOBluetooth", 0);

#if DEBUG
        this.inquiry.Completed += (o, e) => Debug.Print("Inquiry.Completed: {0} {1}", e.Aborted, e.Error);
        this.inquiry.DeviceFound += (o, e) => Debug.Print("Inquiry.DeviceFound: {0}", e.Device);
        this.inquiry.DeviceInquiryStarted += (o, e) => Debug.Print("Inquiry.Started");
        this.inquiry.DeviceNameUpdated += (o, e) => Debug.Print("Inquiry.DeviceNameUpdated: {0} {1}", e.DevicesRemaining, e.Device);
        this.inquiry.UpdatingDeviceNamesStarted += (o, e) => Debug.Print("Inquiry.UpdatingDeviceNamesStarted: {0}", e.DevicesRemaining);
#endif

        this.inquiry.DeviceFound += this.WrapEventHandler<DeviceFoundEventArgs>(this.Inquiry_DeviceFoundAsync);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.DisposedValue)
        {
            if (this.connectedChannel != null)
            {
                this.connectedChannel.Dispose();
                this.connectedChannel = null;
            }

            this.inquiry.Stop();
            this.inquiry.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override Task<object> DoDiscover(byte majorDeviceClass, byte minorDeviceClass)
    {
        this.inquiry.Stop();
        this.inquiry.ClearFoundDevices();
        this.searchClassMajor = (DeviceClassMajor)majorDeviceClass;
        this.searchClassMinor = (DeviceClassMinor)minorDeviceClass;
        this.inquiry.SetSearchCriteria(ServiceClassMajor.Any, this.searchClassMajor, DeviceClassMinor.Any);
        this.inquiry.SearchType = DeviceSearchType.Classic;
        this.inquiry.InquiryLength = 20;
        this.inquiry.UpdateNewDeviceNames = true;
        var inquiryStatus = (IOReturn)this.inquiry.Start();
        if (inquiryStatus != IOReturn.Success)
        {
            Debug.Print("Failed to start inquiry: {0}", inquiryStatus.ToDebugString());
            throw JsonRpc2Error.ServerError(-32500, "Device inquiry failed to start").ToException();
        }

        return Task.FromResult<object>(null);
    }

    /// <inheritdoc/>
    protected override async Task<object> DoConnect(BluetoothDevice device)
    {
        this.inquiry.Stop();

        Debug.Print("Attempting to connect to BT device with address={0}", device.AddressString);

        var rfcommDelegate = new RfcommChannelEventDelegate();

#if DEBUG
        rfcommDelegate.RfcommChannelClosedEvent += (o, e) => Debug.Print("RfcommChannelClosedEvent on channel {0}", e.Channel.ChannelID);
        rfcommDelegate.RfcommChannelControlSignalsChangedEvent += (o, e) => Debug.Print("RfcommChannelControlSignalsChangedEvent on channel {0}", e.Channel.ChannelID);
        rfcommDelegate.RfcommChannelFlowControlChangedEvent += (o, e) => Debug.Print("RfcommChannelFlowControlChangedEvent on channel {0}", e.Channel.ChannelID);
        rfcommDelegate.RfcommChannelOpenCompleteEvent += (o, e) => Debug.Print("RfcommChannelOpenCompleteEvent on channel {0} with error={1}", e.Channel.ChannelID, e.Error);

        // These are especially noisy
        // rfcommDelegate.RfcommChannelDataEvent += (o, e) => Debug.Print("RfcommChannelDataEvent on channel {0} with length {1}", e.Channel.ChannelID, e.Data.Length);
        // rfcommDelegate.RfcommChannelQueueSpaceAvailableEvent += (o, e) => Debug.Print("RfcommChannelQueueSpaceAvailableEvent on channel {0}", e.Channel.ChannelID);
        // rfcommDelegate.RfcommChannelWriteCompleteEvent += (o, e) => Debug.Print("RfcommChannelWriteCompleteEvent on channel {0} with error={1}", e.Channel.ChannelID, e.Error);
#endif

        rfcommDelegate.RfcommChannelDataEvent += this.RfcommDelegate_RfcommChannelData;

        using (await this.channelLock.WaitDisposableAsync(DefaultLockTimeout))
        {
            var openChannelResult = await EventAwaiter<RfcommChannelOpenCompleteEventArgs>.MakeTask(
                h => rfcommDelegate.RfcommChannelOpenCompleteEvent += h,
                h => rfcommDelegate.RfcommChannelOpenCompleteEvent -= h,
                TimeSpan.FromSeconds(30),
                CancellationToken.None,
                () =>
                {
                    // OpenRfcommChannelSync sometimes returns "general error" even when the connection will succeed later.
                    // Ignore its return value and check for error status on the RfcommChannelOpenComplete event instead.
                    device.OpenRfcommChannelSync(out this.connectedChannel, 1, rfcommDelegate.Self);
                });

            if (openChannelResult.Error != IOReturn.Success)
            {
                Debug.Print("Opening RFCOMM channel failed: {0}", openChannelResult.Error.ToDebugString());
                throw JsonRpc2Error.ServerError(-32500, "Could not connect to RFCOMM channel.").ToException();
            }
        }

        // Connect is done already; don't wait for this run loop / session to complete.
        _ = Task.Run(async () =>
        {
            // run loop specifically for this device: necessary to get delegate callbacks
            // TODO: is it actually necessary with this new implementation?
            while (this.connectedChannel?.IsOpen ?? false)
            {
                await Task.Delay(500);
            }

            this.connectedChannel.Dispose();
            this.connectedChannel = null;

            await this.SendErrorNotification(JsonRpc2Error.ApplicationError("RFCOMM run loop exited"));

            this.EndSession();
        });

        return null;
    }

    /// <inheritdoc/>
    protected override async Task<int> DoSend(byte[] buffer)
    {
        ushort shortLength = (ushort)buffer.Length;
        if (shortLength != buffer.Length)
        {
            throw JsonRpc2Error.InvalidParams("buffer too big to send").ToException();
        }

        IOReturn writeResult;
        GCHandle pinnedBuffer = default(GCHandle);

        try
        {
            pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            using (await this.channelLock.WaitDisposableAsync(DefaultLockTimeout))
            {
                writeResult = (IOReturn)this.connectedChannel.WriteSync(pinnedBuffer.AddrOfPinnedObject(), shortLength);
            }
        }
        finally
        {
            pinnedBuffer.Free();
        }

        if (writeResult != IOReturn.Success)
        {
            Debug.Print("send error: {0}", writeResult.ToDebugString());
            throw JsonRpc2Error.InternalError("send encountered an error").ToException();
        }

        return shortLength;
    }

    private void RfcommDelegate_RfcommChannelData(object sender, RfcommChannelDataEventArgs e)
    {
        _ = this.DidReceiveMessage(e.Data);
    }

    private async void Inquiry_DeviceFoundAsync(object sender, DeviceFoundEventArgs e)
    {
        if (e.Device.DeviceClassMajor != this.searchClassMajor)
        {
            // major class doesn't match
            return;
        }

        // on some systems the minor class will show up as zero... macOS bug?
        if ((e.Device.DeviceClassMinor != this.searchClassMinor) &&
            (e.Device.DeviceClassMinor != 0))
        {
            // minor class doesn't match
            return;
        }

        await this.OnPeripheralDiscovered(e.Device, e.Device.Address, e.Device.NameOrAddress, e.Device.Rssi);
    }
}

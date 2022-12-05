// <copyright file="MacBTSession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac.BT;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AppKit;
using CoreFoundation;
using Fleck;
using Foundation;
using IOBluetooth;
using ScratchLink.BT;
using ScratchLink.Extensions;
using ScratchLink.JsonRpc;
using ScratchLink.Mac.BT.Rfcomm;
using ScratchLink.Mac.Extensions;

/// <summary>
/// Implements a BT session on MacOS.
/// </summary>
internal class MacBTSession : BTSession<BluetoothDevice, BluetoothDeviceAddress>
{
    private readonly DeviceInquiry inquiry = new ();
    private readonly DispatchQueue rfcommQueue = new DispatchQueue("RFCOMM dispatch queue for MacBT session");

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
        ObjCRuntime.Dlfcn.dlopen("/System/Library/Frameworks/IOBluetooth.framework/IOBluetooth", 0);

#if DEBUG
        this.inquiry.Completed += (o, e) => Trace.WriteLine($"Inquiry.Completed: Aborted={e.Aborted} Error={e.Error}");
        this.inquiry.DeviceFound += (o, e) => Trace.WriteLine($"Inquiry.DeviceFound: {e.Device}");
        this.inquiry.DeviceInquiryStarted += (o, e) => Trace.WriteLine("Inquiry.Started");
        this.inquiry.DeviceNameUpdated += (o, e) => Trace.WriteLine($"Inquiry.DeviceNameUpdated: Remaining={e.DevicesRemaining} Device={e.Device}");
        this.inquiry.UpdatingDeviceNamesStarted += (o, e) => Trace.WriteLine($"Inquiry.UpdatingDeviceNamesStarted: Remaining={e.DevicesRemaining}");
#endif

        this.inquiry.DeviceFound += this.WrapEventHandler<DeviceFoundEventArgs>(this.Inquiry_DeviceFoundAsync);
    }

    /// <inheritdoc/>
    protected override bool IsConnected => this.connectedChannel != null;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.DisposedValue)
        {
            if (this.connectedChannel != null)
            {
                var device = this.connectedChannel.Device;

                this.connectedChannel.Dispose();
                this.connectedChannel = null;

                Trace.WriteLine("disconnecting device");
                device.CloseConnection();
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
            Trace.WriteLine($"Failed to start inquiry: {inquiryStatus.ToDebugString()}");
            throw JsonRpc2Error.ServerError(-32500, "Device inquiry failed to start").ToException();
        }

        return Task.FromResult<object>(null);
    }

    /// <inheritdoc/>
    protected override async Task<object> DoConnect(BluetoothDevice device, string pinString)
    {
        this.inquiry.Stop();

        Trace.WriteLine($"Connect request for BT device with address={device.AddressString}, isPaired = {device.IsPaired}");

        if (!device.IsPaired)
        {
            await this.DoPair(device, pinString);
        }

        if (device.IsConnected)
        {
            // this could mean the user just paired and macOS decided not to disconnect this time
            Trace.WriteLine("Device is already open. Attempting to close...");
            var closeResult = (IOReturn)device.CloseConnection();
            Trace.WriteLine($"Close result: {closeResult.ToDebugString()}");
            await Task.Delay(1000); // let the close operation settle
        }

        Trace.WriteLine("Attempting to open RFCOMM channel");

        var rfcommDelegate = new RfcommChannelEventDelegate();

#if DEBUG
        rfcommDelegate.RfcommChannelClosedEvent += (o, e) => Trace.WriteLine($"RfcommChannelClosedEvent on channel {e.Channel.ChannelID}");
        rfcommDelegate.RfcommChannelControlSignalsChangedEvent += (o, e) => Trace.WriteLine($"RfcommChannelControlSignalsChangedEvent on channel {e.Channel.ChannelID}");
        rfcommDelegate.RfcommChannelFlowControlChangedEvent += (o, e) => Trace.WriteLine($"RfcommChannelFlowControlChangedEvent on channel {e.Channel.ChannelID}");
        rfcommDelegate.RfcommChannelOpenCompleteEvent += (o, e) => Trace.WriteLine($"RfcommChannelOpenCompleteEvent on channel {e.Channel.ChannelID} with error={e.Error}");

        // These are especially noisy
        // rfcommDelegate.RfcommChannelDataEvent += (o, e) => Trace.WriteLine($"RfcommChannelDataEvent on channel {e.Channel.ChannelID} with length {e.Data.Length}");
        // rfcommDelegate.RfcommChannelQueueSpaceAvailableEvent += (o, e) => Trace.WriteLine($"RfcommChannelQueueSpaceAvailableEvent on channel {e.Channel.ChannelID}");
        // rfcommDelegate.RfcommChannelWriteCompleteEvent += (o, e) => Trace.WriteLine($"RfcommChannelWriteCompleteEvent on channel {e.Channel.ChannelID} with error={e.Error}");
#endif

        rfcommDelegate.RfcommChannelDataEvent += this.RfcommDelegate_RfcommChannelData;

        Trace.WriteLine("about to openRfcommChannel");
        var openResult = (IOReturn)device.OpenRfcommChannelSync(out var channel, 1, rfcommDelegate);
        Trace.WriteLine($"openRfcommChannel result={openResult.ToDebugString()}");

        // Sometimes OpenRfcommChannel returns success but channel.IsOpen is false. It might become true later.
        // Sometimes OpenRfcommChannel returns failure but channel.IsOpen will still become true later.
        // This behavior seems to differ by macOS version: macOS 10, 11, and 12 are all slightly different.
        // So:
        // - ignore the return value of OpenRfcommChannel
        // - if channel.IsOpen is false, try polling for a bit to see if it turns true
        // - only fail if channel.IsOpen is still false after polling
        if (!channel.IsOpen)
        {
            Trace.WriteLine("polling in case openRfcommChannel just needs some time");

            var connectionDidTimeout = false;
            using (var connectionTimer = new Timer((_) => { connectionDidTimeout = true; }, null, TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan))
            {
                while (!(channel.IsOpen || connectionDidTimeout))
                {
                    await Task.Delay(100);
                }
            }
        }

        if (!channel.IsOpen)
        {
            Trace.WriteLine("RFCOMM channel is not open even after polling");
            throw JsonRpc2Error.ServerError(-32500, "Could not connect to RFCOMM channel.").ToException();
        }

        Trace.WriteLine("RFCOMM channel is open");

        // finally, commit the connection to shared state
        await this.rfcommQueue.DispatchTask(() =>
        {
            this.connectedChannel = channel;
        });

        rfcommDelegate.RfcommChannelClosedEvent += (o, e) => this.EndSession();

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

        return await this.rfcommQueue.DispatchTask(() =>
        {
            if (this.connectedChannel == null)
            {
                throw JsonRpc2Error.InvalidRequest("cannot send when not connected").ToException();
            }

            IOReturn writeResult;
            GCHandle pinnedBuffer = default(GCHandle);

            try
            {
                pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                writeResult = (IOReturn)this.connectedChannel.WriteSync(pinnedBuffer.AddrOfPinnedObject(), shortLength);
            }
            finally
            {
                pinnedBuffer.Free();
            }

            if (writeResult != IOReturn.Success)
            {
                Trace.WriteLine($"send error: {writeResult.ToDebugString()}");
                throw JsonRpc2Error.InternalError("send encountered an error").ToException();
            }

            return shortLength;
        });
    }

    private Task DoPair(BluetoothDevice device, string pinString)
    {
        // TODO: try to use IOBluetoothUI for a more directed pairing process
        var completionSource = new TaskCompletionSource<bool>();

        NSApplication.SharedApplication.BeginInvokeOnMainThread(() =>
        {
            NSWorkspace.SharedWorkspace.OpenURL(
                NSUrl.FromFilename("/System/Library/PreferencePanes/Bluetooth.prefPane"),
                NSWorkspaceLaunchOptions.Default,
                new NSDictionary(),
                out var _);

            var alert = new NSAlert
            {
                AlertStyle = NSAlertStyle.Informational,
                MessageText = "Please use Bluetooth Preferences to connect to this device for the first time.",
                InformativeText = $"Selected peripheral device: {device.NameOrAddress}",
                AccessoryView = NSTextField.CreateLabel(string.Join(
                    Environment.NewLine,
                    "1. Go to Bluetooth Preferences",
                    $"2. Find {device.NameOrAddress} and press 'Connect'",
                    $"3. Follow the instructions on your computer and/or device",
                    $"   until {device.NameOrAddress} displays 'Connected'",
                    "   * Check 'Options' to make sure the PIN / Passkey / Code",
                    "     matches on your computer and your device",
                    "4. Close Bluetooth Preferences",
                    "5. Press OK to continue",
                    "6. You might need to retry the connection")),
            };
            alert.RunModal();

            completionSource.TrySetResult(true);
        });

        return completionSource.Task;
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

        if (e.Device.IsConnected)
        {
            Trace.WriteLine($"BT ignoring connected device that would otherwise match: {e.Device.NameOrAddress}");
            return;
        }

        await this.OnPeripheralDiscovered(e.Device, e.Device.Address, e.Device.NameOrAddress, e.Device.Rssi);
    }
}

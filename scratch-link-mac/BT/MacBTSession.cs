// <copyright file="MacBTSession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac.BT;

using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Fleck;
using IOBluetooth;
using ScratchLink.BT;
using ScratchLink.JsonRpc;

/// <summary>
/// Implements a BT session on MacOS.
/// </summary>
internal class MacBTSession : BTSession
{
    private const int KIOReturnSuccess = 0;

    private readonly DeviceInquiry inquiry;

    private DeviceClassMajor searchClassMajor;
    private DeviceClassMinor searchClassMinor;
    private byte[] ouiPrefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="MacBTSession"/> class.
    /// </summary>
    /// <param name="webSocket">The web socket.</param>
    public MacBTSession(IWebSocketConnection webSocket)
        : base(webSocket)
    {
        ObjCRuntime.Dlfcn.dlopen("/System.Library/Frameworks/IOBluetooth.framework/IOBluetooth", 0);

        this.inquiry = new DeviceInquiry();

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
    protected override Task<object> DoDiscover(byte majorDeviceClass, byte minorDeviceClass, byte[] ouiPrefix)
    {
        // don't use inquiry.SetSearchCriteria to filter for device class
        // see the DeviceFound handler for details
        this.searchClassMajor = (DeviceClassMajor)majorDeviceClass;
        this.searchClassMinor = (DeviceClassMinor)minorDeviceClass;
        this.inquiry.SearchType = DeviceSearchType.Classic;
        this.inquiry.InquiryLength = 20;
        this.inquiry.UpdateNewDeviceNames = true;
        var inquiryStatus = this.inquiry.Start();
        if (inquiryStatus != KIOReturnSuccess)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.ServerError(-32500, "Device inquiry failed to start"));
        }

        return Task.FromResult<object>(null);
    }

    /// <inheritdoc/>
    protected override Task<object> DoConnect(JsonElement jsonPeripheralId)
    {
        throw new System.NotImplementedException();
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

        if (this.ouiPrefix != null)
        {
            if ((this.ouiPrefix[0] != e.Device.Address.Data[0]) ||
                (this.ouiPrefix[1] != e.Device.Address.Data[1]) ||
                (this.ouiPrefix[2] != e.Device.Address.Data[2]))
            {
                return;
            }
        }

        var message = new BTPeripheralDiscovered
        {
            Name = e.Device.NameOrAddress,
            PeripheralId = e.Device.AddressString,
            RSSI = e.Device.Rssi,
        };
        await this.SendRequest("didDiscoverPeripheral", message, this.CancellationToken);
    }
}

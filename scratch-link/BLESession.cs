// <copyright file="BLESession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using ScratchLink.JsonRpc;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Implements the cross-platform portions of a BLE session.
/// </summary>
/// <typeparam name="TUUID">The platform-specific type which represents UUIDs (like Guid or CBUUID).</typeparam>
internal abstract class BLESession<TUUID> : Session
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BLESession{TUUID}"/> class.
    /// </summary>
    /// <inheritdoc cref="Session.Session(WebSocketContext)"/>
    public BLESession(WebSocketContext context)
        : base(context)
    {
        this.GattHelpers = IPlatformApplication.Current.Services.GetService<GattHelpers<TUUID>>();
        this.Handlers["discover"] = this.HandleDiscover;
        this.Handlers["connect"] = this.HandleConnect;
        this.Handlers["write"] = this.HandleWrite;
        this.Handlers["read"] = this.HandleRead;
        this.Handlers["startNotifications"] = this.HandleStartNotifications;
        this.Handlers["stopNotifications"] = this.HandleStopNotifications;
        this.Handlers["getServices"] = this.HandleGetServices;
    }

    /// <summary>
    /// Gets a GattHelpers instance configured for this platform.
    /// </summary>
    protected GattHelpers<TUUID> GattHelpers { get; }

    /// <summary>
    /// Implement the JSON-RPC "discover" request to search for peripherals which match the filter information
    /// provided in the parameters. Valid in the initial state; transitions to discovery state on success.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("discover").</param>
    /// <param name="args">
    /// JSON object containing at least one filter, and optionally an "optionalServices" list. See
    /// <a href="https://webbluetoothcg.github.io/web-bluetooth/#dictdef-requestdeviceoptions">here</a> for more
    /// information, but note that the "acceptAllDevices" property is ignored.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task<object> HandleDiscover(string methodName, JsonElement? args)
    {
        if (args?.TryGetProperty("filters", out var jsonFilters) != true ||
            jsonFilters.ValueKind != JsonValueKind.Array)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("could not parse filters in discovery request"));
        }

        if (jsonFilters.GetArrayLength() < 1)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("discovery request must include at least one filter"));
        }

        var filters = jsonFilters.EnumerateArray().Select(jsonFilter => this.ParseFilter(jsonFilter)).ToList();
        if (filters.Any(filter => filter.IsEmpty))
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("discovery request includes empty filter"));
        }

        HashSet<TUUID> optionalServices = null;
        if (args?.TryGetProperty("optionalServices", out var jsonOptionalServices) == true)
        {
            if (jsonOptionalServices.ValueKind != JsonValueKind.Array)
            {
                throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("could not parse optionalServices in discovery request"));
            }

            optionalServices = new (jsonOptionalServices.EnumerateArray().Select(this.GattHelpers.GetServiceUuid));
        }

        return await this.DoDiscover(filters, optionalServices);
    }

    /// <summary>
    /// Platform-specific implementation for peripheral device discovery.
    /// </summary>
    /// <param name="filters">The filters for device discovery. A peripheral device must match at least one filter to pass.</param>
    /// <param name="optionalServices">Additional services the client might use, in addition to those in the matching filter.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected abstract Task<object> DoDiscover(List<BLEScanFilter> filters, HashSet<TUUID> optionalServices);

    /// <summary>
    /// Implement the JSON-RPC "connect" request to connect to a particular peripheral.
    /// Valid in the discovery state; transitions to connected state on success.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("connect").</param>
    /// <param name="args">
    /// A JSON object containing the UUID of a peripheral found by the most recent discovery request.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task<object> HandleConnect(string methodName, JsonElement? args)
    {
        if (args?.TryGetProperty("peripheralId", out var jsonPeripheralId) != true)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("connect request must include peripheralId"));
        }

        return await this.DoConnect(jsonPeripheralId);
    }

    /// <summary>
    /// Platform-specific implementation for connecting to a peripheral device.
    /// </summary>
    /// <param name="jsonPeripheralId">A JSON element representing a platform-specific peripheral ID.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    protected abstract Task<object> DoConnect(JsonElement jsonPeripheralId);

    /// <summary>
    /// Implement the JSON-RPC "write" request to write a value to a particular service characteristic.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("write").</param>
    /// <param name="args">
    /// The IDs of the service and characteristic along with the message and optionally the message encoding.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected Task<object> HandleWrite(string methodName, JsonElement? args)
    {
        return Task.FromResult<object>(null);
    }

    /// <summary>
    /// Implement the JSON-RPC "read" request to read the value of a particular service characteristic.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("read").</param>
    /// <param name="args">
    /// The IDs of the service and characteristic, an optional encoding to be used in the response, and an optional
    /// flag to request notification of future changes to this characteristic's value.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected Task<object> HandleRead(string methodName, JsonElement? args)
    {
        return Task.FromResult<object>(null);
    }

    /// <summary>
    /// Implement the JSON-RPC "startNotifications" request to start receiving notifications for changes in a characteristic's value.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("startNotifications").</param>
    /// <param name="args">The service and characteristic for which to start notifications.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected Task<object> HandleStartNotifications(string methodName, JsonElement? args)
    {
        return Task.FromResult<object>(null);
    }

    /// <summary>
    /// Implement the JSON-RPC "stopNotifications" request to stop receiving notifications for changes in a characteristic's value.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("stopNotifications").</param>
    /// <param name="args">The service and characteristic for which to stop notifications.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected Task<object> HandleStopNotifications(string methodName, JsonElement? args)
    {
        return Task.FromResult<object>(null);
    }

    /// <summary>
    /// Implement the JSON-RPC "getServices" request which lists all available services on the peripheral device.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("getServices").</param>
    /// <param name="args">Ignored.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected Task<object> HandleGetServices(string methodName, JsonElement? args)
    {
        return Task.FromResult<object>(null);
    }

    /// <summary>
    /// Parse JSON to create a new instance of the <see cref="BLEScanFilter"/> class.
    /// See <a href="https://webbluetoothcg.github.io/web-bluetooth/#bluetoothlescanfilterinit-canonicalizing">here</a>.
    /// </summary>
    /// <param name="jsonFilter">The JSON element to parse and canonicalize to build the new filter object.</param>
    /// <returns>A new <see cref="BLEScanFilter"/> with properties matching those specified in the provided JSON.</returns>
    protected BLEScanFilter ParseFilter(JsonElement jsonFilter)
    {
        var filter = new BLEScanFilter();

        if (jsonFilter.TryGetProperty("name", out var jsonName))
        {
            filter.Name = jsonName.GetString();
        }

        if (jsonFilter.TryGetProperty("namePrefix", out var jsonNamePrefix))
        {
            filter.NamePrefix = jsonNamePrefix.GetString();
        }

        if (jsonFilter.TryGetProperty("services", out var jsonServices))
        {
            filter.RequiredServices = new (jsonServices.EnumerateArray().Select(this.GattHelpers.GetServiceUuid));
            if (filter.RequiredServices.Count < 1)
            {
                throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams($"filter contains empty or invalid services list: {filter}"));
            }
        }

        if (jsonFilter.TryGetProperty("manufacturerData", out var jsonManufacturerData))
        {
            filter.ManufacturerData = new ();
            foreach (var property in jsonManufacturerData.EnumerateObject())
            {
                var manufacturerId = int.Parse(property.Name);
                var dataFilter = this.ParseDataFilter(property.Value);
                filter.ManufacturerData.Add(manufacturerId, dataFilter);
            }
        }

        if (jsonFilter.TryGetProperty("serviceData", out _))
        {
            throw new JsonRpc2Exception(JsonRpc2Error.ApplicationError("filtering on serviceData is not currently supported"));
        }

        return filter;
    }

    /// <summary>
    /// Parse JSON to create a new instance of the <see cref="BLEDataFilter"/> class.
    /// </summary>
    /// <param name="dataFilter">JSON representation of a data filter.</param>
    /// <returns>A new <see cref="BLEDataFilter"/> with properties matching those specified in the provided JSON.</returns>
    protected BLEDataFilter ParseDataFilter(JsonElement dataFilter)
    {
        var filter = new BLEDataFilter();

        if (dataFilter.TryGetProperty("dataPrefix", out var jsonDataPrefix))
        {
            filter.DataPrefix = new (jsonDataPrefix.EnumerateArray().Select(element => element.GetByte()));
        }
        else
        {
            // an empty data prefix is a valid way to check that manufacturer data exists for a particular ID
            filter.DataPrefix = new ();
        }

        if (dataFilter.TryGetProperty("mask", out var jsonMask))
        {
            filter.Mask = new (jsonMask.EnumerateArray().Select(element => element.GetByte()));
        }
        else
        {
            filter.Mask = Enumerable.Repeat<byte>(0xFF, filter.DataPrefix.Count).ToList();
        }

        if (filter.DataPrefix.Count != filter.Mask.Count)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams(
                $"length of data prefix ({filter.DataPrefix.Count}) does not match length of mask ({filter.Mask.Count})"));
        }

        return filter;
    }

    /// <summary>
    /// Store information associated with one entry in the "filters" array of a "discover" request.
    /// </summary>
    protected class BLEScanFilter
    {
        /// <summary>
        /// Gets or sets the exact name to search for. A peripheral device will match only if this is its exact name. Ignored if null.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the name prefix to search for. A peripheral device will match only if its name starts with this. Ignored if null.
        /// </summary>
        public string NamePrefix { get; set; }

        /// <summary>
        /// Gets or sets the set of required UUIDs for the search. A peripheral device will match only if it offers every service in this set.
        /// Ignored if null or empty.
        /// </summary>
        public HashSet<TUUID> RequiredServices { get; set; }

        /// <summary>
        /// Gets or sets the map of manufacturer data ID to manufacturer data filter. A peripheral device will match only if every manufacturer data filter matches.
        /// Ignored if null or empty.
        /// </summary>
        public Dictionary<int, BLEDataFilter> ManufacturerData { get; set; }

        /// <summary>
        /// Gets a value indicating whether or not this filter is empty. A filter is empty if it matches every possible peripheral device.
        /// </summary>
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(this.Name) &&
            string.IsNullOrWhiteSpace(this.NamePrefix) &&
            (this.RequiredServices == null || this.RequiredServices.Count < 1) &&
            (this.ManufacturerData == null || this.ManufacturerData.Count < 1);
    }

    /// <summary>
    /// Filter for matching BLE data with a mask.
    /// </summary>
    protected class BLEDataFilter
    {
        /// <summary>
        /// Gets or sets the list of bytes to match the candidate data against after masking.
        /// </summary>
        public List<byte> DataPrefix { get; set; }

        /// <summary>
        /// Gets or sets the list of bytes to mask the candidate data with before testing for a match.
        /// </summary>
        public List<byte> Mask { get; set; }
    }
}

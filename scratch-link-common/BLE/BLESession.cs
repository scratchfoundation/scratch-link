// <copyright file="BLESession.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.BLE;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fleck;
using Microsoft.Extensions.DependencyInjection;
using ScratchLink.Extensions;
using ScratchLink.JsonRpc;

/// <summary>
/// Implements the cross-platform portions of a BLE session.
/// </summary>
/// <typeparam name="TUUID">The platform-specific type which represents UUIDs (like Guid or CBUUID).</typeparam>
internal abstract class BLESession<TUUID> : Session
    where TUUID : IEquatable<TUUID>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BLESession{TUUID}"/> class.
    /// </summary>
    /// <inheritdoc cref="Session.Session(IWebSocketConnection)"/>
    public BLESession(IWebSocketConnection webSocket)
        : base(webSocket)
    {
        this.GattHelpers = ScratchLinkApp.Current.Services.GetService<GattHelpers<TUUID>>();
        this.AllowedServices = new ();
        this.Handlers["discover"] = this.HandleDiscover;
        this.Handlers["connect"] = this.HandleConnect;
        this.Handlers["write"] = this.HandleWrite;
        this.Handlers["read"] = this.HandleRead;
        this.Handlers["startNotifications"] = this.HandleStartNotifications;
        this.Handlers["stopNotifications"] = this.HandleStopNotifications;
        this.Handlers["getServices"] = this.HandleGetServices;
    }

    /// <summary>
    /// Gets a value indicating whether or not a peripheral is currently connected and available.
    /// </summary>
    protected abstract bool IsConnected { get; }

    /// <summary>
    /// Gets a GattHelpers instance configured for this platform.
    /// </summary>
    protected GattHelpers<TUUID> GattHelpers { get; }

    /// <summary>
    /// Gets the set of services which are allowed based on discovery filters.
    /// See Scratch Link protocol documentation.
    /// </summary>
    protected HashSet<TUUID> AllowedServices { get; }

    /// <summary>
    /// Parse JSON to create a new instance of the <see cref="BLEDataFilter"/> class.
    /// </summary>
    /// <param name="dataFilter">JSON representation of a data filter.</param>
    /// <returns>A new <see cref="BLEDataFilter"/> with properties matching those specified in the provided JSON.</returns>
    protected static BLEDataFilter ParseDataFilter(JsonElement dataFilter)
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

        if (filter.DataPrefix.Where((dataByte, index) => dataByte != (dataByte & filter.Mask[index])).Any())
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams(
                "invalid data filter: dataPrefix contains masked-out bits and will never match"));
        }

        return filter;
    }

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

        this.AllowedServices.Clear();

        if (args?.TryGetProperty("optionalServices", out var jsonOptionalServices) == true)
        {
            if (jsonOptionalServices.ValueKind != JsonValueKind.Array)
            {
                throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("could not parse optionalServices in discovery request"));
            }

            var optionalServices = jsonOptionalServices.EnumerateArray().Select(this.GattHelpers.GetServiceUuid);
            this.AllowedServices.UnionWith(optionalServices);
        }

        foreach (var filter in filters)
        {
            this.AllowedServices.UnionWith(filter.RequiredServices.OrEmpty());
        }

        return await this.DoDiscover(filters);
    }

    /// <summary>
    /// Platform-specific implementation for peripheral device discovery.
    /// </summary>
    /// <param name="filters">The filters for device discovery. A peripheral device must match at least one filter to pass.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected abstract Task<object> DoDiscover(List<BLEScanFilter> filters);

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
    protected async Task<object> HandleWrite(string methodName, JsonElement? args)
    {
        if (args == null)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("required parameter missing"));
        }

        var endpoint = await this.GetEndpoint("write", (JsonElement)args, GattHelpers<TUUID>.BlockListStatus.ExcludeWrites);
        var buffer = EncodingHelpers.DecodeBuffer((JsonElement)args);
        var withResponse = args?.TryGetProperty("withResponse", out var jsonWithResponse) == true ? jsonWithResponse.IsTruthy() : (bool?)null;

        var bytesWritten = await endpoint.Write(buffer, withResponse, this.CancellationToken);

        return bytesWritten;
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
    protected async Task<object> HandleRead(string methodName, JsonElement? args)
    {
        if (args == null)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("required parameter missing"));
        }

        var startNotifications = args?.TryGetProperty("startNotifications", out var jsonStartNotifications) == true && jsonStartNotifications.GetBoolean();
        var endpoint = await this.GetEndpoint("read", (JsonElement)args, GattHelpers<TUUID>.BlockListStatus.ExcludeReads);

        // TODO: add a way for the client to ask for plaintext instead of base64
        var encoding = args?.TryGetProperty("encoding", out var jsonEncoding) == true ? jsonEncoding.GetString() : "base64";

        var bytes = await endpoint.Read(this.CancellationToken);

        if (startNotifications)
        {
            await endpoint.StartNotifications(async bytes => await this.SendChangeNotification(endpoint, bytes, encoding));
        }

        return EncodingHelpers.EncodeBuffer(bytes, encoding);
    }

    /// <summary>
    /// Implement the JSON-RPC "startNotifications" request to start receiving notifications for changes in a characteristic's value.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("startNotifications").</param>
    /// <param name="args">The service and characteristic for which to start notifications.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task<object> HandleStartNotifications(string methodName, JsonElement? args)
    {
        if (args == null)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("required parameter missing"));
        }

        var endpoint = await this.GetEndpoint("startNotifications", (JsonElement)args, GattHelpers<TUUID>.BlockListStatus.ExcludeReads);

        // TODO: add a way for the client to ask for plaintext instead of base64
        var encoding = args?.TryGetProperty("encoding", out var jsonEncoding) == true ? jsonEncoding.GetString() : "base64";

        // check that the encoding is valid (and throw if not) before setting up the notification
        _ = EncodingHelpers.EncodeBuffer(Array.Empty<byte>(), encoding);

        await endpoint.StartNotifications(async bytes => await this.SendChangeNotification(endpoint, bytes, encoding));

        return null;
    }

    /// <summary>
    /// Notify the client that a characteristic's value has changed.
    /// </summary>
    /// <param name="endpoint">The endpoint for which the value has changed.</param>
    /// <param name="bytes">The new value.</param>
    /// <param name="encoding">The encoding to use when sending the new value.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task SendChangeNotification(IBLEEndpoint endpoint, byte[] bytes, string encoding)
    {
        string encodedBytes = EncodingHelpers.EncodeBuffer(bytes, encoding);

        var parameters = new Dictionary<string, string>
            {
                { "serviceId", endpoint.ServiceId },
                { "characteristicId", endpoint.CharacteristicId },
                { "message", encodedBytes },
            };

        await this.SendNotification("characteristicDidChange", parameters, this.CancellationToken);
    }

    /// <summary>
    /// Implement the JSON-RPC "stopNotifications" request to stop receiving notifications for changes in a characteristic's value.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("stopNotifications").</param>
    /// <param name="args">The service and characteristic for which to stop notifications.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task<object> HandleStopNotifications(string methodName, JsonElement? args)
    {
        if (args == null)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams("required parameter missing"));
        }

        var endpoint = await this.GetEndpoint("stopNotifications", (JsonElement)args, GattHelpers<TUUID>.BlockListStatus.ExcludeReads);

        await endpoint.StopNotifications();

        return null;
    }

    /// <summary>
    /// Implement the JSON-RPC "getServices" request which lists all available services on the peripheral device.
    /// </summary>
    /// <param name="methodName">The name of the method being called ("getServices").</param>
    /// <param name="args">Ignored.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected Task<object> HandleGetServices(string methodName, JsonElement? args)
    {
        return Task.FromResult<object>(this.AllowedServices.Select(x => x.ToString()));
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
                var dataFilter = ParseDataFilter(property.Value);
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
    /// Fetch the characteristic referred to in the <paramref name="endpointInfo"/> and perform access verification.
    /// </summary>
    /// <param name="errorContext">A string to include when reporting an error to the client, if an error is encountered.</param>
    /// <param name="endpointInfo">A JSON object which may contain a 'serviceId' property and a 'characteristicId' property.</param>
    /// <param name="checkFlag">Check if this flag is set for this service or characteristic in the block list. If so, throw.</param>
    /// <returns>The specified GATT service characteristic, if it can be resolved and all checks pass.</returns>
    /// <exception cref="JsonRpc2Exception">Thrown if the endpoint is blocked or could not be resolved.</exception>
    protected Task<IBLEEndpoint> GetEndpoint(string errorContext, JsonElement endpointInfo, GattHelpers<TUUID>.BlockListStatus checkFlag)
    {
        if (!this.IsConnected)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.ApplicationError($"Peripheral is not connected for {errorContext}"));
        }

        var serviceId = endpointInfo.TryGetProperty("serviceId", out var jsonServiceId)
            ? this.GattHelpers.GetServiceUuid(jsonServiceId)
            : this.GetDefaultServiceId();

        if (EqualityComparer<TUUID>.Default.Equals(serviceId, default))
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams($"could not determine GATT service for {errorContext}"));
        }

        var characteristicId = endpointInfo.TryGetProperty("characteristicId", out var jsonCharacteristicId)
            ? this.GattHelpers.GetCharacteristicUuid(jsonCharacteristicId)
            : this.GetDefaultCharacteristicId(serviceId);

        if (EqualityComparer<TUUID>.Default.Equals(characteristicId, default))
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams($"could not determine GATT characteristic for {errorContext}"));
        }

        if (this.GattHelpers.BlockList.TryGetValue(serviceId, out var serviceBlockStatus) && serviceBlockStatus.HasFlag(checkFlag))
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams($"service is blocked with {serviceBlockStatus}: {serviceId}"));
        }

        if (this.GattHelpers.BlockList.TryGetValue(characteristicId, out var characteristicBlockStatus) && characteristicBlockStatus.HasFlag(checkFlag))
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams($"characteristic is blocked with {serviceBlockStatus}: {serviceId}"));
        }

        if (this.AllowedServices?.Any(allowedServiceId => serviceId.Equals(allowedServiceId)) != true)
        {
            throw new JsonRpc2Exception(JsonRpc2Error.InvalidParams($"attempt to access unexpected service: {serviceId}"));
        }

        return this.DoGetEndpoint(serviceId, characteristicId);
    }

    /// <summary>
    /// Retrieve the ID of the default service on the connected peripheral.
    /// The definition of "default" service may be platform-specific but should exclude blocked services.
    /// Returns <c>default(TUUID)</c> on failure.
    /// </summary>
    /// <returns>The ID of the default service on the connected peripheral, or <c>default(TUUID)</c> on failure.</returns>
    protected abstract TUUID GetDefaultServiceId();

    /// <summary>
    /// Retrieve the ID of the default characteristic on the specified service.
    /// The definition of "default" characteristic may be platform-specific but should exclude blocked characteristics.
    /// Returns <c>default(TUUID)</c> on failure.
    /// </summary>
    /// <param name="serviceId">The service for which to find the default characteristic.</param>
    /// <returns>The ID of the default service on the connected peripheral, or <c>default(TUUID)</c> on failure.</returns>
    protected abstract TUUID GetDefaultCharacteristicId(TUUID serviceId);

    /// <summary>
    /// Platform-specific implementation for GetEndpoint.
    /// Returns <c>default(TUUID)</c> on failure.
    /// </summary>
    /// <param name="serviceId">The ID of the service to look up.</param>
    /// <param name="characteristicId">The ID of the characteristic to look up.</param>
    /// <returns>The specified GATT service characteristic, if found.</returns>
    protected abstract Task<IBLEEndpoint> DoGetEndpoint(TUUID serviceId, TUUID characteristicId);

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

        /// <summary>
        /// Test if this filter matches the provided data.
        /// </summary>
        /// <param name="testName">The device name to test against.</param>
        /// <param name="testServices">The list of services to test against.</param>
        /// <param name="testManufacterData">The dictionary of manufacturer data to test against.</param>
        /// <returns>True if all filter checks pass. False if any check fails.</returns>
        /// See https://webbluetoothcg.github.io/web-bluetooth/#matches-a-filter
        public bool Matches(string testName, IEnumerable<TUUID> testServices, IDictionary<int, IEnumerable<byte>> testManufacterData)
        {
            if (this.Name != null && this.Name != testName)
            {
                return false;
            }

            if (this.NamePrefix != null && testName?.StartsWith(this.NamePrefix) != true)
            {
                return false;
            }

            if (this.RequiredServices != null && !this.RequiredServices.IsSubsetOf(testServices))
            {
                return false;
            }

            foreach (var (manufacturerId, dataFilter) in this.ManufacturerData.OrEmpty())
            {
                if (!testManufacterData.TryGetValue(manufacturerId, out var testBytes))
                {
                    return false;
                }

                if (!dataFilter.Matches(testBytes))
                {
                    return false;
                }
            }

            return true;
        }
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

        /// <summary>
        /// Test if this data filter matches a sequence of bytes.
        /// </summary>
        /// <param name="testData">The data to test against.</param>
        /// <returns>True if the filter matches, false otherwise.</returns>
        public bool Matches(IEnumerable<byte> testData)
        {
            var testPrefix = testData.Take(this.DataPrefix.Count);

            // mask each advertised byte with the corresponding mask byte from the filter
            var maskedPrefix = testPrefix
                .Select((testByte, index) => (byte)(testByte & this.Mask[index]));

            // check if the masked bytes from the advertised data matches the filter's prefix bytes
            return maskedPrefix.SequenceEqual(this.DataPrefix);
        }
    }

    /// <summary>
    /// JSON-ready class to use when reporting that a peripheral was discovered.
    /// </summary>
    protected class BLEPeripheralDiscovered
    {
        /// <summary>
        /// Gets or sets the advertised name of the peripheral.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the ID which can be used for connecting to this peripheral.
        /// </summary>
        [JsonPropertyName("peripheralId")]
        public string PeripheralId { get; set; }

        /// <summary>
        /// Gets or sets the relative signal strength of the advertisement.
        /// </summary>
        [JsonPropertyName("rssi")]
        public int RSSI { get; set; }
    }
}

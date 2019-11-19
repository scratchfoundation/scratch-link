using Fleck;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace scratch_link
{
    /// <summary>
    /// A JSON-RPC session associated with a single Web Socket and single BLE peripheral.
    /// </summary>
    internal class BLESession : Session
    {
        /// <summary>
        /// Only return devices with RSSI >= this value.
        /// </summary>
        private const int MinimumSignalStrength = -70;

        /// <summary>
        /// Hysteresis margin for signal strength threshold.
        /// </summary>
        private const int SignalStrengthMargin = 5;

        /// <summary>
        /// Time, in milliseconds, after which a peripheral will be considered "out of range".
        /// </summary>
        private const int OutOfRangeTimeout = 2000;

        /// <summary>
        /// A device must pass at least one filter to be reported to the client.
        /// </summary>
        private IEnumerable<BLEScanFilter> _filters;

        /// <summary>
        /// Set of peripherals which have been reported to the client during the most recent discovery.
        /// These peripherals are eligible for connection.
        /// </summary>
        private readonly HashSet<ulong> _reportedPeripherals;

        /// <summary>
        /// The characteristics for which notification has been requested.
        /// </summary>
        private readonly HashSet<GattCharacteristic> _notifyCharacteristics;

        /// <summary>
        /// In addition to the services mentioned in _filters, the client will have access to these if present.
        /// </summary>
        private HashSet<Guid> _optionalServices;

        /// <summary>
        /// Cached results of GetCharacteristicsAsync(). It might be OK to use the OS cache but the exact behavior is
        /// unclear and keeping this on the C# side is faster anyway.
        /// See also: https://github.com/MicrosoftDocs/winrt-api/issues/339
        /// </summary>
        private Dictionary<Guid, IReadOnlyList<GattCharacteristic>> _cachedServiceCharacteristics;

        /// <summary>
        /// Cached results of GetCharacteristicsForUuidAsync(). It might be OK to use the OS cache but the exact
        /// behavior is unclear and keeping this on the C# side is faster anyway.
        /// See also: https://github.com/MicrosoftDocs/winrt-api/issues/339
        /// </summary>
        private Dictionary<Guid, GattCharacteristic> _cachedCharacteristics;

        private BluetoothLEDevice _peripheral;
        private IReadOnlyList<GattDeviceService> _services;
        private BluetoothLEAdvertisementWatcher _watcher;
        private HashSet<Guid> _allowedServices;

        /// <summary>
        /// Create a session dedicated to this Web Socket.
        /// </summary>
        /// <param name="webSocket"></param>
        internal BLESession(IWebSocketConnection webSocket) : base(webSocket)
        {
            _reportedPeripherals = new HashSet<ulong>();
            _notifyCharacteristics = new HashSet<GattCharacteristic>();
            _cachedServiceCharacteristics = new Dictionary<Guid, IReadOnlyList<GattCharacteristic>>();
            _cachedCharacteristics = new Dictionary<Guid, GattCharacteristic>();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                lock (_notifyCharacteristics)
                {
                    foreach (var characteristic in _notifyCharacteristics)
                    {
                        try
                        {
                            _ = StopNotifications(characteristic);
                        }
                        catch
                        {
                            // ignore: probably the peripheral is gone
                        }
                    }
                    _notifyCharacteristics.Clear();

                    if (_services != null)
                    {
                        foreach (var service in _services)
                        {
                            try
                            {
                                service.Dispose();
                            }
                            catch
                            {
                                // ignore: probably the peripheral is gone
                            }
                        }
                        _services = null;
                    }

                    _peripheral?.Dispose();
                    _peripheral = null;
                }
            }
        }

        /// <summary>
        /// Handle a client request
        /// </summary>
        /// <param name="method">The name of the method called by the client</param>
        /// <param name="parameters">The parameters passed by the client</param>
        /// <param name="completion">The completion handler to be called with the result</param>
        protected override async Task DidReceiveCall(string method, JObject parameters,
            Func<JToken, JsonRpcException, Task> completion)
        {
            switch (method)
            {
                case "discover":
                    Discover(parameters);
                    await completion(null, null);
                    break;
                case "connect":
                    await Connect(parameters);
                    await completion(null, null);
                    break;
                case "write":
                    await completion(await Write(parameters), null);
                    break;
                case "read":
                    await completion(await Read(parameters), null);
                    break;
                case "startNotifications":
                    await StartNotifications(parameters);
                    await completion(null, null);
                    break;
                case "stopNotifications":
                    await StopNotifications(parameters);
                    await completion(null, null);
                    break;
                case "getServices":
                    List<string> allServices = new List<string>();
                    foreach (var s in _services)
                    {
                        allServices.Add(s.Uuid.ToString());
                    }
                    await completion(JToken.FromObject(allServices), null);
                    break;
                default:
                    // unrecognized method: pass to base class
                    await base.DidReceiveCall(method, parameters, completion);
                    break;
            }
        }

        /// <summary>
        /// Search for peripherals which match the filter information provided in the parameters.
        /// Valid in the initial state; transitions to discovery state on success.
        /// </summary>
        /// <param name="parameters">
        /// JSON object containing at least one filter, and optionally an "optionalServices" list. See
        /// <a href="https://webbluetoothcg.github.io/web-bluetooth/#dictdef-requestdeviceoptions">here</a> for more
        /// information, but note that the "acceptAllDevices" property is ignored.
        /// </param>
        private void Discover(JObject parameters)
        {
            if (_services != null)
            {
                throw JsonRpcException.InvalidRequest("cannot discover when connected");
            }

            var jsonFilters = parameters["filters"]?.ToObject<JArray>();
            if (jsonFilters == null || jsonFilters.Count < 1)
            {
                throw JsonRpcException.InvalidParams("discovery request must include filters");
            }

            var newFilters = jsonFilters.Select(filter => new BLEScanFilter(filter)).ToList();
            if (newFilters.Any(filter => filter.IsEmpty))
            {
                throw JsonRpcException.InvalidParams("discovery request includes empty filter");
            }

            HashSet<Guid> newOptionalServices = null;
            if (parameters.TryGetValue("optionalServices", out var optionalServicesToken))
            {
                var optionalServicesArray = (JArray)optionalServicesToken;
                newOptionalServices = new HashSet<Guid>(optionalServicesArray.Select(GattHelpers.GetServiceUuid));
            }

            if (_watcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started)
            {
                _watcher.Received -= OnAdvertisementReceived;
                _watcher.Stop();
            }

            _watcher = new BluetoothLEAdvertisementWatcher()
            {
                SignalStrengthFilter =
                {
                    InRangeThresholdInDBm = MinimumSignalStrength,
                    OutOfRangeThresholdInDBm = MinimumSignalStrength - SignalStrengthMargin,
                    OutOfRangeTimeout = TimeSpan.FromMilliseconds(OutOfRangeTimeout)
                }
            };
            _reportedPeripherals.Clear();
            _filters = newFilters;
            _optionalServices = newOptionalServices;
            _watcher.Received += OnAdvertisementReceived;
            _watcher.Start();
        }

        /// <summary>
        /// Event handler which runs when a BLE advertisement is received.
        /// </summary>
        /// <param name="sender">The watcher which called this event handler</param>
        /// <param name="args">Information about the advertisement which generated this event</param>
        private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender,
            BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (args.RawSignalStrengthInDBm == -127)
            {
                // TODO: figure out why we get redundant(?) advertisements with RSSI=-127
                return;
            }

            if (args.AdvertisementType != BluetoothLEAdvertisementType.ConnectableDirected &&
                args.AdvertisementType != BluetoothLEAdvertisementType.ConnectableUndirected)
            {
                // Advertisement does not indicate that the device can connect
                return;
            }

            if (!_filters.Any(filter => filter.Matches(args.Advertisement)))
            {
                // No matching filters
                return;
            }

            var peripheralData = new JObject
            {
                new JProperty("name", new JValue(args.Advertisement.LocalName ?? "")),
                new JProperty("rssi", new JValue(args.RawSignalStrengthInDBm)),
                new JProperty("peripheralId", new JValue(args.BluetoothAddress))
            };

            _reportedPeripherals.Add(args.BluetoothAddress);
            SendRemoteRequest("didDiscoverPeripheral", peripheralData);
        }

        /// <summary>
        /// Handle the client's request to connect to a particular peripheral.
        /// Valid in the discovery state; transitions to connected state on success.
        /// </summary>
        /// <param name="parameters">
        /// A JSON object containing the UUID of a peripheral found by the most recent discovery request
        /// </param>
        private async Task Connect(JObject parameters)
        {
            if (_services != null)
            {
                throw JsonRpcException.InvalidRequest("already connected to peripheral");
            }

            var peripheralId = parameters["peripheralId"].ToObject<ulong>();
            if (!_reportedPeripherals.Contains(peripheralId))
            {
                // the client may only connect to devices that were returned by the current discovery request
                throw JsonRpcException.InvalidParams($"invalid peripheral ID: {peripheralId}");
            }

            _peripheral = await BluetoothLEDevice.FromBluetoothAddressAsync(peripheralId);
            var servicesResult = await _peripheral.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            if (servicesResult.Status != GattCommunicationStatus.Success)
            {
                throw JsonRpcException.ApplicationError($"failed to enumerate GATT services: {servicesResult.Status}");
            }

            _peripheral.ConnectionStatusChanged += OnPeripheralStatusChanged;
            _services = servicesResult.Services;

            // cache all characteristics in all services
            foreach (var service in _services) {
                var characteristicsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                if (characteristicsResult.Status != GattCommunicationStatus.Success) {
                    continue;
                }

                foreach (var characteristic in characteristicsResult.Characteristics) {
                    _cachedCharacteristics.Add(characteristic.Uuid, characteristic);
                }
            }

            // collect optional services plus all services from all filters
            // Note: this modifies _optionalServices for convenience since we know it'll go away soon.
            _allowedServices = _optionalServices ?? new HashSet<Guid>();
            _allowedServices = _filters
                .Where(filter => filter.RequiredServices?.Count > 0)
                .Aggregate(_allowedServices, (result, filter) =>
                {
                    result.UnionWith(filter.RequiredServices);
                    return result;
                });

            // clean up resources used by discovery
            _watcher.Stop();
            _watcher = null;
            _reportedPeripherals.Clear();
            _optionalServices = null;
        }

        private void OnPeripheralStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                Dispose();
            }
        }

        /// <summary>
        /// Handle the client's request to write a value to a particular service characteristic.
        /// </summary>
        /// <param name="parameters">
        /// The IDs of the service & characteristic along with the message and optionally the message encoding.
        /// </param>
        /// <returns>The number of decoded bytes written</returns>
        private async Task<JToken> Write(JObject parameters)
        {
            var buffer = EncodingHelpers.DecodeBuffer(parameters);
            var endpoint = await GetEndpoint("write request", parameters, GattHelpers.BlockListStatus.ExcludeWrites);
            var withResponse = parameters["withResponse"]?.ToObject<bool>() ??
                !endpoint.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse);

            var result = await endpoint.WriteValueWithResultAsync(buffer.AsBuffer(),
                withResponse ? GattWriteOption.WriteWithResponse : GattWriteOption.WriteWithoutResponse);

            switch (result.Status)
            {
                case GattCommunicationStatus.Success:
                    return buffer.Length;
                case GattCommunicationStatus.ProtocolError:
                    throw JsonRpcException.ApplicationError($"Error while attempting to write: {result.Status} {result.ProtocolError}"); // "ProtocolError 3"
                default:
                    throw JsonRpcException.ApplicationError($"Error while attempting to write: {result.Status}"); // "Unreachable"
            }
        }

        /// <summary>
        /// Handle the client's request to read the value of a particular service characteristic.
        /// </summary>
        /// <param name="parameters">
        /// The IDs of the service & characteristic, an optional encoding to be used in the response, and an optional
        /// flag to request notification of future changes to this characteristic's value.
        /// </param>
        /// <returns>
        /// The current value as a JSON object with a "message" property and optional "encoding" property
        /// </returns>
        private async Task<JToken> Read(JObject parameters)
        {
            var endpoint = await GetEndpoint("read request", parameters, GattHelpers.BlockListStatus.ExcludeReads);
            var encoding = parameters.TryGetValue("encoding", out var encodingToken)
                ? encodingToken?.ToObject<string>() // possibly null and that's OK
                : "base64";
            var startNotifications = parameters["startNotifications"]?.ToObject<bool>() ?? false;

            var readResult = await endpoint.ReadValueAsync(BluetoothCacheMode.Uncached);

            if (startNotifications)
            {
                await StartNotifications(endpoint, encoding);
            }

            switch (readResult.Status)
            {
                case GattCommunicationStatus.Success:
                    // Calling ToArray() on a buffer of length 0 throws an ArgumentException
                    var resultBytes = readResult.Value.Length > 0 ? readResult.Value.ToArray() : new byte[0];
                    return EncodingHelpers.EncodeBuffer(resultBytes, encoding);
                case GattCommunicationStatus.Unreachable:
                    throw JsonRpcException.ApplicationError("destination unreachable");
                default:
                    throw JsonRpcException.ApplicationError($"unknown result from read: {readResult.Status}");
            }
        }

        private async Task StartNotifications(JObject parameters)
        {
            var endpoint = await GetEndpoint("startNotifications request", parameters, GattHelpers.BlockListStatus.ExcludeReads);
            var encoding = parameters.TryGetValue("encoding", out var encodingToken)
                ? encodingToken?.ToObject<string>() // possibly null and that's OK
                : "base64";
            await StartNotifications(endpoint, encoding);
        }

        private async Task StartNotifications(GattCharacteristic endpoint, string encoding)
        {
            if (!_notifyCharacteristics.Contains(endpoint))
            {
                endpoint.ValueChanged += OnValueChanged;
                var notificationRequestResult = await endpoint.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (notificationRequestResult != GattCommunicationStatus.Success)
                {
                    endpoint.ValueChanged -= OnValueChanged;
                    throw JsonRpcException.ApplicationError(
                        $"could not start notifications: {notificationRequestResult}");
                }
                _notifyCharacteristics.Add(endpoint);
            }
        }

        /// <summary>
        /// Handle the client's request to stop receiving notifications for changes in a characteristic's value.
        /// </summary>
        /// <param name="parameters">The IDs of the service and characteristic</param>
        private async Task StopNotifications(JObject parameters)
        {
            var endpoint = await GetEndpoint("stopNotifications request", parameters, GattHelpers.BlockListStatus.ExcludeReads);
            _notifyCharacteristics.Remove(endpoint);
            await StopNotifications(endpoint);
        }

        private async Task StopNotifications(GattCharacteristic endpoint)
        {
            endpoint.ValueChanged -= OnValueChanged;
            await endpoint.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.None);
        }

        /// <summary>
        /// Handle a "ValueChanged" event on a characteristic, and report it to the client.
        /// </summary>
        /// <param name="characteristic">The characteristic which generated this event</param>
        /// <param name="args">Information about the newly-changed value</param>
        private void OnValueChanged(GattCharacteristic characteristic, GattValueChangedEventArgs args)
        {
            var parameters = new JObject
            {
                new JProperty("serviceId", characteristic.Service.Uuid),
                new JProperty("characteristicId", characteristic.Uuid),
            };
            // TODO: remember encoding from read request?
            EncodingHelpers.EncodeBuffer(args.CharacteristicValue.ToArray(), "base64", parameters);
            SendRemoteRequest("characteristicDidChange", parameters);
        }

        /// <summary>
        /// Fetch the characteristic referred to in the endpointInfo object and perform access verification.
        /// </summary>
        /// <param name="errorContext">
        /// A string to include in error reporting, if an error is encountered
        /// </param>
        /// <param name="endpointInfo">
        /// A JSON object which may contain a 'serviceId' property and a 'characteristicId' property
        /// </param>
        /// <param name="checkFlag">
        /// Check if this flag is set for this service or characteristic in the block list. If so, throw.
        /// </param>
        /// <returns>
        /// The specified GATT service characteristic, if it can be resolved and all checks pass.
        /// Otherwise, a JSON-RPC exception is thrown indicating what went wrong.
        /// </returns>
        private async Task<GattCharacteristic> GetEndpoint(string errorContext, JObject endpointInfo,
            GattHelpers.BlockListStatus checkFlag)
        {
            GattDeviceService service;
            Guid? serviceId;

            if (_peripheral.ConnectionStatus != BluetoothConnectionStatus.Connected)
            {
                throw JsonRpcException.ApplicationError($"Peripheral is not connected for {errorContext}");
            }

            if (endpointInfo.TryGetValue("serviceId", out var serviceToken))
            {
                serviceId = GattHelpers.GetServiceUuid(serviceToken);
                service = _services?.FirstOrDefault(s => s.Uuid == serviceId);
            }
            else
            {
                service = _services?.FirstOrDefault(); // could in theory be null
                serviceId = service?.Uuid;
            }

            if (!serviceId.HasValue)
            {
                throw JsonRpcException.InvalidParams($"Could not determine service UUID for {errorContext}");
            }

            if (_allowedServices?.Contains(serviceId.Value) != true)
            {
                throw JsonRpcException.InvalidParams($"attempt to access unexpected service: {serviceId}");
            }

            var blockStatus = GattHelpers.GetBlockListStatus(serviceId.Value);
            if (blockStatus.HasFlag(checkFlag))
            {
                throw JsonRpcException.InvalidParams($"service is block-listed with {blockStatus}: {serviceId}");
            }

            if (service == null)
            {
                throw JsonRpcException.InvalidParams($"could not find service {serviceId}");
            }

            GattCharacteristic characteristic;
            Guid? characteristicId;
            if (endpointInfo.TryGetValue("characteristicId", out var characteristicToken))
            {
                characteristic = null; // we will attempt to collect this below
                characteristicId = GattHelpers.GetCharacteristicUuid(characteristicToken);
            }
            else
            {
                if (!_cachedServiceCharacteristics.TryGetValue(service.Uuid, out var characteristics))
                {
                    var characteristicsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (characteristicsResult.Status != GattCommunicationStatus.Success)
                    {
                        throw JsonRpcException.ApplicationError(
                            $"failed to collect characteristics from service: {characteristicsResult.Status}");
                    }
                    characteristics = characteristicsResult.Characteristics;
                    _cachedServiceCharacteristics.Add(service.Uuid, characteristics);
                }

                characteristic = characteristics.FirstOrDefault(); // could in theory be null
                characteristicId = characteristic?.Uuid;
            }

            if (!characteristicId.HasValue)
            {
                throw JsonRpcException.InvalidParams($"Could not determine characteristic UUID for {errorContext}");
            }

            blockStatus = GattHelpers.GetBlockListStatus(characteristicId.Value);
            if (blockStatus.HasFlag(checkFlag))
            {
                throw JsonRpcException.InvalidParams(
                    $"characteristic is block-listed with {blockStatus}: {characteristicId}");
            }

            // collect the characteristic if we didn't do so above
            if (characteristic == null &&
                !_cachedCharacteristics.TryGetValue(characteristicId.Value, out characteristic))
            {
                var characteristicsResult =
                    await service.GetCharacteristicsForUuidAsync(characteristicId.Value, BluetoothCacheMode.Uncached);
                if (characteristicsResult.Status != GattCommunicationStatus.Success)
                {
                    throw JsonRpcException.ApplicationError(
                        $"failed to collect characteristics from service: {characteristicsResult.Status}");
                }

                if (characteristicsResult.Characteristics.Count < 1)
                {
                    throw JsonRpcException.InvalidParams(
                        $"could not find characteristic {characteristicId} on service {serviceId}");
                }

                // TODO: why is this a list?
                characteristic = characteristicsResult.Characteristics[0];
                _cachedCharacteristics.Add(characteristicId.Value, characteristic);
            }

            try
            {
                // Unfortunately there's no direct way to test if the peripheral object has been disposed. The
                // `connectionState` property still indicates that the peripheral is connected in some cases, for
                // example when Bluetooth is turned off in Bluetooth settings / Control Panel. However, trying to
                // access the `Service` property of the `Characteristic` will throw an `ObjectDisposedException` in
                // this case, so that's the hack being used here to check for a disposed peripheral.
                var tempDisposalProbe = characteristic.Service;
            }
            catch (ObjectDisposedException)
            {
                // This could mean that Bluetooth was turned off or the computer resumed from sleep
                throw JsonRpcException.ApplicationError($"Peripheral is disposed for {errorContext}");
            }

            return characteristic;
        }
    }

    internal class BLEDataFilter
    {
        public readonly List<byte> dataPrefix;
        public readonly List<byte> mask;

        internal BLEDataFilter(JToken dataFilter)
        {
            var filterObject = (JObject)dataFilter;

            JToken token;

            if (filterObject.TryGetValue("dataPrefix", out token))
            {
                dataPrefix = token.ToObject<List<byte>>();
            }
            else
            {
                dataPrefix = new List<byte>();
            }

            if (filterObject.TryGetValue("mask", out token))
            {
                mask = token.ToObject<List<byte>>();
            }
            else
            {
                mask = Enumerable.Repeat<byte>(0xFF, dataPrefix.Count).ToList();
            }

            if (dataPrefix.Count != mask.Count)
            {
                throw JsonRpcException.InvalidParams(
                    $"length of data prefix ({dataPrefix.Count}) does not match length of mask ({mask.Count})");
            }
        }

        public bool Matches(IBuffer data)
        {
            if (data.Length < dataPrefix.Count)
            {
                return false;
            }
            var reader = DataReader.FromBuffer(data);
            var dataBytes = new byte[dataPrefix.Count];
            reader.ReadBytes(dataBytes);
            // if the result doesn't contain `false` then the result is empty or all true
            return !(
                mask
                .Zip(dataBytes, (maskByte, dataByte) => maskByte & dataByte) // actualByte = (maskByte & dataByte)
                .Zip(dataPrefix, (actualByte, expectedByte) => actualByte == expectedByte) // result = (actualByte == expectedByte)
                .Contains(false) // did any of the comparisons fail?
            );
        }
    }

    internal class BLEScanFilter
    {
        public string Name { get; }
        public string NamePrefix { get; }
        public HashSet<Guid> RequiredServices { get; }
        public Dictionary<int, BLEDataFilter> ManufacturerData { get; }

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Name) &&
            string.IsNullOrWhiteSpace(NamePrefix) &&
            (RequiredServices == null || RequiredServices.Count < 1) &&
            (ManufacturerData == null || ManufacturerData.Count < 1);

        // See https://webbluetoothcg.github.io/web-bluetooth/#bluetoothlescanfilterinit-canonicalizing
        internal BLEScanFilter(JToken filter)
        {
            var filterObject = (JObject)filter;

            JToken token;

            if (filterObject.TryGetValue("name", out token))
            {
                Name = token.ToString();
            }

            if (filterObject.TryGetValue("namePrefix", out token))
            {
                NamePrefix = token.ToString();
            }

            if (filterObject.TryGetValue("services", out token))
            {
                var serviceArray = (JArray)token;
                RequiredServices = new HashSet<Guid>(serviceArray.Select(GattHelpers.GetServiceUuid));
                if (RequiredServices.Count < 1)
                {
                    throw JsonRpcException.InvalidParams($"filter contains empty or invalid services list: {filter}");
                }
            }

            if (filterObject.TryGetValue("manufacturerData", out token))
            {
                ManufacturerData = new Dictionary<int, BLEDataFilter>();
                var manufacturerData = (JObject)token;
                foreach (var kv in manufacturerData)
                {
                    var manufacturerId = int.Parse(kv.Key);
                    var dataFilter = new BLEDataFilter(kv.Value);
                    ManufacturerData.Add(manufacturerId, dataFilter);
                }
            }

            if (filterObject.TryGetValue("serviceData", out token))
            {
                throw JsonRpcException.ApplicationError("filtering on serviceData is not currently supported");
            }
        }

        // See https://webbluetoothcg.github.io/web-bluetooth/#matches-a-filter
        public bool Matches(BluetoothLEAdvertisement advertisement)
        {
            if (!string.IsNullOrWhiteSpace(Name) && (advertisement.LocalName != Name))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(NamePrefix) && (!advertisement.LocalName.StartsWith(NamePrefix)))
            {
                return false;
            }

            if (RequiredServices != null)
            {
                if (!RequiredServices.All(service => advertisement.ServiceUuids.Contains(service)))
                {
                    return false;
                }
            }

            if (ManufacturerData != null)
            {
                foreach (var manufacturerDataFilter in ManufacturerData)
                {
                    var advertisedData = advertisement.ManufacturerData
                        .FirstOrDefault(d => d.CompanyId == manufacturerDataFilter.Key);
                    if (advertisedData == null)
                    {
                        // the peripheral doesn't advertise any data under that manufacturer ID
                        return false;
                    }
                    if (!manufacturerDataFilter.Value.Matches(advertisedData.Data))
                    {
                        // the advertised data doesn't match the filter
                        return false;
                    }
                }
            }

            return true;
        }
    }
}

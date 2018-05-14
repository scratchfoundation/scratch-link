using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Newtonsoft.Json.Linq;

namespace scratch_connect
{
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
        /// In addition to the services mentioned in _filters, the client will have access to these if present.
        /// </summary>
        private HashSet<Guid> _optionalServices;

        private BluetoothLEDevice _peripheral;
        private BluetoothLEAdvertisementWatcher _watcher;
        private HashSet<Guid> _allowedServices;

        internal BLESession(WebSocket webSocket) : base(webSocket)
        {
            _reportedPeripherals = new HashSet<ulong>();
        }

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
                    Connect(parameters);
                    await completion(null, null);
                    break;
                case "pingMe":
                    await completion("willPing", null);
                    SendRemoteRequest("ping", null, (result, error) =>
                    {
                        Debug.Print($"Got result from ping: {result}");
                        return Task.CompletedTask;
                    });
                    break;
                default:
                    throw JsonRpcException.MethodNotFound(method);
            }
        }

        private void Discover(JObject parameters)
        {
            if (_peripheral != null)
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
                var optionalServicesArray = (JArray) optionalServicesToken;
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

        private async void Connect(JObject parameters)
        {
            if (_peripheral != null)
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

            // collect optional services plus all services from all filters
            // Note: this modifies _optionalServices for convenience since we know it'll go away soon.
            _allowedServices = _filters
                .Where(filter => filter.RequiredServices?.Count > 0)
                .Aggregate(_optionalServices, (result, filter) =>
            {
                result.UnionWith(filter.RequiredServices);
                return result;
            });

            // remove everything that's completely excluded by the block-list
            // we will filter specifically for reads and writes later
            _allowedServices.RemoveWhere(uuid =>
                GattHelpers.GetBlockListStatus(uuid) == GattHelpers.BlockListStatus.Exclude);

            // clean up resources used by discovery
            _watcher.Stop();
            _watcher = null;
            _reportedPeripherals.Clear();
            _optionalServices = null;
        }
    }

    internal class BLEScanFilter
    {
        public string Name { get; }
        public string NamePrefix { get; }
        public HashSet<Guid> RequiredServices { get; }

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Name) &&
            string.IsNullOrWhiteSpace(NamePrefix) &&
            (RequiredServices == null || RequiredServices.Count < 1);

        // See https://webbluetoothcg.github.io/web-bluetooth/#bluetoothlescanfilterinit-canonicalizing
        internal BLEScanFilter(JToken filter)
        {
            var filterObject = (JObject) filter;

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
                var serviceArray = (JArray) token;
                RequiredServices = new HashSet<Guid>(serviceArray.Select(GattHelpers.GetServiceUuid));
                if (RequiredServices.Count < 1)
                {
                    throw JsonRpcException.InvalidParams($"filter contains empty or invalid services list: {filter}");
                }
            }

            if (filterObject.TryGetValue("manufacturerData", out token))
            {
                throw JsonRpcException.ApplicationError("filtering on manufacturerData is not currently supported");
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

            return RequiredServices.All(service => advertisement.ServiceUuids.Contains(service));
        }
    }
}

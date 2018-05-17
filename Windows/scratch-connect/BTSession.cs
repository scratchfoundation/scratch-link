using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;

namespace scratch_connect
{
    internal class BTSession : Session
    {
        // Things we can look for are listed here:
        // <a href="https://docs.microsoft.com/en-us/windows/uwp/devices-sensors/device-information-properties" />

        /// <summary>
        /// Signal strength property
        /// </summary>
        private const string SignalStrengthPropertyName = "System.Devices.Aep.SignalStrength";

        /// <summary>
        /// Indicates that the device returned is actually available and not discovered from a cache
        /// </summary>
        private const string IsPresentPropertyName = "System.Devices.Aep.IsPresent";

        /// <summary>
        /// Bluetooth MAC address
        /// </summary>
        private const string BluetoothAddressPropertyName = "System.Devices.Aep.DeviceAddress";

        /// <summary>
        /// PIN code expected to pair with EV3
        /// </summary>
        private const string EV3PairingCode = "1234";

        private DeviceWatcher _watcher;
        private readonly List<DeviceInformation> _devices;
        private StreamSocket _connectedSocket;

        internal BTSession(WebSocket webSocket) : base(webSocket)
        {
            _devices = new List<DeviceInformation>();
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
                    if (_watcher.Status == DeviceWatcherStatus.Started)
                    {
                        _watcher.Stop();
                    }
                    Connect(parameters);
                    await completion(null, null);
                    break;
                default:
                    throw JsonRpcException.MethodNotFound(method);
            }
        }

        private void Discover(JObject parameters)
        {
            var major = parameters["majorDeviceClass"]?.ToObject<BluetoothMajorClass>();
            var minor = parameters["minorDeviceClass"]?.ToObject<BluetoothMinorClass>();
            if (major == null || minor == null)
            {
                throw JsonRpcException.InvalidParams("majorDeviceClass and minorDeviceClass required");
            }

            var deviceClass = BluetoothClassOfDevice.FromParts(major.Value, minor.Value,
                    BluetoothServiceCapabilities.None);
            var selector = BluetoothDevice.GetDeviceSelectorFromClassOfDevice(deviceClass);

            try
            {
                _watcher = DeviceInformation.CreateWatcher(selector, new List<String>
                {
                    SignalStrengthPropertyName,
                    IsPresentPropertyName,
                    BluetoothAddressPropertyName
                });
                _watcher.Added += PeripheralDiscovered;
                _watcher.Removed += PeripheralLost;
                _watcher.Updated += PeripheralUpdated;
                _watcher.EnumerationCompleted += EnumerationCompleted;
                _watcher.Stopped += EnumerationStopped;
                _watcher.Start();
            }
            catch (ArgumentException)
            {
                throw JsonRpcException.ApplicationError("Failed to create device watcher");
            }
        }

        private async void Connect(JObject parameters)
        {
            var id = parameters["peripheralId"]?.ToObject<string>();
            var address = Convert.ToUInt64(id, 16);
            var bluetoothDevice = await BluetoothDevice.FromBluetoothAddressAsync(address);
            if (!bluetoothDevice.DeviceInformation.Pairing.IsPaired)
            {
                await Pair(bluetoothDevice);
            }
            var services = await bluetoothDevice.GetRfcommServicesAsync(BluetoothCacheMode.Uncached);
            if (services.Services.Count > 0)
            {
                _connectedSocket = new StreamSocket();
                await _connectedSocket.ConnectAsync(services.Services[0].ConnectionHostName,
                    services.Services[0].ConnectionServiceName);
            }
            else
            {
                throw JsonRpcException.ApplicationError("Cannot read services from peripheral");
            }
        }

        private async Task Pair(BluetoothDevice bluetoothDevice)
        {
            bluetoothDevice.DeviceInformation.Pairing.Custom.PairingRequested += CustomOnPairingRequested;
            var pairingResult =
                await bluetoothDevice.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ProvidePin);
            if (!(pairingResult.Status == DevicePairingResultStatus.Paired ||
                pairingResult.Status == DevicePairingResultStatus.AlreadyPaired))
            {
                throw JsonRpcException.ApplicationError("Could not automatically pair with peripheral");
            }
        }

        #region Custom Pairing Events

        private void CustomOnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            args.Accept(EV3PairingCode);
        }

        #endregion

        #region DeviceWatcher Events

        async void PeripheralDiscovered(DeviceWatcher sender, DeviceInformation deviceInformation)
        {
            if (!deviceInformation.Properties.TryGetValue(IsPresentPropertyName, out var isPresent)
                || (bool)isPresent == false)
            {
                return;
            }
            deviceInformation.Properties.TryGetValue(BluetoothAddressPropertyName, out var address);
            deviceInformation.Properties.TryGetValue(SignalStrengthPropertyName, out var rssi);
            var peripheralId = ((string) address)?.Replace(":", "");

            _devices.Add(deviceInformation);

            var peripheralInfo = new JObject
            {
                new JProperty("peripheralId", peripheralId),
                new JProperty("name", new JValue(deviceInformation.Name)),
                new JProperty("rssi", rssi)
            };

            SendRemoteRequest("didDiscoverPeripheral", peripheralInfo);
        }

        async void PeripheralUpdated(DeviceWatcher sender, DeviceInformationUpdate deviceUpdate)
        {
            foreach (var device in _devices)
            {
                if (device.Id == deviceUpdate.Id)
                {
                    device.Update(deviceUpdate);
                }
            }
        }

        async void PeripheralLost(DeviceWatcher sender, DeviceInformationUpdate deviceUpdate)
        {
            var index = _devices.IndexOf(_devices.FirstOrDefault(d => d.Id == deviceUpdate.Id));
            _devices.RemoveAt(index);
        }

        async void EnumerationCompleted(DeviceWatcher sender, object args)
        {
            Debug.Print("Enumeration completed.");
        }

        async void EnumerationStopped(DeviceWatcher sender, object args)
        {
            if (_watcher.Status == DeviceWatcherStatus.Aborted)
            {
                Debug.Print("Enumeration stopped unexpectedly.");
            }
            else if (_watcher.Status == DeviceWatcherStatus.Stopped)
            {
                Debug.Print("Enumeration stopped.");
            }
            _watcher.Added -= PeripheralDiscovered;
            _watcher.Removed -= PeripheralLost;
            _watcher.Updated -= PeripheralUpdated;
            _watcher.EnumerationCompleted -= EnumerationCompleted;
            _watcher.Stopped -= EnumerationStopped;
        }

        #endregion
    }
}

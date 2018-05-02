using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace scratch_connect
{
    internal class BTSession : Session
    {
        // Things we can look for are listed here:
        // https://docs.microsoft.com/en-us/windows/uwp/devices-sensors/device-information-properties
        private const string SignalStrengthPropertyName = "System.Devices.Aep.SignalStrength";
        private DeviceWatcher _watcher;
        private readonly List<DeviceInformation> _devices;

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
                    try
                    {
                        if (!parameters.TryGetValue("majorDeviceClass", out var majorDeviceClassToken) || 
                            !parameters.TryGetValue("minorDeviceClass", out var minorDeviceClassToken))
                        {
                            throw new ArgumentException();
                        }
                        var major = majorDeviceClassToken.ToObject<int>();
                        var minor = minorDeviceClassToken.ToObject<int>();
                        var deviceClass =
                            BluetoothClassOfDevice.FromParts((BluetoothMajorClass) major, (BluetoothMinorClass) minor,
                                BluetoothServiceCapabilities.None);
                        var selector = BluetoothDevice.GetDeviceSelectorFromClassOfDevice(deviceClass);

                        _watcher = DeviceInformation.CreateWatcher(selector,
                            new List<String> { SignalStrengthPropertyName });
                        _watcher.Added += PeripheralDiscovered;
                        _watcher.Removed += PeripheralLost;
                        _watcher.Updated += PeripheralUpdated;
                        _watcher.EnumerationCompleted += EnumerationCompleted;
                        _watcher.Stopped += EnumerationStopped;
                        _watcher.Start();

                        await completion(null, null);
                    }
                    catch (ArgumentException)
                    {
                        await completion(null,
                            JsonRpcException.InvalidParams("majorDeviceClass and minorDeviceClass required"));
                    }
                    break;
                case "connect":
                    _watcher.Stop();
                    break;
                default:
                    throw JsonRpcException.MethodNotFound(method);
            }
        }

        #region DeviceWatcher Events

        async void PeripheralDiscovered(DeviceWatcher sender, DeviceInformation deviceInformation)
        {
            _devices.Add(deviceInformation);

            dynamic peripheralInfo = new JObject();
            peripheralInfo.peripheralId = deviceInformation.Id.Split('-')[1];
            peripheralInfo.name = deviceInformation.Name;
            peripheralInfo.rssi = deviceInformation.Properties[SignalStrengthPropertyName];
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
        }

        #endregion
    }
}

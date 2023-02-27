using Fleck;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace scratch_link
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
        /// PIN code for auto-pairing
        /// </summary>
        private string _pairingCode = "1234";

        private DeviceWatcher _watcher;
        private StreamSocket _connectedSocket;
        private DataWriter _socketWriter;
        private DataReader _socketReader;

        internal BTSession(IWebSocketConnection webSocket) : base(webSocket)
        {
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_watcher != null &&
                (_watcher.Status == DeviceWatcherStatus.Started ||
                 _watcher.Status == DeviceWatcherStatus.EnumerationCompleted))
            {
                _watcher.Stop();
            }
            if (_connectedSocket != null)
            {
                CloseConnection();
            }
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
                    if (_watcher != null && _watcher.Status == DeviceWatcherStatus.Started)
                    {
                        _watcher.Stop();
                    }
                    await Connect(parameters);
                    await completion(null, null);
                    break;
                case "send":
                    await completion(await SendMessage(parameters), null);
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
                _watcher.EnumerationCompleted += EnumerationCompleted;
                _watcher.Updated += PeripheralUpdated;
                _watcher.Stopped += EnumerationStopped;
                _watcher.Start();
            }
            catch (ArgumentException)
            {
                throw JsonRpcException.ApplicationError("Failed to create device watcher");
            }
        }

        private async Task Connect(JObject parameters)
        {
            if (_connectedSocket?.Information.RemoteHostName != null)
            {
                throw JsonRpcException.InvalidRequest("Already connected");
            }
            var id = parameters["peripheralId"]?.ToObject<string>();
            var address = Convert.ToUInt64(id, 16);
            var bluetoothDevice = await BluetoothDevice.FromBluetoothAddressAsync(address);
            if (!bluetoothDevice.DeviceInformation.Pairing.IsPaired)
            {
                if (parameters.TryGetValue("pin", out var pin))
                {
                    _pairingCode = (string) pin;
                }
                var pairingResult = await Pair(bluetoothDevice);
                if (pairingResult != DevicePairingResultStatus.Paired &&
                    pairingResult != DevicePairingResultStatus.AlreadyPaired)
                {
                    throw JsonRpcException.ApplicationError("Could not automatically pair with peripheral");
                }
            }

            var services = await bluetoothDevice.GetRfcommServicesForIdAsync(RfcommServiceId.SerialPort,
                BluetoothCacheMode.Uncached);
            if (services.Services.Count > 0)
            {
                _connectedSocket = new StreamSocket();
                await _connectedSocket.ConnectAsync(services.Services[0].ConnectionHostName,
                    services.Services[0].ConnectionServiceName);
                _socketWriter = new DataWriter(_connectedSocket.OutputStream);
                _socketReader = new DataReader(_connectedSocket.InputStream) {ByteOrder = ByteOrder.LittleEndian};
                ListenForMessages();
            }
            else
            {
                throw JsonRpcException.ApplicationError("Cannot read services from peripheral");
            }
        }

        private async Task<DevicePairingResultStatus> Pair(BluetoothDevice bluetoothDevice)
        {
            bluetoothDevice.DeviceInformation.Pairing.Custom.PairingRequested += CustomOnPairingRequested;
            var pairingResult = await bluetoothDevice.DeviceInformation.Pairing.Custom.PairAsync(
                DevicePairingKinds.ProvidePin);
            bluetoothDevice.DeviceInformation.Pairing.Custom.PairingRequested -= CustomOnPairingRequested;
            return pairingResult.Status;
        }

        private async Task<JToken> SendMessage(JObject parameters)
        {
            if (_socketWriter == null)
            {
                throw JsonRpcException.InvalidRequest("Not connected to peripheral");
            }

            var data = EncodingHelpers.DecodeBuffer(parameters);
            try
            {
                _socketWriter.WriteBytes(data);
                await _socketWriter.StoreAsync();
            }
            catch (ObjectDisposedException)
            {
                throw JsonRpcException.InvalidRequest("Not connected to peripheral");
            }
            return data.Length;
        }

        private async void ListenForMessages()
        {
            try
            {
                while (true)
                {
                    await _socketReader.LoadAsync(sizeof(UInt16));
                    var messageSize = _socketReader.ReadUInt16();
                    var headerBytes = BitConverter.GetBytes(messageSize);

                    var messageBytes = new byte[messageSize];
                    await _socketReader.LoadAsync(messageSize);
                    _socketReader.ReadBytes(messageBytes);

                    var totalBytes = new byte[headerBytes.Length + messageSize];
                    Array.Copy(headerBytes, totalBytes, headerBytes.Length);
                    Array.Copy(messageBytes, 0, totalBytes, headerBytes.Length, messageSize);

                    var parameters = EncodingHelpers.EncodeBuffer(totalBytes, "base64");
                    SendRemoteRequest("didReceiveMessage", parameters);
                }
            }
            catch (Exception e)
            {
                Debug.Print($"Closing connection to peripheral: {e.Message}");
                CloseConnection();
            }
        }

        private void CloseConnection()
        {
            _socketReader.Dispose();
            _socketWriter.Dispose();
            _connectedSocket.Dispose();
        }

        #region Custom Pairing Event Handlers

        private void CustomOnPairingRequested(DeviceInformationCustomPairing sender,
            DevicePairingRequestedEventArgs args)
        {
            args.Accept(_pairingCode);
        }

        #endregion

        #region DeviceWatcher Event Handlers

        private void PeripheralDiscovered(DeviceWatcher sender, DeviceInformation deviceInformation)
        {
            if (!deviceInformation.Properties.TryGetValue(IsPresentPropertyName, out var isPresent)
                || isPresent == null || (bool)isPresent == false)
            {
                return;
            }
            deviceInformation.Properties.TryGetValue(BluetoothAddressPropertyName, out var address);
            deviceInformation.Properties.TryGetValue(SignalStrengthPropertyName, out var rssi);
            var peripheralId = ((string) address)?.Replace(":", "");

            var peripheralInfo = new JObject
            {
                new JProperty("peripheralId", peripheralId),
                new JProperty("name", new JValue(deviceInformation.Name)),
                new JProperty("rssi", rssi)
            };

            SendRemoteRequest("didDiscoverPeripheral", peripheralInfo);
        }

        /// <summary>
        /// Handle event when a discovered peripheral is updated
        /// </summary>
        /// <remarks>
        /// This method does nothing, but having an event handler for <see cref="DeviceWatcher.Updated"/> seems to
        /// be necessary for timely "didDiscoverPeripheral" notifications. If there is no handler, all discovered
        /// peripherals are notified right before enumeration completes.
        /// </remarks>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void PeripheralUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
        }

        private void EnumerationCompleted(DeviceWatcher sender, object args)
        {
            Debug.Print("Enumeration completed.");
        }

        private void EnumerationStopped(DeviceWatcher sender, object args)
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
            _watcher.EnumerationCompleted -= EnumerationCompleted;
            _watcher.Updated -= PeripheralUpdated;
            _watcher.Stopped -= EnumerationStopped;
            _watcher = null;
        }

        #endregion
    }
}

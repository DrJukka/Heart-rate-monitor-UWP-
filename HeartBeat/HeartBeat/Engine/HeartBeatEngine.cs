/* Copyright (c) 2016 Microsoft Corporation. This software is licensed under the MIT License.
 * See the license file delivered with this project for further information.
 */
using HeartBeat.Model;
using System;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using Windows.System.Threading;

namespace HeartBeat.Engine
{
    public delegate void ValueChangeCompletedHandler(HeartbeatMeasurement HeartbeatMeasurementValue);
    public delegate void DeviceConnectionUpdatedHandler(bool isConnected, string error);

    public class HeartBeatEngine
    {
        private GattDeviceService _service = null;
        private GattCharacteristic _characteristic = null;
        private DeviceViewModel _selectedDevice = null;
        private static HeartBeatEngine _instance = new HeartBeatEngine();

        public static HeartBeatEngine Instance
        {
            get { return _instance; }
        }

        public DeviceViewModel SelectedDevice
        {
            get { return _selectedDevice; }
            set { _selectedDevice = value; }
        }

        public event ValueChangeCompletedHandler ValueChangeCompleted;
        public event DeviceConnectionUpdatedHandler DeviceConnectionUpdated;

        //simulator timer
        ThreadPoolTimer _periodicTimer = null;
        ushort _maxSimulatorValue;
        ushort _minSimulatorValue;
        ushort _stepSimulatorValue;
        ushort _startSimulatorValue;
        bool _simulatorGoingUp;
        public void StartSimulator(ushort max, ushort min, ushort step, ushort start, bool goUp)
        {
            _maxSimulatorValue = max;
            _minSimulatorValue = min;
            _stepSimulatorValue = step;
            _startSimulatorValue = start;
            _simulatorGoingUp = goUp;

            _periodicTimer = ThreadPoolTimer.CreatePeriodicTimer(new TimerElapsedHandler(PeriodicTimerCallback), TimeSpan.FromSeconds(1));
            if (DeviceConnectionUpdated != null)
            {
                DeviceConnectionUpdated(true, null);
            }
        }
        private void PeriodicTimerCallback(ThreadPoolTimer timer)
        {
            if (ValueChangeCompleted == null)
            {
                return;
            }

            if (_simulatorGoingUp)
            {
                _startSimulatorValue = (ushort)(_startSimulatorValue + _stepSimulatorValue);
                if (_startSimulatorValue > _maxSimulatorValue)
                {
                    _startSimulatorValue = _maxSimulatorValue;
                    _simulatorGoingUp = false;
                }
            }
            else
            {
                _startSimulatorValue = (ushort)(_startSimulatorValue - _stepSimulatorValue);
                if (_startSimulatorValue < _minSimulatorValue)
                {
                    _startSimulatorValue = _minSimulatorValue;
                    _simulatorGoingUp = true;
                }
            }

            ValueChangeCompleted(HeartbeatMeasurement.GetHeartbeatMeasurementFromData(_startSimulatorValue, DateTimeOffset.Now));
        }

        public void Deinitialize()
        {
            //de-init the simulator
            if (_periodicTimer != null)
            {
                _periodicTimer.Cancel();
                _periodicTimer = null;
            }
            if (_characteristic != null)
            {
                _characteristic.ValueChanged -= Oncharacteristic_ValueChanged;
                _characteristic = null;
            }

            if (_service != null)
            {
                _service.Device.ConnectionStatusChanged -= OnConnectionStatusChanged;
                //_service.Dispose();// appears that we should not call this here!!
                _service = null;
            }
        }

        public async void InitializeServiceAsync(string deviceId, Guid characteristicsGuid)
        {
            try
            {
                Deinitialize();
                _service = await GattDeviceService.FromIdAsync(deviceId);

                if (_service != null)
                {
                    //we could be already connected, thus lets check that before we start monitoring for changes
                    if (DeviceConnectionUpdated != null && (_service.Device.ConnectionStatus == BluetoothConnectionStatus.Connected))
                    {
                        DeviceConnectionUpdated(true, null);
                    }

                    _service.Device.ConnectionStatusChanged += OnConnectionStatusChanged;
                    var characteristic = _service.GetCharacteristics(characteristicsGuid);
                    _characteristic = characteristic[0];
                    _characteristic.ValueChanged += Oncharacteristic_ValueChanged;

                    //this appears to be problematic with my simulator, thus commenting out the checking, writing always works just fine with all ways tested so far
                    /*       var currentDescriptorValue = await _characteristic.ReadClientCharacteristicConfigurationDescriptorAsync();
                           if ((currentDescriptorValue.Status != GattCommunicationStatus.Success) ||
                           (currentDescriptorValue.ClientCharacteristicConfigurationDescriptor != GattClientCharacteristicConfigurationDescriptorValue.Notify))
                           {
                      */         // most likely we never get here, though if for any reason this value is not Notify, then we should really set it to be
                    await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    //     }
                }
            }
            catch (Exception e)
            {
                if (DeviceConnectionUpdated != null)
                {
                    DeviceConnectionUpdated(false, "Accessing device failed: " + e.Message);
                }
            }
        }

        private void Oncharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var data = new byte[args.CharacteristicValue.Length];
            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);

            if (ValueChangeCompleted != null)
            {
                ValueChangeCompleted(HeartbeatMeasurement.GetHeartbeatMeasurementFromData(data, args.Timestamp));
            }
        }

        private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (DeviceConnectionUpdated != null)
            {
                DeviceConnectionUpdated(sender.ConnectionStatus == BluetoothConnectionStatus.Connected, null);
            }
        }
    }
}

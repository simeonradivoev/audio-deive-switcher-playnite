using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Playnite.SDK;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace AudioDeviceSwitcher
{
    public class AudioDevice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsDefault { get; set; }
    }

    public class AudioDeviceManager : IDisposable, IMMNotificationClient
    {
        private readonly MMDeviceEnumerator _enumerator = new MMDeviceEnumerator();

        private readonly ObservableCollection<AudioDevice> _devices = new ObservableCollection<AudioDevice>();

        private AudioDeviceSwitcher _plugin;

        public AudioDeviceManager(AudioDeviceSwitcher plugin)
        {
            _plugin = plugin;
            var client = (IMMNotificationClient)this;
            RebuildDevices();
            _enumerator.RegisterEndpointNotificationCallback(client);
        }

        public ObservableCollection<AudioDevice> Devices
        {
            get
            {
                return _devices;
            }
        }

        private void RebuildDevices()
        {
            _devices.Clear();
            
            try
            {
                var deviceCollection = _enumerator.EnumerateAudioEndPoints(
                    DataFlow.Render, 
                    DeviceState.Active);
                var defaultId = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;

                foreach (var device in deviceCollection)
                {
                    _devices.Add(new AudioDevice
                    {
                        Id = device.ID,
                        Name = device.FriendlyName,
                        IsDefault = device.ID == defaultId
                    });
                }
            }
            catch (Exception ex)
            {
                LogManager.GetLogger().Error(ex, "Error getting audio devices");
            }
        }

        public AudioDevice GetDefaultAudioDevice()
        {
            try
            {
                var defaultDevice = _enumerator.GetDefaultAudioEndpoint(
                    DataFlow.Render, 
                    Role.Multimedia);

                return new AudioDevice 
                { 
                    Id = defaultDevice.ID,
                    Name = defaultDevice.FriendlyName,
                    IsDefault = true
                };
            }
            catch (Exception ex)
            {
                LogManager.GetLogger().Error(ex, "Error getting default audio device");
                return null;
            }
        }

        public bool SetDefaultAudioDevice(string deviceId)
        {
            try
            {
                var policyConfig = new PolicyConfigClient();
                policyConfig.SetDefaultEndpoint(deviceId, Role.Multimedia);
                policyConfig.SetDefaultEndpoint(deviceId, Role.Console);
                policyConfig.SetDefaultEndpoint(deviceId, Role.Communications);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.GetLogger().Error(ex, "Error setting default audio device");
                return false;
            }
        }

        #region IDisposable

        public void Dispose()
        {
            _enumerator?.Dispose();
        }

        #endregion

        #region Implementation of IMMNotificationClient

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            var newDevice = (newState & DeviceState.Active) != 0 ? _enumerator.GetDevice(deviceId) : null;

            var existingDevice = _devices.FirstOrDefault(d => d.Id == deviceId);
            var existingDeviceIndex = existingDevice == null ? -1 : _devices.IndexOf(existingDevice);
            if (newDevice != null && existingDeviceIndex < 0)
            {
                _devices.Insert(0,new AudioDevice
                {
                    Id = deviceId,
                    Name = newDevice.FriendlyName,
                    IsDefault = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia) == newDevice
                });
            }
            else if (newDevice == null && existingDeviceIndex >= 0)
            {
                _devices.RemoveAt(existingDeviceIndex);
            }
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
            var newDevice = _enumerator.GetDevice(pwstrDeviceId);
            _devices.Insert(0,new AudioDevice
            {
                Id = pwstrDeviceId,
                Name = newDevice.FriendlyName,
                IsDefault = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia) == newDevice
            });
        }

        public void OnDeviceRemoved(string deviceId)
        {
            var existingDevice = _devices.FirstOrDefault(d => d.Id == deviceId);
            var existingDeviceIndex = existingDevice == null ? -1 : _devices.IndexOf(existingDevice);
            if (existingDeviceIndex >= 0)
            {
                _devices.RemoveAt(existingDeviceIndex);
            }
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
            
        }

        #endregion
    }
    
    [ComImport]
    [Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    internal class PolicyConfigClient
    {
    }

    [ComImport]
    [Guid("f8679f50-850a-41cf-9c72-430f290290c8")][InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        [PreserveSig]
        int GetMixFormat(string pszDeviceName, IntPtr ppFormat);

        [PreserveSig]
        int GetDeviceFormat(string pszDeviceName, bool bDefault, IntPtr ppFormat);

        [PreserveSig]
        int ResetDeviceFormat(string pszDeviceName);

        [PreserveSig]
        int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr MixFormat);

        [PreserveSig]
        int GetProcessingPeriod(string pszDeviceName, bool bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);

        [PreserveSig]
        int SetProcessingPeriod(string pszDeviceName, IntPtr pmftPeriod);

        [PreserveSig]
        int GetShareMode(string pszDeviceName, IntPtr pMode);

        [PreserveSig]
        int SetShareMode(string pszDeviceName, IntPtr mode);

        [PreserveSig]
        int GetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);

        [PreserveSig]
        int SetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);

        [PreserveSig]
        int SetDefaultEndpoint(string pszDeviceName, Role role);

        [PreserveSig]
        int SetEndpointVisibility(string pszDeviceName, bool bVisible);
    }

    internal static class PolicyConfigClientExtensions
    {
        public static void SetDefaultEndpoint(this PolicyConfigClient client, string deviceId, Role role)
        {
            var config = (IPolicyConfig)client;
            config.SetDefaultEndpoint(deviceId, role);
        }
    }
}
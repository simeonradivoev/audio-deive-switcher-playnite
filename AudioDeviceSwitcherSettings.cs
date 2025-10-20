using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AudioDeviceSwitcher
{
    public class AudioDeviceSwitcherSettings : ObservableObject
    {
        private bool _enableFullscreenSwitch = false;
        private string _fullscreenDeviceId = null;

        public bool EnableFullscreenSwitch
        {
            get => _enableFullscreenSwitch;
            set => SetValue(ref _enableFullscreenSwitch, value);
        }

        public string FullscreenDeviceId
        {
            get => _fullscreenDeviceId;
            set => SetValue(ref _fullscreenDeviceId, value);
        }
    }
    
    public class AudioDeviceSwitcherSettingsViewModel : ObservableObject, ISettings
    {
        private readonly AudioDeviceSwitcher _plugin;
        private AudioDeviceSwitcherSettings _settings;
        private AudioDeviceSwitcherSettings _editingClone;

        public ObservableCollection<AudioDevice> AvailableDevices { get; private set; }

        public AudioDeviceSwitcherSettings Settings
        {
            get => _settings;
            private set
            {
                _settings = value;
                OnPropertyChanged();
            }
        }

        public AudioDeviceSwitcherSettingsViewModel(AudioDeviceSwitcher plugin, AudioDeviceManager deviceManager)
        {
            _plugin = plugin;

            var savedSettings = _plugin.LoadPluginSettings<AudioDeviceSwitcherSettings>();

            if (savedSettings != null)
            {
                _settings = savedSettings;
            }
            else
            {
                _settings = new AudioDeviceSwitcherSettings();
            }

            // Load available devices
            try
            {
                AvailableDevices = deviceManager.Devices;
                AvailableDevices.CollectionChanged += (sender, args) => OnPropertyChanged();
            }
            catch (Exception ex)
            {
                AudioDeviceSwitcher.logger.Error(ex, "Failed to load audio devices for settings.");
                AvailableDevices = new ObservableCollection<AudioDevice>();
            }
        }

        public void BeginEdit()
        {
            // Create a copy of the settings for editing
            _editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            // Discard changes
            Settings = _editingClone;
        }

        public void EndEdit()
        {
            // Apply changes to the original settings object
            _plugin.SavePluginSettings(Settings); // Explicitly save settings
            
        }

        public bool VerifySettings(out List<string> errors)
        {
            // No validation needed
            errors = new List<string>();
            return true;
        }
    }
}
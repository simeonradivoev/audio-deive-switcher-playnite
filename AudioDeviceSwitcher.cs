using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace AudioDeviceSwitcher
{
    public class AudioDeviceSwitcher : GenericPlugin
    {
        // Made logger internal static to be accessible from VM
        internal static readonly ILogger logger = LogManager.GetLogger();

        private readonly AudioDeviceSwitcherSettingsViewModel _settings;
        private string _originalAudioDeviceId; // Stores the device ID before we switch
        private readonly AudioDeviceManager _deviceManager;
        public override Guid Id { get; } = Guid.Parse("4b207713-1cad-4236-8fb1-4e0ae60a75e3");

        public AudioDeviceSwitcher(IPlayniteAPI api) : base(api)
        {
            // Load default settings
            _deviceManager = new AudioDeviceManager(this);
            _settings = new AudioDeviceSwitcherSettingsViewModel(this,_deviceManager);

            Properties = new GenericPluginProperties
            {
                // Tell Playnite this plugin has settings
                HasSettings = true
            };
        }

        #region Overrides of Plugin

        public override void Dispose()
        {
            base.Dispose();
            _deviceManager.Dispose();
        }

        #endregion

        public override ISettings GetSettings(bool firstRunSettings)
        {
            // Return a new instance of the view model
            return _settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            // Return the settings UI
            return new AudioDeviceSwitcherSettingsView();
        }
        
        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            if (PlayniteApi.ApplicationInfo.Mode != ApplicationMode.Fullscreen || !_settings.Settings.EnableFullscreenSwitch)
            {
                return;
            }

            try
            {
                logger.Info("Entering Fullscreen, switching audio device.");
                var currentDevice = _deviceManager.GetDefaultAudioDevice();

                // Store the original device ID, but only if it's not the one we're switching to
                if (currentDevice != null && currentDevice.Id != _settings.Settings.FullscreenDeviceId)
                {
                    _originalAudioDeviceId = currentDevice.Id;
                    logger.Info($"Stored original audio device: {currentDevice.Name} ({currentDevice.Id})");
                }
                else
                {
                    _originalAudioDeviceId = null; // Already on target device or can't get current
                }

                // Set the new device
                if (_deviceManager.SetDefaultAudioDevice(_settings.Settings.FullscreenDeviceId))
                {
                    logger.Info($"Switched audio device to ID: {_settings.Settings.FullscreenDeviceId}");
                }
                else
                {
                    logger.Error($"Failed to switch audio device to ID: {_settings.Settings.FullscreenDeviceId}");
                }
            }catch (Exception ex)
            {
                logger.Error(ex, "Error during audio device switch on mode change.");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    string.Format(ResourceProvider.GetString("ADSPluginErrorSwitching"),ex.Message),
                    ResourceProvider.GetString("ADSPluginTitle"));
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            if (PlayniteApi.ApplicationInfo.Mode != ApplicationMode.Fullscreen)
            {
                return;
            }

            try
            {
                logger.Info("Exiting Fullscreen, switching audio device back.");

                // Check if we have a device to switch back to
                if (!string.IsNullOrEmpty(_originalAudioDeviceId))
                {
                    if (_deviceManager.SetDefaultAudioDevice(_originalAudioDeviceId))
                    {
                        logger.Info($"Switched audio device back to: {_originalAudioDeviceId}");
                    }
                    else
                    {
                        logger.Error($"Failed to switch audio device back to: {_originalAudioDeviceId}");
                    }
                    _originalAudioDeviceId = null; // Clear the stored ID
                }
                else
                {
                    logger.Info("No original audio device stored, not switching back.");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during audio device switch on mode change.");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    string.Format(ResourceProvider.GetString("ADSPluginErrorSwitching"),ex.Message),
                    ResourceProvider.GetString("ADSPluginTitle"));
            }
        }

        private void SetDefaultAudioDevice(AudioDevice device)
        {
            if (_deviceManager.SetDefaultAudioDevice(device.Id))
            {
                PlayniteApi.Dialogs.ShowMessage(
                    string.Format(ResourceProvider.GetString("ADSPluginSwitched"),device.Name),
                    ResourceProvider.GetString("ADSPluginTitle")
                );
            }
            else
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    ResourceProvider.GetString("ADSPluginFailedToSwitch"),
                    ResourceProvider.GetString("ADSPluginTitle")
                );
            }
        }
        
        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            var items = new List<MainMenuItem>();

            try
            {
                var devices = _deviceManager.Devices;

                if (devices == null || !devices.Any())
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(ResourceProvider.GetString("ADSPluginNoAudioDevice"), ResourceProvider.GetString("ADSPluginTitle"));
                    return items;
                }

                items.AddRange(devices.Select(device =>
                {
                    var item = new MainMenuItem()
                    {
                        Description = device.Name,
                        MenuSection = $"@{ResourceProvider.GetString("ADSPluginTitle")}",
                        Action = a => SetDefaultAudioDevice(device)
                    };

                    return item;
                }));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error showing audio device selector");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Error: {ex.Message}",
                    ResourceProvider.GetString("ADSPluginTitle")
                );
            }

            return items;
        }
    }
}
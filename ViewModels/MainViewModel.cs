using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CFanControl.Models;
using CFanControl.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;

namespace CFanControl.ViewModels
{
    public class MainViewModel : ViewModelBase, INotifyPropertyChanged
    {
        private readonly AWCCService _awccService;
        private readonly ProfileService _profileService;
        private readonly HotkeyService _hotkeyService;
        private readonly HardwareDetectionService _hardwareService;
        private readonly AutoStartService _autoStartService;
        
        private HardwareInfo _hardwareInfo;
        private FanProfile _currentProfile;
        private string _selectedProfileName;
        private ObservableCollection<string> _profileNames;
        private bool _isAutoStartEnabled;
        
        private bool _isHotkeyCapturing;
        private string _currentCaptureProfileName;
        private bool _showHotkeySuccessMessage;
        private string _hotkeySuccessText;

        private int _cpuTemp;
        private int _cpuFanRpm;
        private int _gpuTemp;
        private int _gpuFanRpm;
        
        private System.Threading.CancellationTokenSource _saveDelayTokenSource;
        
        private bool _minimizeOnExit;
        private readonly AppSettings _settings;
        private bool _allowNotifications;
        
        public HardwareInfo HardwareInfo
        {
            get => _hardwareInfo;
            set => SetProperty(ref _hardwareInfo, value);
        }
        
        public FanProfile CurrentProfile
        {
            get => _currentProfile;
            set
            {
                if (SetProperty(ref _currentProfile, value))
                {
                    OnPropertyChanged(nameof(CurrentProfileName));
                }
            }
        }
        
        public string CurrentProfileName => CurrentProfile?.Name;
        
        public FanProfile CustomProfile
        {
            get => null;
            set { }
        }
        
        public string SelectedProfileName
        {
            get => _selectedProfileName;
            set
            {
                if (SetProperty(ref _selectedProfileName, value))
                {
                    LoadSelectedProfile();
                }
            }
        }
        
        public ObservableCollection<string> ProfileNames
        {
            get => _profileNames;
            set => SetProperty(ref _profileNames, value);
        }
        
        public int CpuManualSpeed
        {
            get => CurrentProfile?.CpuSpeed ?? 50;
            set 
            {
                if (CurrentProfile != null)
                {
                    if (CurrentProfile.Name != "Custom")
                    {
                        SwitchToCustomProfile();
                        
                        if (CurrentProfile != null)
                        {
                            CurrentProfile.CpuSpeed = value;
                            OnPropertyChanged();
                        }
                        return;
                    }
                    
                    CurrentProfile.CpuSpeed = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public int GpuManualSpeed
        {
            get => CurrentProfile?.GpuSpeed ?? 50;
            set 
            {
                if (CurrentProfile != null)
                {
                    if (CurrentProfile.Name != "Custom")
                    {
                        SwitchToCustomProfile();
                        
                        if (CurrentProfile != null)
                        {
                            CurrentProfile.GpuSpeed = value;
                            OnPropertyChanged();
                        }
                        return;
                    }
                    
                    CurrentProfile.GpuSpeed = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public bool IsAutoStartEnabled
        {
            get => _isAutoStartEnabled;
            set
            {
                if (_isAutoStartEnabled != value)
                {
                    _isAutoStartEnabled = value;
                    _settings.AutoStart = value;
                    _settings.Save();
                    
                    SetStartupRegistry(value);
                    OnPropertyChanged();
                }
            }
        }
        
        public bool IsHotkeyCapturing
        {
            get => _isHotkeyCapturing;
            set => SetProperty(ref _isHotkeyCapturing, value);
        }
        
        public string CurrentCaptureProfileName
        {
            get => _currentCaptureProfileName;
            set => SetProperty(ref _currentCaptureProfileName, value);
        }

        public bool ShowHotkeySuccessMessage
        {
            get => _showHotkeySuccessMessage;
            set => SetProperty(ref _showHotkeySuccessMessage, value);
        }

        public string HotkeySuccessText
        {
            get => _hotkeySuccessText;
            set => SetProperty(ref _hotkeySuccessText, value);
        }

        public int CPUTemperature
        {
            get => _cpuTemp;
            private set => SetProperty(ref _cpuTemp, value);
        }

        public int CPUFanRPM
        {
            get => _cpuFanRpm;
            private set => SetProperty(ref _cpuFanRpm, value);
        }

        public int GPUTemperature
        {
            get => _gpuTemp;
            private set => SetProperty(ref _gpuTemp, value);
        }

        public int GPUFanRPM
        {
            get => _gpuFanRpm;
            private set => SetProperty(ref _gpuFanRpm, value);
        }
        
        public ICommand ApplyProfileCommand { get; private set; }
        public ICommand RemoveHotkeyCommand { get; private set; }
        public ICommand StartHotkeyAssignmentCommand { get; private set; }
        public ICommand RemoveProfileCommand { get; private set; }
        
        public bool AllowNotifications
        {
            get => _allowNotifications;
            set
            {
                if (_allowNotifications != value)
                {
                    _allowNotifications = value;
                    _settings.AllowNotifications = value;
                    _settings.Save();
                    OnPropertyChanged();
                }
            }
        }
        
        public bool MinimizeOnExit
        {
            get => _minimizeOnExit;
            set
            {
                if (_minimizeOnExit != value)
                {
                    _minimizeOnExit = value;
                    _settings.MinimizeOnExit = value;
                    _settings.Save();
                    OnPropertyChanged();
                }
            }
        }
        
        public MainViewModel(Window mainWindow)
        {
            _hardwareInfo = new HardwareInfo();
            _settings = AppSettings.Load();
            _isAutoStartEnabled = _settings.AutoStart;
            _minimizeOnExit = _settings.MinimizeOnExit;
            
            _profileNames = new ObservableCollection<string>();
            
            _hardwareService = new HardwareDetectionService();
            _awccService = new AWCCService();
            _profileService = new ProfileService();
            _autoStartService = new AutoStartService();
            _hotkeyService = new HotkeyService(mainWindow);
            
            LoadProfiles();
            
            CheckAutoStartStatus();
            
            ApplyProfileCommand = new RelayCommand<string>(ApplyProfile);
            RemoveHotkeyCommand = new RelayCommand<string>(RemoveHotkey);
            StartHotkeyAssignmentCommand = new RelayCommand<string>(StartHotkeyAssignment);
            RemoveProfileCommand = new RelayCommand<string>(RemoveProfile);
            
            _hotkeyService.HotkeyTriggered += HotkeyService_HotkeyTriggered;
            _profileService.ProfilesChanged += ProfileService_ProfilesChanged;
            _awccService.OnSensorUpdated += AWCCService_OnSensorUpdated;
        }
        
        private async void CheckAutoStartStatus()
        {
            try
            {
                bool isEnabled = await _autoStartService.IsEnabledAsync();
                if (isEnabled != _isAutoStartEnabled)
                {
                    _isAutoStartEnabled = isEnabled;
                    SetStartupRegistry(_isAutoStartEnabled);
                }
            }
            catch
            {
            }
        }
        
        public async Task InitializeAsync()
        {
            try
            {
                LoadSettings();
                
                _awccService.Initialize();
                
                _hardwareService.DetectHardware(_hardwareInfo);
                
                string lastProfileName = _profileService.LastProfileName;
                
                UpdateProfileList();
                
                if (!string.IsNullOrEmpty(lastProfileName) && _profileService.Profiles.ContainsKey(lastProfileName))
                {
                    _selectedProfileName = lastProfileName;
                    
                    FanProfile selectedProfile = _profileService.GetProfile(lastProfileName);
                    if (selectedProfile != null)
                    {
                        CurrentProfile = selectedProfile;
                        OnPropertyChanged(nameof(SelectedProfileName));
                        OnPropertyChanged(nameof(CurrentProfileName));
                        OnPropertyChanged(nameof(CpuManualSpeed));
                        OnPropertyChanged(nameof(GpuManualSpeed));
                        ApplyCurrentProfileSpeeds();
                    }
                }
                else
                {
                    SelectedProfileName = "Silent";
                    LoadSelectedProfile();
                }
                
                _awccService.StartPeriodicUpdates(1000);
                
                IsAutoStartEnabled = await _autoStartService.IsEnabledAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void UpdateProfileList()
        {
            var orderedProfileNames = new List<string> { "Silent", "Balanced", "Performance", "G-Mode", "Custom" };
            
            if (ProfileNames == null)
            {
                ProfileNames = new ObservableCollection<string>();
            }
            
            string currentSelected = SelectedProfileName;
            
            ProfileNames.Clear();
            
            foreach (var name in orderedProfileNames)
            {
                if (_profileService.Profiles.ContainsKey(name))
                {
                    ProfileNames.Add(name);
                }
            }
            
            if (!string.IsNullOrEmpty(currentSelected) && ProfileNames.Contains(currentSelected))
            {
                SelectedProfileName = currentSelected;
            }
            else if (ProfileNames.Count > 0)
            {
                SelectedProfileName = ProfileNames[0];
            }
        }
        
        private void ApplyCurrentProfileSpeeds()
        {
            if (CurrentProfile == null) return;

            try
            {
                _awccService.SetFanSpeed(_hardwareInfo.CpuFanId, CurrentProfile.CpuSpeed);
                _awccService.SetFanSpeed(_hardwareInfo.GpuFanId, CurrentProfile.GpuSpeed);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying '{CurrentProfile.Name}' profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void LoadSelectedProfile()
        {
            if (string.IsNullOrEmpty(SelectedProfileName))
            {
                CurrentProfile = null;
                return;
            }

            if (CurrentProfile != null && CurrentProfile.Name == SelectedProfileName)
            {
                return;
            }

            FanProfile profileToLoad = null;
            if (_profileService.Profiles.TryGetValue(SelectedProfileName, out var registeredProfile))
            {
                profileToLoad = registeredProfile;
            }

            if (profileToLoad != null)
            {
                CurrentProfile = profileToLoad;
                
                OnPropertyChanged(nameof(CpuManualSpeed));
                OnPropertyChanged(nameof(GpuManualSpeed));
                
                ApplyCurrentProfileSpeeds();

                _profileService.SetLastProfile(SelectedProfileName);
                
                // Show notification for profile change
                if (AllowNotifications)
                {
                    ShowProfileChangeNotification(SelectedProfileName);
                }
            }
            else
            {
                CurrentProfile = null; 
                MessageBox.Show($"'{SelectedProfileName}' profile could not be found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void ShowProfileChangeNotification(string profileName)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Application.Current.MainWindow is Views.MainWindow mainWindow)
                    {
                        mainWindow.ShowNotification($"Profile changed to: {profileName}", 1000);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing notification: {ex.Message}");
            }
        }
        
        private void HotkeyService_HotkeyTriggered(object sender, string profileName)
        {
            if (!string.IsNullOrEmpty(profileName))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SelectedProfileName = profileName;
                });
            }
        }
        
        private void ProfileService_ProfilesChanged(object sender, EventArgs e)
        {
            UpdateProfileList();
        }
        
        private async void UpdateAutoStart()
        {
            if (IsAutoStartEnabled)
            {
                await _autoStartService.EnableAsync();
            }
            else
            {
                await _autoStartService.DisableAsync();
            }
        }
        
        private void ApplyProfile(string profileName)
        {
            if (!string.IsNullOrEmpty(profileName))
            {
                SelectedProfileName = profileName;
            }
        }
        
        public void HotkeyDetected(Key key, ModifierKeys modifiers)
        {
            if (!IsHotkeyCapturing || string.IsNullOrEmpty(CurrentCaptureProfileName))
                return;
                
            if (_hotkeyService.SetHotkey(CurrentCaptureProfileName, key, modifiers))
            {
                HotkeySuccessText = $"Hotkey successfully assigned: {modifiers} + {key}";
                ShowHotkeySuccessMessage = true;
                
                _ = Task.Delay(1000).ContinueWith(_ => 
                {
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        ShowHotkeySuccessMessage = false;
                    });
                });
            }
            
            IsHotkeyCapturing = false;
            CurrentCaptureProfileName = null;
        }
        
        private void RemoveHotkey(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
                return;
                
            _hotkeyService.RemoveHotkey(profileName);
        }
        
        public void Cleanup()
        {
            try
            {
                if (_awccService != null)
                {
                    try
                    {
                        _awccService.StopPeriodicUpdates();
                    }
                    catch
                    {
                    }
                }
                
                if (_hotkeyService != null)
                {
                    try
                    {
                        _hotkeyService.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            try
            {
                if (_awccService != null)
                {
                    try
                    {
                        _awccService.OnSensorUpdated -= AWCCService_OnSensorUpdated;
                    }
                    catch
                    {
                    }
                    
                    try
                    {
                        _awccService.StopPeriodicUpdates();
                    }
                    catch
                    {
                    }
                }
                
                Cleanup();
            }
            catch
            {
            }
        }

        private void AWCCService_OnSensorUpdated(object sender, AWCCService.SensorUpdateEventArgs e)
        {
            if (e.SensorId == 0x01)
            {
                CPUTemperature = e.Temperature;
                CPUFanRPM = e.FanRPM;
                _hardwareInfo.CpuTemperature = e.Temperature;
                _hardwareInfo.CpuFanRpm = e.FanRPM;
            }
            else if (e.SensorId == 0x06)
            {
                GPUTemperature = e.Temperature;
                GPUFanRPM = e.FanRPM;
                _hardwareInfo.GpuTemperature = e.Temperature;
                _hardwareInfo.GpuFanRpm = e.FanRPM;
            }
            OnPropertyChanged(nameof(HardwareInfo));
        }

        private void SaveSettings()
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string fanControlDir = Path.Combine(appDataPath, "FanControl");
                string settingsPath = Path.Combine(fanControlDir, "settings.json");

                if (!Directory.Exists(fanControlDir))
                {
                    Directory.CreateDirectory(fanControlDir);
                }

                var settings = new
                {
                    MinimizeOnExit = MinimizeOnExit
                };

                string json = System.Text.Json.JsonSerializer.Serialize(settings);
                File.WriteAllText(settingsPath, json);
            }
            catch
            {
            }
        }

        private void LoadSettings()
        {
            try
            {
                _isAutoStartEnabled = _settings.AutoStart;
                _minimizeOnExit = _settings.MinimizeOnExit;
                _allowNotifications = _settings.AllowNotifications;
                
                _hotkeyService.UnregisterAllHotkeys();
                
                if (_settings.ProfileHotkeys != null)
                {
                    foreach (var hotkeyEntry in _settings.ProfileHotkeys)
                    {
                        string profileName = hotkeyEntry.Key;
                        string hotkeyString = hotkeyEntry.Value;
                        
                        if (!string.IsNullOrEmpty(profileName) && !string.IsNullOrEmpty(hotkeyString))
                        {
                            try
                            {
                                var parts = hotkeyString.Split('+');
                                if (parts.Length >= 2)
                                {
                                    ModifierKeys modifierKeys = ModifierKeys.None;
                                    
                                    for (int i = 0; i < parts.Length - 1; i++)
                                    {
                                        if (Enum.TryParse<ModifierKeys>(parts[i], out var modifier))
                                        {
                                            modifierKeys |= modifier;
                                        }
                                    }
                                    
                                    if (Enum.TryParse<Key>(parts[parts.Length - 1], out var key))
                                    {
                                        _hotkeyService.SetHotkey(profileName, key, modifierKeys);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to parse hotkey '{hotkeyString}' for profile '{profileName}': {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying loaded settings: {ex.Message}");
            }
        }

        private void SetStartupRegistry(bool enable)
        {
            try
            {
                if (enable)
                {
                    _autoStartService.EnableAsync().ConfigureAwait(false);
                }
                else
                {
                    _autoStartService.DisableAsync().ConfigureAwait(false);
                }
            }
            catch
            {
            }
        }
        
        private void LoadProfiles()
        {
            try
            {
                _profileNames.Clear();
                _profileNames.Add("Silent");
                _profileNames.Add("Balanced");
                _profileNames.Add("Performance");
                _profileNames.Add("G-Mode");
                _profileNames.Add("Custom");
                
                if (_profileNames.Count > 0)
                {
                    if (_profileService != null && 
                        !string.IsNullOrEmpty(_profileService.LastProfileName) && 
                        _profileService.Profiles.ContainsKey(_profileService.LastProfileName))
                    {
                        _selectedProfileName = _profileService.LastProfileName;
                    }
                    else
                    {
                        _selectedProfileName = "Silent";
                    }
                }
            }
            catch
            {
            }
        }
        
        public void ApplyCustomSpeed()
        {
            if (CurrentProfile != null)
            {
                _profileService.UpdateProfile(CurrentProfile);
                ApplyCurrentProfileSpeeds();
                
                ScheduleProfileSave();
            }
        }

        private void ScheduleProfileSave()
        {
            try
            {
                _saveDelayTokenSource?.Cancel();
                _saveDelayTokenSource = new System.Threading.CancellationTokenSource();
                var token = _saveDelayTokenSource.Token;
                
                _ = Task.Delay(500, token).ContinueWith(t => 
                {
                    if (!t.IsCanceled)
                    {
                        _ = _profileService.SaveProfilesAsync();
                    }
                }, TaskScheduler.Default);
            }
            catch
            {
                _ = _profileService.SaveProfilesAsync();
            }
        }

        private void StartHotkeyAssignment(string profileName)
        {
            if (!string.IsNullOrEmpty(profileName))
            {
                IsHotkeyCapturing = true;
                CurrentCaptureProfileName = profileName;
            }
        }
        
        private void RemoveProfile(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
                return;
                
            if (_profileService.RemoveProfile(profileName))
            {
                UpdateProfileList();
            }
        }

        public void Slider_DragCompleted()
        {
            if (CurrentProfile != null && CurrentProfile.Name != "Custom")
            {
                SwitchToCustomProfile();
            }
            
            ApplyCustomSpeed();
        }

        private void SwitchToCustomProfile()
        {
            int currentCpuSpeed = CurrentProfile?.CpuSpeed ?? 50;
            int currentGpuSpeed = CurrentProfile?.GpuSpeed ?? 50;

            var customProfile = _profileService.GetProfile("Custom");
            if (customProfile != null)
            {
                customProfile.CpuSpeed = currentCpuSpeed;
                customProfile.GpuSpeed = currentGpuSpeed;
                _profileService.UpdateProfile(customProfile);
                
                string oldProfileName = SelectedProfileName;
                
                _selectedProfileName = "Custom";
                CurrentProfile = customProfile;
                
                OnPropertyChanged(nameof(SelectedProfileName));
                OnPropertyChanged(nameof(CurrentProfile));
                OnPropertyChanged(nameof(CurrentProfileName));
                OnPropertyChanged(nameof(CpuManualSpeed));
                OnPropertyChanged(nameof(GpuManualSpeed));
                
                ScheduleProfileSave();
            }
        }
    }
}
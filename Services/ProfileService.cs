using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CFanControl.Models;

namespace CFanControl.Services
{
    public class ProfileService
    {
        private Dictionary<string, FanProfile> _profiles;
        private string _lastProfileName;
        private readonly string _profilesFilePath;

        public event EventHandler<string> ProfileApplied;
        public event EventHandler ProfilesChanged;

        public Dictionary<string, FanProfile> Profiles => _profiles;
        public string LastProfileName => _lastProfileName;

        public ProfileService()
        {
            _profiles = new Dictionary<string, FanProfile>();
            _profilesFilePath = GetProfilesFilePath();
            
            InitializeDefaultProfiles();
            
            LoadProfiles();
        }

        private void InitializeDefaultProfiles()
        {
            _profiles.Clear();
            _profiles.Add("Silent", new FanProfile("Silent", 30, 30));
            _profiles.Add("Balanced", new FanProfile("Balanced", 50, 50));
            _profiles.Add("Performance", new FanProfile("Performance", 70, 70));
            _profiles.Add("G-Mode", new FanProfile("G-Mode", 100, 100));
            _profiles.Add("Custom", new FanProfile("Custom", 50, 50));
            
            _lastProfileName = "Silent";
        }

        private string GetProfilesFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string fanControlDir = Path.Combine(appDataPath, "FanControl"); 
            
            if (!Directory.Exists(fanControlDir))
            {
                Directory.CreateDirectory(fanControlDir);
            }
            
            return Path.Combine(fanControlDir, "profiles.json");
        }

        public async Task LoadProfilesAsync()
        {
            await Task.Run(() => LoadProfiles());
        }

        private void LoadProfiles()
        {
            InitializeDefaultProfiles();
            try
            {
                if (File.Exists(_profilesFilePath))
                {
                    string json = File.ReadAllText(_profilesFilePath);
                    var savedData = JsonSerializer.Deserialize<ProfilesData>(json);
                    
                    if (savedData != null)
                    {
                        if (savedData.CustomProfiles.TryGetValue("Custom", out var customProfile))
                        {
                            if (_profiles.TryGetValue("Custom", out var existingCustom))
                            {
                                existingCustom.CpuSpeed = customProfile.CpuSpeed;
                                existingCustom.GpuSpeed = customProfile.GpuSpeed;
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(savedData.LastProfileName) && _profiles.ContainsKey(savedData.LastProfileName))
                        {
                            _lastProfileName = savedData.LastProfileName;
                        }
                        else
                        {
                             _lastProfileName = "Silent";
                        }
                    }
                }
            }
            catch (Exception)
            {
                 _lastProfileName = "Silent";
            }
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task SaveProfilesAsync()
        {
            await Task.Run(() => SaveProfiles());
        }

        private void SaveProfiles()
        {
            try
            {
                var customProfiles = new Dictionary<string, ProfileData>();
                
                if (_profiles.TryGetValue("Custom", out var customProfile))
                {
                    customProfiles["Custom"] = new ProfileData
                    {
                        CpuSpeed = customProfile.CpuSpeed,
                        GpuSpeed = customProfile.GpuSpeed
                    };
                }
                
                if (string.IsNullOrEmpty(_lastProfileName) || !_profiles.ContainsKey(_lastProfileName))
                {
                    _lastProfileName = "Silent";
                }
                
                var profilesData = new ProfilesData
                {
                    CustomProfiles = customProfiles,
                    LastProfileName = _lastProfileName
                };
                
                string json = JsonSerializer.Serialize(profilesData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                string tempPath = _profilesFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                
                if (File.Exists(_profilesFilePath))
                {
                    File.Delete(_profilesFilePath);
                }
                
                File.Move(tempPath, _profilesFilePath);
            }
            catch (Exception)
            {
            }
        }

        public FanProfile GetProfile(string name)
        {
            if (string.IsNullOrEmpty(name) || !_profiles.ContainsKey(name))
                return null;
                
            return _profiles[name];
        }

        public FanProfile GetLastProfile()
        {
            return GetProfile(_lastProfileName);
        }

        public bool AddProfile(FanProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.Name))
                return false;
                
            if (!IsDefaultProfile(profile.Name))
                return false;
                
            _profiles[profile.Name] = profile;
            SaveProfiles();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool UpdateProfile(FanProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.Name))
                return false;
                
            if (!_profiles.ContainsKey(profile.Name))
                return false;
            
            if (profile.Name != "Custom" && IsDefaultProfile(profile.Name))
                return false;
                
            _profiles[profile.Name] = profile;
            SaveProfiles();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool RemoveProfile(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
                
            if (IsDefaultProfile(name))
                return false;
                
            if (_profiles.Remove(name))
            {
                if (_lastProfileName == name)
                {
                    _lastProfileName = "Silent";
                }
                
                SaveProfiles();
                ProfilesChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            
            return false;
        }

        public void SetLastProfile(string name)
        {
            if (string.IsNullOrEmpty(name) || !_profiles.ContainsKey(name))
            {
                return;
            }
            
            _lastProfileName = name;
            
            SaveProfiles();
            
            ProfileApplied?.Invoke(this, name);
        }

        private bool IsDefaultProfile(string name)
        {
            return name == "Silent" || name == "Balanced" || name == "Performance" || name == "G-Mode" || name == "Custom";
        }

        private class ProfileData
        {
            public int CpuSpeed { get; set; }
            public int GpuSpeed { get; set; }
        }

        private class ProfilesData
        {
            public Dictionary<string, ProfileData> CustomProfiles { get; set; } = new Dictionary<string, ProfileData>();
            public string LastProfileName { get; set; }
        }
    }
}
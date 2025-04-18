using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using CFanControl.Models;
using System.Linq;

namespace CFanControl.Services
{
    public class HotkeyService : IDisposable
    {
        private Dictionary<string, HotkeyBinding> _hotkeyBindings;
        private readonly string _hotkeysFilePath;
        private readonly IntPtr _hwnd;
        private readonly Dictionary<int, HotkeyBinding> _registeredHotkeys;
        private bool _isDisposed = false;

        public event EventHandler<string> HotkeyTriggered;
        public event EventHandler HotkeysChanged;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;

        public Dictionary<string, HotkeyBinding> HotkeyBindings => _hotkeyBindings;

        public HotkeyService(Window mainWindow)
        {
            _hotkeyBindings = new Dictionary<string, HotkeyBinding>();
            _registeredHotkeys = new Dictionary<int, HotkeyBinding>();
            _hotkeysFilePath = GetHotkeysFilePath();
            
            _hwnd = new WindowInteropHelper(mainWindow).EnsureHandle();
            
            HwndSource source = HwndSource.FromHwnd(_hwnd);
            source.AddHook(HwndHook);
            
            LoadHotkeys();
        }

        public HotkeyService(IntPtr hwnd)
        {
            _hotkeyBindings = new Dictionary<string, HotkeyBinding>();
            _registeredHotkeys = new Dictionary<int, HotkeyBinding>();
            _hotkeysFilePath = GetHotkeysFilePath();
            
            _hwnd = hwnd;
            
            HwndSource source = HwndSource.FromHwnd(_hwnd);
            source.AddHook(HwndHook);
            
            LoadHotkeys();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_registeredHotkeys.ContainsKey(id))
                {
                    var binding = _registeredHotkeys[id];
                    HotkeyTriggered?.Invoke(this, binding.ProfileName);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private string GetHotkeysFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string fanControlDir = Path.Combine(appDataPath, "FanControl");
            
            if (!Directory.Exists(fanControlDir))
            {
                Directory.CreateDirectory(fanControlDir);
            }
            
            return Path.Combine(fanControlDir, "hotkeys.json");
        }

        public async Task LoadHotkeysAsync()
        {
            await Task.Run(() => LoadHotkeys());
        }

        private void LoadHotkeys()
        {
            try
            {
                UnregisterAllHotkeys();
                _hotkeyBindings.Clear();

                if (File.Exists(_hotkeysFilePath))
                {
                    string json = File.ReadAllText(_hotkeysFilePath);
                    var hotkeyData = JsonSerializer.Deserialize<Dictionary<string, HotkeyData>>(json);
                    
                    if (hotkeyData != null)
                    {
                        foreach (var kvp in hotkeyData)
                        {
                            if (Enum.TryParse<Key>(kvp.Value.Key, out Key key) && 
                                Enum.TryParse<ModifierKeys>(kvp.Value.Modifiers, out ModifierKeys modifiers))
                            {
                                var binding = new HotkeyBinding(kvp.Key, key, modifiers);
                                _hotkeyBindings[kvp.Key] = binding;
                                RegisterHotkey(binding);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                _hotkeyBindings.Clear();
                UnregisterAllHotkeys();
            }
        }

        public async Task SaveHotkeysAsync()
        {
            await Task.Run(() => SaveHotkeys());
        }

        private void SaveHotkeys()
        {
            var hotkeyData = new Dictionary<string, HotkeyData>();
            
            foreach (var kvp in _hotkeyBindings)
            {
                hotkeyData[kvp.Key] = new HotkeyData
                {
                    Key = kvp.Value.Key.ToString(),
                    Modifiers = kvp.Value.Modifiers.ToString()
                };
            }
            
            string json = JsonSerializer.Serialize(hotkeyData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            string tempFilePath = _hotkeysFilePath + ".tmp";
            File.WriteAllText(tempFilePath, json);
            
            if (File.Exists(_hotkeysFilePath))
            {
                File.Delete(_hotkeysFilePath);
            }
            
            File.Move(tempFilePath, _hotkeysFilePath);
        }

        public bool SetHotkey(string profileName, Key key, ModifierKeys modifiers)
        {
            if (string.IsNullOrEmpty(profileName) || key == Key.None)
                return false;
                
            foreach (var kvp in _hotkeyBindings)
            {
                if (kvp.Value.Key == key && kvp.Value.Modifiers == modifiers && kvp.Key != profileName)
                {
                    RemoveHotkey(kvp.Key);
                    break;
                }
            }
                
            if (_hotkeyBindings.ContainsKey(profileName))
            {
                UnregisterHotkey(_hotkeyBindings[profileName]);
            }
                
            var binding = new HotkeyBinding(profileName, key, modifiers);
            _hotkeyBindings[profileName] = binding;
            
            if (RegisterHotkey(binding))
            {
                SaveHotkeys();
                HotkeysChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            
            return false;
        }

        public bool RemoveHotkey(string profileName)
        {
            if (string.IsNullOrEmpty(profileName) || !_hotkeyBindings.ContainsKey(profileName))
                return false;
                
            var binding = _hotkeyBindings[profileName];
            if (UnregisterHotkey(binding))
            {
                _hotkeyBindings.Remove(profileName);
                SaveHotkeys();
                HotkeysChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            
            return false;
        }

        private int GenerateHotkeyId(Key key, ModifierKeys modifiers)
        {
            return (int)key + ((int)modifiers * 0x10000);
        }

        private bool RegisterHotkey(HotkeyBinding binding)
        {
            if (binding.Key == Key.None)
                return false;
                
            int id = GenerateHotkeyId(binding.Key, binding.Modifiers);
            
            uint modifiers = 0;
            if (binding.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= 0x0001;
            if (binding.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= 0x0002;
            if (binding.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= 0x0004;
            if (binding.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= 0x0008;
            
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(binding.Key);
            
            if (RegisterHotKey(_hwnd, id, modifiers, vk))
            {
                _registeredHotkeys[id] = binding;
                return true;
            }
            
            return false;
        }

        private bool UnregisterHotkey(HotkeyBinding binding)
        {
            if (binding.Key == Key.None)
                return false;
                
            int id = GenerateHotkeyId(binding.Key, binding.Modifiers);
            
            if (_registeredHotkeys.ContainsKey(id))
            {
                if (UnregisterHotKey(_hwnd, id))
                {
                    _registeredHotkeys.Remove(id);
                    return true;
                }
            }
            
            return false;
        }

        public void UnregisterAllHotkeys()
        {
            foreach (var id in _registeredHotkeys.Keys.ToList())
            {
                UnregisterHotKey(_hwnd, id);
            }
            _registeredHotkeys.Clear();
        }

        public Dictionary<string, string> GetHotkeyMappings()
        {
            var mappings = new Dictionary<string, string>();
            
            foreach (var kvp in _hotkeyBindings)
            {
                string profileName = kvp.Key;
                var binding = kvp.Value;
                
                if (binding != null && binding.Key != Key.None)
                {
                    string hotkeyString = string.Empty;
                    
                    if (binding.Modifiers.HasFlag(ModifierKeys.Control))
                        hotkeyString += "Control+";
                    if (binding.Modifiers.HasFlag(ModifierKeys.Alt))
                        hotkeyString += "Alt+";
                    if (binding.Modifiers.HasFlag(ModifierKeys.Shift))
                        hotkeyString += "Shift+";
                    if (binding.Modifiers.HasFlag(ModifierKeys.Windows))
                        hotkeyString += "Windows+";
                    
                    hotkeyString += binding.Key.ToString();
                    
                    mappings[profileName] = hotkeyString;
                }
            }
            
            return mappings;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    HotkeysChanged = null;
                    HotkeyTriggered = null;
                }

                UnregisterAllHotkeys();
                _isDisposed = true;
            }
        }

        ~HotkeyService()
        {
            Dispose(false);
        }

        private class HotkeyData
        {
            public string Key { get; set; }
            public string Modifiers { get; set; }
        }
    }
}
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CFanControl.Models
{
    public class HotkeyBinding : INotifyPropertyChanged
    {
        private string _profileName;
        private Key _key;
        private ModifierKeys _modifiers;

        public string ProfileName
        {
            get => _profileName;
            set
            {
                if (_profileName != value)
                {
                    _profileName = value;
                    OnPropertyChanged();
                }
            }
        }

        public Key Key
        {
            get => _key;
            set
            {
                if (_key != value)
                {
                    _key = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HotkeyString));
                }
            }
        }

        public ModifierKeys Modifiers
        {
            get => _modifiers;
            set
            {
                if (_modifiers != value)
                {
                    _modifiers = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HotkeyString));
                }
            }
        }

        public string HotkeyString
        {
            get
            {
                string result = "";

                if (Modifiers.HasFlag(ModifierKeys.Control))
                    result += "Ctrl + ";
                if (Modifiers.HasFlag(ModifierKeys.Alt))
                    result += "Alt + ";
                if (Modifiers.HasFlag(ModifierKeys.Shift))
                    result += "Shift + ";
                if (Modifiers.HasFlag(ModifierKeys.Windows))
                    result += "Win + ";

                if (Key != Key.None)
                    result += Key.ToString();

                return result.TrimEnd(' ', '+');
            }
        }

        public HotkeyBinding()
        {
            ProfileName = "";
            Key = Key.None;
            Modifiers = ModifierKeys.None;
        }

        public HotkeyBinding(string profileName, Key key, ModifierKeys modifiers)
        {
            ProfileName = profileName;
            Key = key;
            Modifiers = modifiers;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
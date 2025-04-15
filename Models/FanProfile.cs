using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CFanControl.Models
{
    public class FanProfile : INotifyPropertyChanged
    {
        private string _name;
        private int _cpuSpeed;
        private int _gpuSpeed;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CpuSpeed
        {
            get => _cpuSpeed;
            set => _cpuSpeed = Clamp(value, 0, 100);
        }

        public int GpuSpeed
        {
            get => _gpuSpeed;
            set => _gpuSpeed = Clamp(value, 0, 100);
        }

        private int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public FanProfile()
        {
            Name = "New Profile";
            CpuSpeed = 50;
            GpuSpeed = 50;
        }

        public FanProfile(string name, int cpuSpeed = 0, int gpuSpeed = 0)
        {
            Name = name;
            CpuSpeed = cpuSpeed;
            GpuSpeed = gpuSpeed;
        }

        public FanProfile Clone()
        {
            return new FanProfile(Name, CpuSpeed, GpuSpeed);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
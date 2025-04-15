using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CFanControl.Models
{
    public class HardwareInfo : INotifyPropertyChanged
    {
        private string _cpuName;
        private string _gpuName;
        private int? _cpuTemperature;
        private int? _gpuTemperature;
        private int? _cpuFanRpm;
        private int? _gpuFanRpm;
        private int _cpuFanId;
        private int _gpuFanId;
        private int _cpuSensorId;
        private int _gpuSensorId;

        public string CpuName
        {
            get => _cpuName;
            set
            {
                if (_cpuName != value)
                {
                    _cpuName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string GpuName
        {
            get => _gpuName;
            set
            {
                if (_gpuName != value)
                {
                    _gpuName = value;
                    OnPropertyChanged();
                }
            }
        }

        public int? CpuTemperature
        {
            get => _cpuTemperature;
            set
            {
                if (_cpuTemperature != value)
                {
                    _cpuTemperature = value;
                    OnPropertyChanged();
                }
            }
        }

        public int? GpuTemperature
        {
            get => _gpuTemperature;
            set
            {
                if (_gpuTemperature != value)
                {
                    _gpuTemperature = value;
                    OnPropertyChanged();
                }
            }
        }

        public int? CpuFanRpm
        {
            get => _cpuFanRpm;
            set
            {
                if (_cpuFanRpm != value)
                {
                    _cpuFanRpm = value;
                    OnPropertyChanged();
                }
            }
        }

        public int? GpuFanRpm
        {
            get => _gpuFanRpm;
            set
            {
                if (_gpuFanRpm != value)
                {
                    _gpuFanRpm = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CpuFanId
        {
            get => _cpuFanId;
            set
            {
                if (_cpuFanId != value)
                {
                    _cpuFanId = value;
                    OnPropertyChanged();
                }
            }
        }

        public int GpuFanId
        {
            get => _gpuFanId;
            set
            {
                if (_gpuFanId != value)
                {
                    _gpuFanId = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CpuSensorId
        {
            get => _cpuSensorId;
            set
            {
                if (_cpuSensorId != value)
                {
                    _cpuSensorId = value;
                    OnPropertyChanged();
                }
            }
        }

        public int GpuSensorId
        {
            get => _gpuSensorId;
            set
            {
                if (_gpuSensorId != value)
                {
                    _gpuSensorId = value;
                    OnPropertyChanged();
                }
            }
        }

        public HardwareInfo()
        {
            CpuName = "Unknown";
            GpuName = "Unknown";
            CpuTemperature = null;
            GpuTemperature = null;
            CpuFanRpm = null;
            GpuFanRpm = null;
            CpuFanId = 0x32;
            GpuFanId = 0x33;
            CpuSensorId = 0x01;
            GpuSensorId = 0x06;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
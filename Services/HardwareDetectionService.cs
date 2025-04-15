using System;
using System.Management;
using System.Linq;
using System.Threading.Tasks;
using CFanControl.Models;

namespace CFanControl.Services
{
    public class HardwareDetectionService
    {
        private const int CPU_FAN_IDX = 0;
        private const int GPU_FAN_IDX = 1;

        public HardwareDetectionService()
        {
        }

        public async Task<string> GetCpuNameAsync()
        {
            return await Task.Run(() => GetCpuName());
        }

        public async Task<string> GetGpuNameAsync()
        {
            return await Task.Run(() => GetGpuName());
        }

        public void DetectHardware(HardwareInfo hardwareInfo)
        {
            if (hardwareInfo == null) return;

            try
            {
                hardwareInfo.CpuName = GetCpuName();
                hardwareInfo.GpuName = GetGpuName();
            }
            catch (Exception)
            {
                hardwareInfo.CpuName = "Unknown";
                hardwareInfo.GpuName = "Unknown";
            }
        }

        private string GetCpuName()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("select * from Win32_Processor"))
                {
                    var processor = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    if (processor != null)
                    {
                        return processor["Name"]?.ToString().Trim() ?? "Unknown";
                    }
                }
            }
            catch (Exception)
            {
            }

            return "Unknown";
        }

        private string GetGpuName()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController"))
                {
                    var gpus = searcher.Get().Cast<ManagementObject>()
                        .Where(gpu => 
                        {
                            var name = gpu["Name"]?.ToString() ?? "";
                            return name.Contains("NVIDIA") || name.Contains("AMD") || name.Contains("Radeon");
                        })
                        .ToList();

                    if (gpus.Any())
                    {
                        var dedicatedGpu = gpus.OrderByDescending(gpu => 
                        {
                            try
                            {
                                return Convert.ToInt64(gpu["AdapterRAM"]);
                            }
                            catch (Exception)
                            {
                                return 0;
                            }
                        }).FirstOrDefault();

                        if (dedicatedGpu != null)
                        {
                            return dedicatedGpu["Name"]?.ToString().Trim() ?? "Unknown";
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return "Unknown";
        }
    }
}
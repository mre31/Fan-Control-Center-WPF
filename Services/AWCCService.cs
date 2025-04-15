using System;
using System.Collections.Generic;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using CFanControl.Models;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace CFanControl.Services
{
    [ComImport]
    [Guid("44FB51D1-44F3-4C65-B27B-C1D010B42923")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IAWCCWmiMethod
    {
        [DispId(1)]
        int Thermal_Information(int param);

        [DispId(2)]
        int Thermal_Control(int param);
    }

    public class AWCCService : IDisposable
    {
        private ManagementObject _awcc;
        private IAWCCWmiMethod _comAwcc;
        private HardwareInfo _hardwareInfo;
        private bool _isInitialized = false;
        private const int MaxRetries = 5;
        private const int RetryDelay = 1000;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _updateCancellation;
        private Task _updateTask;
        private bool _isDisposed;

        public const int SENSOR_ID_FIRST = 0x01;
        public const int SENSOR_ID_LAST = 0x30;
        public const int FAN_ID_FIRST = 0x31;
        public const int FAN_ID_LAST = 0x63;

        public bool IsInitialized => _isInitialized;
        public HardwareInfo HardwareInfo => _hardwareInfo;

        public AWCCService()
        {
            _hardwareInfo = new HardwareInfo();
        }

        public bool Initialize()
        {
            try
            {
                if (!IsAdministrator())
                {
                    throw new UnauthorizedAccessException("This application must be run with administrator privileges.");
                }

                Exception lastError = null;
                for (int attempt = 0; attempt < MaxRetries; attempt++)
                {
                    try
                    {
                        var scope = new ManagementScope(@"root\WMI");
                        scope.Connect();

                        var query = new ObjectQuery("SELECT * FROM AWCCWmiMethodFunction");
                        using (var searcher = new ManagementObjectSearcher(scope, query))
                        {
                            var collection = searcher.Get();
                            if (collection.Count > 0)
                            {
                                _awcc = collection.Cast<ManagementObject>().FirstOrDefault();
                                if (_awcc != null)
                                {
                                    DetectFanAndSensorIds();
                                    _isInitialized = true;
                                    return true;
                                }
                            }
                        }
                    }
                    catch (Exception wmiEx)
                    {
                        lastError = wmiEx;
                        Debug.WriteLine($"WMI connection error: {wmiEx.Message}");
                    }

                    try
                    {
                        try
                        {
                            Type type = Type.GetTypeFromCLSID(new Guid("44FB51D1-44F3-4C65-B27B-C1D010B42923"));
                            if (type != null)
                            {
                                object instance = Activator.CreateInstance(type);
                                if (instance != null)
                                {
                                    _comAwcc = (IAWCCWmiMethod)instance;
                                    
                                    DetectFanAndSensorIds();
                                    _isInitialized = true;
                                    return true;
                                }
                            }
                        }
                        catch (System.Runtime.InteropServices.COMException comEx)
                        {
                            lastError = comEx;
                            Debug.WriteLine($"COM initialization error: {comEx.Message}, Error code: 0x{comEx.ErrorCode:X8}");
                        }
                        catch (Exception ex)
                        {
                            lastError = ex;
                            Debug.WriteLine($"COM general error: {ex.Message}");
                        }
                    }
                    catch (Exception initEx)
                    {
                        lastError = initEx;
                        Debug.WriteLine($"Initialization error: {initEx.Message}");
                    }

                    Thread.Sleep(RetryDelay);
                }

                _isInitialized = false;
                Debug.WriteLine($"AWCC connection failed: {lastError?.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _isInitialized = false;
                Debug.WriteLine($"AWCC service could not be started: {ex.Message}");
                return false;
            }
        }

        private void DetectFanAndSensorIds()
        {
            var detectedPairs = new List<(int FanId, int SensorId)>();
            
            var knownPairs = new[]
            {
                (FanId: 0x32, SensorId: 0x01),
                (FanId: 0x33, SensorId: 0x06)
            };

            foreach (var pair in knownPairs)
            {
                int? rpm = GetFanRPM(pair.FanId);
                if (rpm.HasValue)
                {
                    int? temp = GetSensorTemperature(pair.SensorId);
                    if (temp.HasValue)
                    {
                        detectedPairs.Add((pair.FanId, pair.SensorId));
                    }
                }
            }

            if (detectedPairs.Count == 0)
            {
                detectedPairs.Add((0x32, 0x01));
                detectedPairs.Add((0x33, 0x06));
            }

            if (detectedPairs.Count >= 2)
            {
                var cpuPair = detectedPairs[0];
                var gpuPair = detectedPairs[1];

                _hardwareInfo.CpuFanId = cpuPair.FanId;
                _hardwareInfo.CpuSensorId = cpuPair.SensorId;
                _hardwareInfo.GpuFanId = gpuPair.FanId;
                _hardwareInfo.GpuSensorId = gpuPair.SensorId;
            }
        }

        private ManagementBaseObject InvokeWmiMethod(string methodName, int arg)
        {
            try
            {
                if (_awcc == null)
                {
                    return null;
                }

                Thread.Sleep(50);

                var inParams = _awcc.GetMethodParameters(methodName);
                if (inParams == null)
                {
                    return null;
                }

                if (methodName == "Thermal_Control" || methodName == "Thermal_Information")
                {
                    if (inParams.Properties["arg2"] != null)
                    {
                        inParams["arg2"] = arg;
                    }
                    else if (inParams.Properties["argr"] != null)
                    {
                        inParams["argr"] = arg;
                    }
                    else if (inParams.Properties.Count > 0)
                    {
                        var firstParam = inParams.Properties.Cast<PropertyData>().First();
                        inParams[firstParam.Name] = arg;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }

                var options = new InvokeMethodOptions();
                options.Timeout = TimeSpan.FromSeconds(5);

                var result = _awcc.InvokeMethod(methodName, inParams, options);
                
                if (result == null)
                {
                    return null;
                }

                return result;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private int? GetWmiReturnValue(ManagementBaseObject result)
        {
            if (result == null) 
            {
                return null;
            }

            foreach (PropertyData prop in result.Properties)
            {
                if (prop.Value != null)
                {
                    if (prop.Value is bool boolValue)
                    {
                        var intValue = boolValue ? 1 : 0;
                        return intValue;
                    }
                    
                    if (prop.Value is uint uintValue)
                    {
                        if (uintValue == 0xFFFFFFFF)
                        {
                            continue;
                        }
                        if (uintValue <= int.MaxValue)
                        {
                            return (int)uintValue;
                        }
                    }
                    else if (prop.Value is int intValue)
                    {
                        if (intValue == -1)
                        {
                            continue;
                        }
                        return intValue;
                    }
                }
            }

            return null;
        }

        public bool SetFanSpeed(int fanId, int speed)
        {
            if (!_isInitialized) return false;
            if (fanId < FAN_ID_FIRST || fanId > FAN_ID_LAST) return false;
            if (speed < 0) speed = 0;
            if (speed > 100) speed = 100;

            int arg = ((speed & 0xFF) << 16) | ((fanId & 0xFF) << 8) | 2;
            var result = InvokeWmiMethod("Thermal_Control", arg);
            var returnValue = GetWmiReturnValue(result);
            bool success = returnValue.HasValue && returnValue.Value == 0;
            return success;
        }

        public bool SetAllFanSpeeds(int cpuSpeed, int gpuSpeed)
        {
            bool success = true;
            
            if (!SetFanSpeed(_hardwareInfo.CpuFanId, cpuSpeed))
                success = false;
            
            if (!SetFanSpeed(_hardwareInfo.GpuFanId, gpuSpeed))
                success = false;
            
            return success;
        }

        public int? GetFanRPM(int fanId)
        {
            if (!_isInitialized) return null;
            if (fanId < FAN_ID_FIRST || fanId > FAN_ID_LAST) return null;

            int arg = ((fanId & 0xFF) << 8) | 0x05;
            var result = InvokeWmiMethod("Thermal_Information", arg);
            var value = GetWmiReturnValue(result);

            if (!value.HasValue)
            {
                return null;
            }

            return value.Value;
        }

        public int? GetSensorTemperature(int sensorId)
        {
            if (!_isInitialized) return null;
            if (sensorId < SENSOR_ID_FIRST || sensorId > SENSOR_ID_LAST) return null;

            int arg = ((sensorId & 0xFF) << 8) | 4;
            var result = InvokeWmiMethod("Thermal_Information", arg);
            var value = GetWmiReturnValue(result);

            if (!value.HasValue)
            {
                return null;
            }

            if (value.Value < 0 || value.Value > 125)
            {
                return null;
            }

            return value.Value;
        }

        public void UpdateHardwareInfo()
        {
            if (!_isInitialized) return;

            _hardwareInfo.CpuTemperature = GetSensorTemperature(_hardwareInfo.CpuSensorId);
            _hardwareInfo.GpuTemperature = GetSensorTemperature(_hardwareInfo.GpuSensorId);
            _hardwareInfo.CpuFanRpm = GetFanRPM(_hardwareInfo.CpuFanId);
            _hardwareInfo.GpuFanRpm = GetFanRPM(_hardwareInfo.GpuFanId);
        }

        public bool ApplyProfile(FanProfile profile)
        {
            if (profile == null || !_isInitialized) return false;
            
            return SetAllFanSpeeds(profile.CpuSpeed, profile.GpuSpeed);
        }

        private bool IsAdministrator()
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        public void Dispose()
        {
            StopPeriodicUpdates();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                OnSensorUpdated = null;
                
                StopPeriodicUpdates();
                
                _semaphore?.Dispose();
                
                if (_awcc != null)
                {
                    _awcc.Dispose();
                    _awcc = null;
                }
                
                if (_comAwcc != null)
                {
                    Marshal.ReleaseComObject(_comAwcc);
                    _comAwcc = null;
                }
            }

            _isDisposed = true;
        }

        ~AWCCService()
        {
            Dispose(false);
        }

        public async Task<int> GetTemperatureAsync(byte sensorId)
        {
            await _semaphore.WaitAsync();
            try
            {
                int arg = ((sensorId & 0xFF) << 8) | 4;
                var result = await Task.Run(() => InvokeWmiMethod("Thermal_Information", arg));
                return Convert.ToInt32(result);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<int> GetFanRPMAsync(byte fanId)
        {
            await _semaphore.WaitAsync();
            try
            {
                int arg = ((fanId & 0xFF) << 8) | 5;
                var result = await Task.Run(() => InvokeWmiMethod("Thermal_Information", arg));
                return Convert.ToInt32(result);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task SetFanSpeedAsync(byte fanId, byte speed)
        {
            await _semaphore.WaitAsync();
            try
            {
                int arg = ((speed & 0xFF) << 16) | ((fanId & 0xFF) << 8) | 2;
                await Task.Run(() => InvokeWmiMethod("Thermal_Control", arg));
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void StartPeriodicUpdates(int intervalMs = 1000)
        {
            StopPeriodicUpdates();
            
            _updateCancellation = new CancellationTokenSource();
            _updateTask = Task.Run(async () =>
            {
                while (!_updateCancellation.Token.IsCancellationRequested)
                {
                    try
                    {
                        var cpuTempTask = Task.Run(() => 
                        {
                            var result = InvokeWmiMethod("Thermal_Information", ((0x01 & 0xFF) << 8) | 4);
                            return GetWmiReturnValue(result);
                        });
                        
                        await Task.Delay(25, _updateCancellation.Token);
                        
                        var cpuFanTask = Task.Run(() => 
                        {
                            var result = InvokeWmiMethod("Thermal_Information", ((0x32 & 0xFF) << 8) | 5);
                            return GetWmiReturnValue(result);
                        });
                        
                        await Task.Delay(25, _updateCancellation.Token);
                        
                        var gpuTempTask = Task.Run(() => 
                        {
                            var result = InvokeWmiMethod("Thermal_Information", ((0x06 & 0xFF) << 8) | 4);
                            return GetWmiReturnValue(result);
                        });
                        
                        await Task.Delay(25, _updateCancellation.Token);
                        
                        var gpuFanTask = Task.Run(() => 
                        {
                            var result = InvokeWmiMethod("Thermal_Information", ((0x33 & 0xFF) << 8) | 5);
                            return GetWmiReturnValue(result);
                        });
                        
                        var cpuTemp = await cpuTempTask;
                        var cpuFan = await cpuFanTask;
                        var gpuTemp = await gpuTempTask;
                        var gpuFan = await gpuFanTask;

                        if (cpuTemp.HasValue && cpuFan.HasValue)
                        {
                            OnSensorUpdated?.Invoke(this, new SensorUpdateEventArgs
                            {
                                SensorId = 0x01,
                                Temperature = cpuTemp.Value,
                                FanRPM = cpuFan.Value
                            });
                        }

                        if (gpuTemp.HasValue && gpuFan.HasValue)
                        {
                            OnSensorUpdated?.Invoke(this, new SensorUpdateEventArgs
                            {
                                SensorId = 0x06,
                                Temperature = gpuTemp.Value,
                                FanRPM = gpuFan.Value
                            });
                        }

                        await Task.Delay(intervalMs, _updateCancellation.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(5000, _updateCancellation.Token);
                    }
                }
            }, _updateCancellation.Token);
        }

        public void StopPeriodicUpdates()
        {
            if (_updateCancellation != null)
            {
                if (!_updateCancellation.IsCancellationRequested)
                {
                    _updateCancellation.Cancel();
                }

                _updateCancellation.Dispose();
                _updateCancellation = null;
            }

            if (_updateTask != null)
            {
                if (!_updateTask.IsCompleted && !_updateTask.IsFaulted && !_updateTask.IsCanceled)
                {
                    _updateTask.Wait(2000);
                }
                
                _updateTask = null;
            }
        }

        public event EventHandler<SensorUpdateEventArgs> OnSensorUpdated;

        public class SensorUpdateEventArgs : EventArgs
        {
            public byte SensorId { get; set; }
            public int Temperature { get; set; }
            public int FanRPM { get; set; }
        }
    }
}
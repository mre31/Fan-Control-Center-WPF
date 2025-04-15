using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace CFanControl.Services
{
    public class AutoStartService
    {
        private readonly string _taskName = "FanControlCenter";
        private readonly string _xmlFilePath;
        
        public AutoStartService()
        {
            string tempPath = Path.GetTempPath();
            _xmlFilePath = Path.Combine(tempPath, "fan_control_task.xml");
        }
        
        public async Task<bool> EnableAsync()
        {
            return await Task.Run(() => Enable());
        }
        
        public bool Enable()
        {
            try
            {
                string appPath = Assembly.GetEntryAssembly().Location;
                
                string xmlContent = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.4"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Fan Control Center Auto Start</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>false</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <DisallowStartOnRemoteAppSession>false</DisallowStartOnRemoteAppSession>
    <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>""{appPath}""</Command>
      <Arguments>--minimized</Arguments>
    </Exec>
  </Actions>
</Task>";
                
                File.WriteAllText(_xmlFilePath, xmlContent, System.Text.Encoding.Unicode);
                
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "schtasks.exe";
                    process.StartInfo.Arguments = $"/create /tn {_taskName} /xml \"{_xmlFilePath}\" /f";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    
                    process.Start();
                    process.WaitForExit();
                    
                    if (process.ExitCode != 0)
                    {
                        return false;
                    }
                }
                
                if (File.Exists(_xmlFilePath))
                {
                    File.Delete(_xmlFilePath);
                }
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        public async Task<bool> DisableAsync()
        {
            return await Task.Run(() => Disable());
        }
        
        public bool Disable()
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "schtasks.exe";
                    process.StartInfo.Arguments = $"/delete /tn {_taskName} /f";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    
                    process.Start();
                    process.WaitForExit();
                    
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
        
        public async Task<bool> IsEnabledAsync()
        {
            return await Task.Run(() => IsEnabled());
        }
        
        public bool IsEnabled()
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "schtasks.exe";
                    process.StartInfo.Arguments = $"/query /tn {_taskName}";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    
                    process.Start();
                    process.WaitForExit();
                    
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
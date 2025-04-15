using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Security.Principal;
using System.Diagnostics;

namespace CFanControl
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (!IsRunAsAdmin())
            {
                try
                {
                    ProcessStartInfo procInfo = new ProcessStartInfo();
                    procInfo.UseShellExecute = true;
                    procInfo.FileName = Process.GetCurrentProcess().MainModule.FileName;
                    procInfo.WorkingDirectory = Environment.CurrentDirectory;
                    procInfo.Verb = "runas";
                    Process.Start(procInfo);
                    Current.Shutdown();
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The application could not be started with administrator privileges. Some features may not work properly.\n\nError: " + ex.Message,
                        "Fan Control Center", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private bool IsRunAsAdmin()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(id);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            HandleException(e.Exception);
            e.Handled = true;
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            HandleException(e.Exception);
            e.SetObserved();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleException(e.ExceptionObject as Exception);
        }

        private void HandleException(Exception ex)
        {
            try
            {
                if (ex == null) return;

                MessageBox.Show(
                    $"An unexpected error occurred in Fan Control Center:\n\n{ex.Message}\n\nThe application will now exit.",
                    "Critical Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                try
                {
                    Shutdown();
                }
                catch
                {
                    Environment.Exit(1);
                }
            }
            catch
            {
                Environment.Exit(1);
            }
        }
    }
}

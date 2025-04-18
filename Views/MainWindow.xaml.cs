using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CFanControl.ViewModels;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using System.Windows.Controls.Primitives;
using System.IO;
using System.Reflection;
using CFanControl.Services;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace CFanControl.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly HotkeyService _hotkeyService;
        private NotifyIcon _notifyIcon;
        private System.Windows.Controls.Border _notificationBorder;
        private DispatcherTimer _notificationTimer;
        
        public MainWindow()
        {
            InitializeComponent();
            
            _viewModel = new MainViewModel(this);
            
            DataContext = _viewModel;
            
            InitializeNotifyIcon();
            
            _hotkeyService = new HotkeyService(this);
            _hotkeyService.HotkeyTriggered += HotkeyService_HotkeyTriggered;
            
            InitializeNotificationSystem();
        }
        
        private void InitializeNotificationSystem()
        {
            _notificationBorder = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#252526")),
                BorderBrush = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#0078D4")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 10, 10),
                Opacity = 0,
                Visibility = Visibility.Collapsed
            };
            
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };
            
            _notificationBorder.Child = textBlock;
            
            if (Content is System.Windows.Controls.Grid grid)
            {
                grid.Children.Add(_notificationBorder);
                System.Windows.Controls.Panel.SetZIndex(_notificationBorder, 9999);
            }
            
            _notificationTimer = new DispatcherTimer();
            _notificationTimer.Tick += NotificationTimer_Tick;
        }
        
        private void NotificationTimer_Tick(object sender, EventArgs e)
        {
            HideNotification();
        }
        
        public void ShowNotification(string message, int duration = 1000)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_notificationBorder == null || !_viewModel.AllowNotifications)
                        return;
                    
                    if (_notifyIcon != null && !IsActive)
                    {
                        _notifyIcon.BalloonTipTitle = "Fan Control Center";
                        _notifyIcon.BalloonTipText = message;
                        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                        _notifyIcon.ShowBalloonTip(duration);
                    }
                    
                    if (IsActive)
                    {
                        if (_notificationBorder.Child is System.Windows.Controls.TextBlock textBlock)
                        {
                            textBlock.Text = message;
                        }
                        
                        _notificationTimer.Stop();
                        _notificationTimer.Interval = TimeSpan.FromMilliseconds(duration);
                        
                        _notificationBorder.Visibility = Visibility.Visible;
                        DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                        _notificationBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                        
                        _notificationTimer.Start();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error showing notification: {ex.Message}");
                }
            });
        }
        
        private void HideNotification()
        {
            try
            {
                _notificationTimer.Stop();
                
                DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                fadeOut.Completed += (s, e) =>
                {
                    _notificationBorder.Visibility = Visibility.Collapsed;
                };
                
                _notificationBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error hiding notification: {ex.Message}");
            }
        }
        
        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "Fan Control Center";
            
            UpdateNotifyIconMenu();
            
            try
            {
                string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
                Stream iconStream = System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/{assemblyName};component/Resources/icon.ico"))?.Stream;
                if (iconStream != null)
                {
                    _notifyIcon.Icon = new Icon(iconStream);
                    iconStream.Close();
                }
                else
                {
                     MessageBox.Show("Failed to load application icon.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading icon: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            _notifyIcon.Visible = false;
            _notifyIcon.MouseDoubleClick += NotifyIcon_MouseDoubleClick;
        }
        
        private void UpdateNotifyIconMenu()
        {
            if (_notifyIcon == null) return;
            
            var contextMenu = new System.Windows.Forms.ContextMenu();
            
            var showItem = new System.Windows.Forms.MenuItem("Show");
            showItem.Click += (s, e) => RestoreWindow();
            contextMenu.MenuItems.Add(showItem);
            
            contextMenu.MenuItems.Add("-");
            
            var profilesMenu = new System.Windows.Forms.MenuItem("Profiles");
            if (_viewModel != null && _viewModel.ProfileNames != null)
            {
                foreach (var profileName in _viewModel.ProfileNames)
                {
                    var profileItem = new System.Windows.Forms.MenuItem(profileName);
                    profileItem.RadioCheck = true;
                    profileItem.Checked = profileName == _viewModel.SelectedProfileName;
                    
                    profileItem.Click += (s, e) => 
                    {
                        var menuItem = s as System.Windows.Forms.MenuItem;
                        if (menuItem != null)
                        {
                            _viewModel.SelectedProfileName = menuItem.Text;
                            UpdateNotifyIconMenu();
                        }
                    };
                    
                    profilesMenu.MenuItems.Add(profileItem);
                }
            }
            contextMenu.MenuItems.Add(profilesMenu);
            
            var settingsMenu = new System.Windows.Forms.MenuItem("Settings");
            
            var autoStartItem = new System.Windows.Forms.MenuItem("Start with Windows");
            autoStartItem.Checked = _viewModel != null && _viewModel.IsAutoStartEnabled;
            autoStartItem.Click += (s, e) => 
            {
                if (_viewModel != null)
                {
                    _viewModel.IsAutoStartEnabled = !_viewModel.IsAutoStartEnabled;
                    UpdateNotifyIconMenu();
                }
            };
            settingsMenu.MenuItems.Add(autoStartItem);
            
            var minimizeOnExitItem = new System.Windows.Forms.MenuItem("Minimize on Exit");
            minimizeOnExitItem.Checked = _viewModel != null && _viewModel.MinimizeOnExit;
            minimizeOnExitItem.Click += (s, e) => 
            {
                if (_viewModel != null)
                {
                    _viewModel.MinimizeOnExit = !_viewModel.MinimizeOnExit;
                    UpdateNotifyIconMenu();
                }
            };
            settingsMenu.MenuItems.Add(minimizeOnExitItem);
            
            var allowNotificationsItem = new System.Windows.Forms.MenuItem("Allow Notifications");
            allowNotificationsItem.Checked = _viewModel != null && _viewModel.AllowNotifications;
            allowNotificationsItem.Click += (s, e) => 
            {
                if (_viewModel != null)
                {
                    _viewModel.AllowNotifications = !_viewModel.AllowNotifications;
                    
                    if (_viewModel.AllowNotifications)
                    {
                        ShowNotification("Notifications enabled", 1500);
                    }
                    
                    UpdateNotifyIconMenu();
                }
            };
            settingsMenu.MenuItems.Add(allowNotificationsItem);
            
            contextMenu.MenuItems.Add(settingsMenu);
            
            contextMenu.MenuItems.Add("-");
            
            var exitItem = new System.Windows.Forms.MenuItem("Exit");
            exitItem.Click += (s, e) => CloseApplication();
            contextMenu.MenuItems.Add(exitItem);
            
            _notifyIcon.ContextMenu = contextMenu;
        }
        
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
            
            UpdateNotifyIconMenu();
            
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && args[1] == "--minimized")
            {
                MinimizeToTray();
            }
        }
        
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_viewModel.MinimizeOnExit)
            {
                e.Cancel = true;
                MinimizeToTray();
                return;
            }

            CloseApplication();
        }
        
        private void HotkeyService_HotkeyTriggered(object sender, string profileName)
        {
            _viewModel.SelectedProfileName = profileName;
        }
        
        private void ChangeHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_viewModel.SelectedProfileName))
            {
                MessageBox.Show("Please select a profile first.", "Fan Control Center", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _viewModel.IsHotkeyCapturing = true;
            _viewModel.CurrentCaptureProfileName = _viewModel.SelectedProfileName;
        }
        
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_viewModel.IsHotkeyCapturing)
            {
                e.Handled = true;
                var modifiers = Keyboard.Modifiers;
                var key = e.Key;

                if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                    key == Key.LeftAlt || key == Key.RightAlt ||
                    key == Key.LeftShift || key == Key.RightShift ||
                    key == Key.LWin || key == Key.RWin)
                {
                    return;
                }

                _viewModel.HotkeyDetected(key, modifiers);
            }
        }
        
        private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            MinimizeToTray();
        }
        
        private void MinimizeToTray()
        {
            Hide();
            
            if (_notifyIcon == null)
            {
                try
                {
                    InitializeNotifyIcon();
                }
                catch (Exception)
                {
                    return;
                }
            }
            else
            {
                UpdateNotifyIconMenu();
            }
            
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = true;
            }
        }
        
        private void SaveCustomProfile_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ApplyCustomSpeed();
        }
        
        private void NewProfile_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ApplyCustomSpeed();
        }
        
        private void AssignHotkey_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button != null && button.Tag is string profileName)
            {
                _viewModel.StartHotkeyAssignmentCommand.Execute(profileName);
            }
        }
        
        private void RemoveHotkey_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button != null && button.Tag is string profileName)
            {
                _viewModel.RemoveHotkeyCommand.Execute(profileName);
            }
        }
        
        private void RemoveProfile_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button != null && button.Tag is string profileName)
            {
                _viewModel.RemoveProfileCommand.Execute(profileName);
            }
        }
        
        private void CancelHotkeyAssignment_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.IsHotkeyCapturing = false;
        }

        private void Github_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/mre31/Fan-Control-Center-WPF",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Web page could not be opened: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _viewModel.Slider_DragCompleted();
        }

        private void NotifyIcon_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            RestoreWindow();
        }
        
        private void RestoreWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
            }
        }

        private void CloseApplication()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    try 
                    {
                        _notifyIcon.Visible = false;
                        _notifyIcon.Dispose();
                        _notifyIcon = null;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error disposing notify icon: {ex.Message}");
                    }
                }
                
                if (_viewModel != null)
                {
                    _viewModel.Cleanup();
                }
                
                try
                {
                    System.Windows.Application.Current.Shutdown();
                }
                catch (Exception)
                {
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"A critical error occurred while closing the application: {ex.Message}\n\nThe application will now exit.",
                    "Critical Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                Environment.Exit(1);
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
        
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
    
    public class ProfileNameDialog : Window
    {
        private System.Windows.Controls.TextBox _textBox;
        
        public string ProfileName { get; private set; }
        
        public ProfileNameDialog()
        {
            Title = "Profile Name";
            Width = 300;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            
            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var label = new System.Windows.Controls.TextBlock 
            { 
                Text = "Enter profile name:", 
                Margin = new Thickness(0, 0, 0, 5) 
            };
            grid.Children.Add(label);
            Grid.SetRow(label, 0);
            
            _textBox = new System.Windows.Controls.TextBox 
            { 
                Margin = new Thickness(0, 0, 0, 10) 
            };
            grid.Children.Add(_textBox);
            Grid.SetRow(_textBox, 1);
            
            var buttonPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right 
            };
            
            var okButton = new System.Windows.Controls.Button 
            { 
                Content = "OK", 
                Width = 75, 
                Margin = new Thickness(0, 0, 5, 0), 
                IsDefault = true 
            };
            okButton.Click += OkButton_Click;
            
            var cancelButton = new System.Windows.Controls.Button 
            { 
                Content = "Cancel", 
                Width = 75, 
                IsCancel = true 
            };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            
            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 2);
            
            Content = grid;
            
            Loaded += (s, e) => _textBox.Focus();
        }
        
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_textBox.Text))
            {
                MessageBox.Show("Please enter a profile name.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            ProfileName = _textBox.Text;
            DialogResult = true;
        }
    }
}
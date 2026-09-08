using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using GoogleDesktopWidget.Interop;
using GoogleDesktopWidget.Models;
using GoogleDesktopWidget.Services;
using Forms = System.Windows.Forms;

namespace GoogleDesktopWidget
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly CalendarService _calendarService;
        private readonly NotesService _notesService;
        private Forms.NotifyIcon? _trayIcon;

        public MainWindow()
        {
            InitializeComponent();

            _settingsService = new SettingsService();
            _calendarService = new CalendarService();
            _notesService = new NotesService();

            Left = _settingsService.Settings.WindowLeft;
            Top = _settingsService.Settings.WindowTop;

            Loaded += MainWindow_Loaded;
            SourceInitialized += MainWindow_SourceInitialized;
            Closing += MainWindow_Closing;
            LocationChanged += MainWindow_LocationChanged;

            _calendarService.OnEventsUpdated += OnCalendarEventsUpdated;
            _calendarService.OnSyncStateChanged += OnCalendarSyncStateChanged;
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            Win32Interop.HideFromAltTab(hwnd);
            ApplyGlassAndBackdrop();

            if (_settingsService.Settings.PinToDesktop)
            {
                Win32Interop.SendToBottom(hwnd);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitTrayIcon();
            ApplyStoredSettingsToUI();
            RefreshHeaderDate();

            BindTasksList();
            ScratchpadBox.Text = _notesService.Data.ScratchpadMarkdown;

            _calendarService.StartTimer(_settingsService.Settings.CalendarUrl, _settingsService.Settings.SyncIntervalMinutes);
        }

        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            _settingsService.Settings.WindowLeft = Left;
            _settingsService.Settings.WindowTop = Top;
            _settingsService.SaveSettings();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _trayIcon?.Dispose();
            _notesService.SaveImmediate();
            _settingsService.SaveSettings();
        }

        private void InitTrayIcon()
        {
            _trayIcon = new Forms.NotifyIcon();
            
            using var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.FromArgb(66, 133, 244));
                g.FillEllipse(System.Drawing.Brushes.White, 3, 3, 10, 10);
            }
            _trayIcon.Icon = System.Drawing.Icon.FromHandle(bmp.GetHicon());
            _trayIcon.Text = "Google Material Desktop Widget";
            _trayIcon.Visible = true;

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Open Widget", null, (s, e) => { Show(); WindowState = WindowState.Normal; Activate(); });
            menu.Items.Add("Refresh Calendar", null, async (s, e) => await _calendarService.RefreshCalendarAsync());
            menu.Items.Add("Reset Position", null, (s, e) => ResetPositionButton_Click(this, new RoutedEventArgs()));
            menu.Items.Add("-");
            menu.Items.Add("Exit", null, (s, e) => Application.Current.Shutdown());
            _trayIcon.ContextMenuStrip = menu;

            _trayIcon.DoubleClick += (s, e) => { Show(); Activate(); };
        }

        private void ApplyStoredSettingsToUI()
        {
            Opacity = _settingsService.Settings.Opacity;
            OpacitySlider.Value = _settingsService.Settings.Opacity;
            AcrylicCheck.IsChecked = _settingsService.Settings.EnableAcrylic;
            PinToDesktopCheck.IsChecked = _settingsService.Settings.PinToDesktop;
            AutoStartCheck.IsChecked = _settingsService.Settings.AutoStart;
            CalendarUrlBox.Text = _settingsService.Settings.CalendarUrl;

            switch (_settingsService.Settings.ThemeName)
            {
                case "EmeraldForest": ThemeComboBox.SelectedIndex = 1; break;
                case "SunsetCoral": ThemeComboBox.SelectedIndex = 2; break;
                case "AmoledBlack": ThemeComboBox.SelectedIndex = 3; break;
                default: ThemeComboBox.SelectedIndex = 0; break;
            }

            App.ApplyTheme(_settingsService.Settings.ThemeName);
        }

        private void ApplyGlassAndBackdrop()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (_settingsService.Settings.EnableAcrylic)
            {
                Win32Interop.EnableAcrylic(hwnd, 0x991E1F22);
            }
            else
            {
                Win32Interop.DisableAcrylic(hwnd);
            }
        }

        private void RefreshHeaderDate()
        {
            HeaderDateText.Text = DateTime.Now.ToString("dddd, MMM d");
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void OnCalendarEventsUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                var active = _calendarService.Events.FirstOrDefault(ev => ev.IsActiveNow);
                if (active != null)
                {
                    ActiveEventCard.Visibility = Visibility.Visible;
                    ActiveEventTitle.Text = active.Title;
                    ActiveEventTime.Text = active.DisplayTimeRange;
                }
                else
                {
                    ActiveEventCard.Visibility = Visibility.Collapsed;
                }

                EventsItemsControl.ItemsSource = null;
                EventsItemsControl.ItemsSource = _calendarService.Events;

                AgendaEmptyState.Visibility = _calendarService.Events.Count == 0 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            });
        }

        private void OnCalendarSyncStateChanged(bool isSyncing)
        {
            Dispatcher.Invoke(() =>
            {
                SyncStatusText.Text = isSyncing ? "Syncing events..." : "Updated just now";
            });
        }

        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            await _calendarService.RefreshCalendarAsync();
        }

        private void EventCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: CalendarEvent item })
            {
                item.IsExpanded = !item.IsExpanded;
                EventsItemsControl.Items.Refresh();
            }
        }

        private void TabAgendaRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (AgendaView == null || TasksNotesView == null) return;
            AgendaView.Visibility = Visibility.Visible;
            TasksNotesView.Visibility = Visibility.Collapsed;
        }

        private void TabTasksRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (AgendaView == null || TasksNotesView == null) return;
            AgendaView.Visibility = Visibility.Collapsed;
            TasksNotesView.Visibility = Visibility.Visible;
        }

        private void BindTasksList()
        {
            TasksItemsControl.ItemsSource = null;
            TasksItemsControl.ItemsSource = _notesService.Data.Tasks;
        }

        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            SubmitTask();
        }

        private void NewTaskBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SubmitTask();
            }
        }

        private void SubmitTask()
        {
            string text = NewTaskBox.Text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                _notesService.AddTask(text);
                NewTaskBox.Text = string.Empty;
                BindTasksList();
            }
        }

        private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox { Tag: string taskId })
            {
                _notesService.ToggleTask(taskId);
                BindTasksList();
            }
        }

        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string taskId })
            {
                _notesService.DeleteTask(taskId);
                BindTasksList();
            }
        }

        private void ScratchpadBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _notesService.UpdateScratchpad(ScratchpadBox.Text);
        }

        private void SettingsNavButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsModal.Visibility = Visibility.Visible;
        }

        private void CloseSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsModal.Visibility = Visibility.Collapsed;
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem item)
            {
                string chosen = item.Content.ToString() switch
                {
                    "Emerald Forest" => "EmeraldForest",
                    "Sunset Coral" => "SunsetCoral",
                    "AMOLED True Black" => "AmoledBlack",
                    _ => "GoogleBlue"
                };

                _settingsService.Settings.ThemeName = chosen;
                App.ApplyTheme(chosen);
                _settingsService.SaveSettings();
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Opacity = e.NewValue;
            _settingsService.Settings.Opacity = e.NewValue;
            _settingsService.SaveSettings();
        }

        private void AcrylicCheck_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.Settings.EnableAcrylic = AcrylicCheck.IsChecked ?? true;
            _settingsService.SaveSettings();
            ApplyGlassAndBackdrop();
        }

        private void PinToDesktopCheck_Click(object sender, RoutedEventArgs e)
        {
            bool pin = PinToDesktopCheck.IsChecked ?? false;
            _settingsService.Settings.PinToDesktop = pin;
            _settingsService.SaveSettings();

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (pin)
            {
                Win32Interop.SendToBottom(hwnd);
            }
        }

        private void AutoStartCheck_Click(object sender, RoutedEventArgs e)
        {
            bool autoStart = AutoStartCheck.IsChecked ?? false;
            _settingsService.Settings.AutoStart = autoStart;
            _settingsService.SaveSettings();
        }

        private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.Settings.CalendarUrl = CalendarUrlBox.Text.Trim();
            _settingsService.SaveSettings();

            SettingsModal.Visibility = Visibility.Collapsed;
            _calendarService.StartTimer(_settingsService.Settings.CalendarUrl, _settingsService.Settings.SyncIntervalMinutes);
            await _calendarService.RefreshCalendarAsync();
        }

        private void ResetPositionButton_Click(object sender, RoutedEventArgs e)
        {
            Left = 100;
            Top = 100;
            _settingsService.Settings.WindowLeft = 100;
            _settingsService.Settings.WindowTop = 100;
            _settingsService.SaveSettings();
        }
    }
}

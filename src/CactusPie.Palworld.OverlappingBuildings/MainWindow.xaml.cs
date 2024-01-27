using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Memory;
using NHotkey;
using NHotkey.Wpf;

namespace CactusPie.Palworld.OverlappingBuildings
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Mem GameMemory = new();

        private bool _buildingOverlappingAllowed;

        private string? _address;

        private readonly object _lock = new();

        private Task? _gameProcessSearchingTask;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            _gameProcessSearchingTask = WaitForGameProcess();
        }

        private static async Task<long?> GetAddressForForbiddenOverlapping()
        {
            IEnumerable<long>? addresses = await GameMemory
                .AoBScan("74 07 B0 14 E9 2F 01 00 00", true, true)
                .ConfigureAwait(false);

            long? address = addresses?.FirstOrDefault();
            return address;
        }

        private static async Task<long?> GetAddressForAllowedOverlapping()
        {
            IEnumerable<long>? addresses = await GameMemory
                .AoBScan("EB 07 B0 14 E9 2F 01 00 00", true, true)
                .ConfigureAwait(false);

            long? address = addresses?.FirstOrDefault();
            return address;
        }

        private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
        {
            if (GameMemory.mProc.Process.HasExited)
            {
                _buildingOverlappingAllowed = false;
                HotkeyManager.Current.Remove("ToggleOverlapping");
                MainLabel.Content = "Waiting for the game to start...";
                MainLabel.Foreground = new SolidColorBrush(Colors.Black);

                lock (_lock)
                {
                    _gameProcessSearchingTask = WaitForGameProcess();
                }

                return;
            }

            ToggleBuildingOverlapping();
            if (_buildingOverlappingAllowed)
            {
                MainLabel.Content = "Building overlapping: ALLOWED (F8 to toggle)";
                MainLabel.Foreground = new SolidColorBrush(Colors.DarkGreen);
            }
            else
            {
                MainLabel.Content = "Building overlapping: NOT ALLOWED (F8 to toggle)";
                MainLabel.Foreground = new SolidColorBrush(Colors.DarkRed);
            }
        }

        private void ToggleBuildingOverlapping()
        {
            _buildingOverlappingAllowed = !_buildingOverlappingAllowed;

            if (_buildingOverlappingAllowed)
            {
                GameMemory.WriteBytes(_address, new byte[]{ 0xEB, 0x07 });
            }
            else
            {
                GameMemory.WriteBytes(_address, new byte[]{ 0x74, 0x07 });
            }
        }

        private async Task WaitForGameProcess()
        {
            if (_gameProcessSearchingTask != null)
            {
                return;
            }

            while (true)
            {
                bool opened = GameMemory.OpenProcess("Palworld-Win64-Shipping.exe");

                if (!opened)
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                    continue;
                }

                Dispatcher.Invoke(
                    () =>
                    {
                        MainLabel.Content = "Searching for the correct memory address...";
                        MainLabel.Foreground = new SolidColorBrush(Colors.Black);
                    });

                long? address = await GetAddressForForbiddenOverlapping().ConfigureAwait(false);

                if (address is null or 0)
                {
                    address = await GetAddressForAllowedOverlapping().ConfigureAwait(false);
                }

                if (address is null or 0)
                {
                    Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                "Could not find the correct memory address.\n" +
                                "This could happen if you're not running the latest Steam version or a new game update broke the mod.\n" +
                                "The mod might not work with GamePass/Windows store version",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);

                            this.Close();
                        }
                    );

                    return;
                }

                _address = address.Value.ToString("X");

                Dispatcher.Invoke(
                    () =>
                    {
                        _buildingOverlappingAllowed = false;
                        HotkeyManager.Current.AddOrReplace("ToggleOverlapping", Key.F8, ModifierKeys.None, OnHotkeyPressed);
                        MainLabel.Content = "Building overlapping: NOT ALLOWED (F8 to toggle)";
                        MainLabel.Foreground = new SolidColorBrush(Colors.DarkRed);
                    });

                break;
            }

            _gameProcessSearchingTask = null;
        }
    }
}
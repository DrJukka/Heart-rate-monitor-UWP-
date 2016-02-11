/* Copyright (c) 2016 Microsoft Corporation. This software is licensed under the MIT License.
 * See the license file delivered with this project for further information.
 */
using System;
using System.Collections.Generic;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using Windows.Storage;
using HeartBeat.Model;
using HeartBeat.Engine;

namespace HeartBeat
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ReFreshDevicesList();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }

        private async void ReFreshDevicesList()
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                var devices = await DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(GattServiceUuids.HeartRate));
                var items = new List<DeviceViewModel>();

                if (devices != null && devices.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("FindAllAsync devices.Count : " + devices.Count);
                    foreach (DeviceInformation device in devices)
                    {
                        if (device != null)
                        {
                            System.Diagnostics.Debug.WriteLine("Found : " + device.Name + ", id: " + device.Id);
                            items.Add(new DeviceViewModel(device));
                        }
                    }
                }
                DeviceSelectionListView.ItemsSource = items;

                noDevicesLabel.Visibility = items.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
                DeviceSelectionListView.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void DeviceSelectionListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            HeartBeatEngine.Instance.SelectedDevice = (DeviceViewModel)e.ClickedItem;
            System.Diagnostics.Debug.WriteLine("Device " + HeartBeatEngine.Instance.SelectedDevice.Name + " selected, now navigating to HeartBeatPage");
            this.Frame.Navigate(typeof(HeartBeatPage));
        }

        private void RefreshDeviceList_Click(object sender, RoutedEventArgs e)
        {
            ReFreshDevicesList();
        }

        private async void openSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-bluetooth:"));
        }

        private void ShowAbout_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(AboutPage));
            //this.Frame.Navigate(typeof(HeartBeatPage), "simulator");
        }

        private async void OpenBeatFile_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".txt");

            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                // Application now has read/write access to the picked file
                System.Diagnostics.Debug.WriteLine("Picked file: " + file.Name);
                this.Frame.Navigate(typeof(ShowFileData), file);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Operation cancelled.");
            }
        }
    }
}

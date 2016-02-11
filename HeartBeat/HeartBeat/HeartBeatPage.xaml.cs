/* Copyright (c) 2016 Microsoft Corporation. This software is licensed under the MIT License.
 * See the license file delivered with this project for further information.
 */
using System;
using System.Collections.Generic;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using HeartBeat.Engine;
using HeartBeat.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace HeartBeat
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HeartBeatPage : Page
    {
        private bool _UseSimlator = false;

        public HeartBeatPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var parameter = e.Parameter as String;

            if (parameter != null && parameter.Equals("simulator"))
            {
                _UseSimlator = true;
            }
            else
            {
                _UseSimlator = false;
            }

            if (HeartBeatEngine.Instance.SelectedDevice == null && !_UseSimlator)
            {
                ShowErrorDialog("Please select device !", "No device selected");
                return;
            }

            SetWaitVisibility(true);

            progressIndicator.Text = "Connecting...";

            System.Diagnostics.Debug.WriteLine("OnNavigatedTo");

            chartControlOne.ResetChartData();
            chartControlOne.SaveButtonPressed += SaveButtonPressed;

            HeartBeatEngine.Instance.DeviceConnectionUpdated += Instance_DeviceConnectionUpdated;
            HeartBeatEngine.Instance.ValueChangeCompleted += Instance_ValueChangeCompleted;

            if (_UseSimlator)
            {
                HeartBeatEngine.Instance.StartSimulator(120, 80, 2, 90, true);
                System.Diagnostics.Debug.WriteLine("Simulator started.");
            }
            else {
                DeviceName.Text = HeartBeatEngine.Instance.SelectedDevice.Name;
                HeartBeatEngine.Instance.InitializeServiceAsync(HeartBeatEngine.Instance.SelectedDevice.Id, GattCharacteristicUuids.HeartRateMeasurement);
                System.Diagnostics.Debug.WriteLine("initialized.");
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("OnNavigatedFrom");

            chartControlOne.SaveButtonPressed -= SaveButtonPressed;
            HeartBeatEngine.Instance.DeviceConnectionUpdated -= Instance_DeviceConnectionUpdated;
            HeartBeatEngine.Instance.ValueChangeCompleted -= Instance_ValueChangeCompleted;
            HeartBeatEngine.Instance.Deinitialize();

            base.OnNavigatedFrom(e);
        }

        private async void SaveButtonPressed(ChartControlFull sender)
        {
            if (sender == chartControlOne)
            {
                string dataToSave = chartControlOne.getDataString();
                if (dataToSave == null)
                {
                    ShowErrorDialog("Chart returned no data, please try again later.", "No data to save");
                    return;
                }

                var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                // Dropdown of file types the user can save the file as
                savePicker.FileTypeChoices.Add("Plain Text", new List<string>() { ".txt" });
                // Default file name if the user does not type one in or select a file to replace
                savePicker.SuggestedFileName = "HeartBeat";

                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    CachedFileManager.DeferUpdates(file);
                    await FileIO.WriteTextAsync(file, dataToSave);

                    Windows.Storage.Provider.FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                    if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                    {
                        ShowErrorDialog("File " + file.Name + " was saved.", "Save to file");
                    }
                    else
                    {
                        ShowErrorDialog("File " + file.Name + " couldn't be saved.", "Save to file");
                    }
                }
            }
        }

        private async void Instance_DeviceConnectionUpdated(bool isConnected, string error)
        {
            // Serialize UI update to the the main UI thread.
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (error != null)
                {
                    ShowErrorDialog(error, "Connect error.");
                }

                if (isConnected)
                {
                    progressIndicator.Text = "Waiting for data...";
                }
            });
        }

        private async void Instance_ValueChangeCompleted(HeartbeatMeasurement HeartbeatMeasurementValue)
        {
            System.Diagnostics.Debug.WriteLine("got heartbeat : " + HeartbeatMeasurementValue.HeartbeatValue);

            // Serialize UI update to the the main UI thread.
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (progressGrid.Visibility == Visibility.Visible)
                {
                    SetWaitVisibility(false);
                }

                chartControlOne.AddChartData(HeartbeatMeasurementValue);
            });
        }

        private void SetWaitVisibility(bool waitVisible)
        {
            progressGrid.Visibility = waitVisible ? Visibility.Visible : Visibility.Collapsed;
            valuesGrid.Visibility = waitVisible ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void ShowErrorDialog(string message, string title)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                var dialog = new MessageDialog(message, title);
                await dialog.ShowAsync();
            });
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            chartControlOne.ShowAllChartData();
        }
    }
}

/* Copyright (c) 2016 Microsoft Corporation. This software is licensed under the MIT License.
 * See the license file delivered with this project for further information.
 */
using HeartBeat.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace HeartBeat
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ShowFileData : Page
    {
        public ShowFileData()
        {
            this.InitializeComponent();
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            StorageFile parameter = e.Parameter as StorageFile;
            if (parameter == null)
            {
                ShowErrorDialog("No file defined for showing, please try again.", "Fine not defined");
            }

            System.Diagnostics.Debug.WriteLine("Attempting to read : + " + parameter.Name);

            FileName.Text = parameter.Name;

            string valuesString = await FileIO.ReadTextAsync(parameter);
            string[] valuesArray = valuesString.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
           
            System.Diagnostics.Debug.WriteLine("valuesArray Count : " + valuesArray.Count());

            int intValue;
            
            List<HeartbeatMeasurement> tmpData = new List<HeartbeatMeasurement>();
            foreach (string value in valuesArray)
            {
                if (Int32.TryParse(value, out intValue))
                {
                    tmpData.Add(HeartbeatMeasurement.GetHeartbeatMeasurementFromData((ushort)intValue, DateTimeOffset.Now));
                }
            }

            System.Diagnostics.Debug.WriteLine("adding data count : " + tmpData.Count);

            chartControlOne.PlotChart(tmpData.ToArray());
        }

        private async void ShowErrorDialog(string message, string title)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                var dialog = new MessageDialog(message, title);
                await dialog.ShowAsync();
            });
        }
    }
}

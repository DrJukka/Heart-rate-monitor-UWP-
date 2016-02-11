/* Copyright (c) 2016 Microsoft Corporation. This software is licensed under the MIT License.
 * See the license file delivered with this project for further information.
 */
using HeartBeat.Engine;
using System;
using System.Collections.Generic;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HeartBeat.Controls
{
    public delegate void SaveButtonPressed(ChartControlFull sender);

    public sealed partial class ChartControlFull : UserControl
    {
        private List<HeartbeatMeasurement> _data;

        //minimum visible point, defines minimun zoon level
        private const int MIN_VISIBLE_POINTS = 10;
        public int ChartDataPoints
        {
            get
            {
                SetValue(ChartDataPointsProperty, outputDataChart.DataPointCount);
                return (int)GetValue(ChartDataPointsProperty);
            }
            set
            {
                if (MIN_VISIBLE_POINTS <= value)
                {
                    outputDataChart.DataPointCount = value;
                    SetValue(ChartDataPointsProperty, outputDataChart.DataPointCount);
                }
            }
        }

        public SaveButtonPressed SaveButtonPressed;

        public bool IsSaveEnabled
        {
            get { return (bool)GetValue(IsSaveEnabledProperty); }
            set { SetValue(IsSaveEnabledProperty, value); }
        }

        public bool IsZoomEnabled
        {
            get { return (bool)GetValue(IsZoomEnabledProperty); }
            set { SetValue(IsZoomEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsZoomEnabledProperty =
            DependencyProperty.Register("IsZoomEnabled ", typeof(bool), typeof(ChartControlFull), new PropertyMetadata(false));

        public static readonly DependencyProperty IsSaveEnabledProperty =
            DependencyProperty.Register("IsSaveEnabled", typeof(bool), typeof(ChartControlFull), new PropertyMetadata(false));

        public static readonly DependencyProperty ChartDataPointsProperty =
            DependencyProperty.Register("ChartDataPoints", typeof(int), typeof(ChartControlFull), new PropertyMetadata(false));

        public ChartControlFull()
        {
            this.InitializeComponent();
            _data = new List<HeartbeatMeasurement>();
        }

        public void ShowAllChartData()
        {
            if(_data == null)
            {
                return;
            }
            ChartDataPoints = _data.Count;
        }
        public void ResetChartData()
        {
            HeartbeatValueBox.Text = "";
            _data.Clear();
            outputDataChart.PlotChart(_data.ToArray());
        }

        public void AddChartData(HeartbeatMeasurement HeartbeatMeasurementValue)
        {
            // lets put the value into the UI control
            HeartbeatValueBox.Text = "" + HeartbeatMeasurementValue.HeartbeatValue;

            //we need to store it in an array in order to visualize the values with graph
            _data.Add(HeartbeatMeasurementValue);

            ReDrawChart();
        }

        private void ReDrawChart()
        {
            //if we have full view of data, we'll enable the saving
            IsSaveEnabled = (ChartDataPoints < _data.Count);

            //we don't allow less than 10 datapoints in chart view
            zoomInButton.Visibility = (ChartDataPoints <= 10) ? Visibility.Collapsed : Visibility.Visible;

            if (_data.Count >= 2)
            {
                // and we have our custom control to show the graph
                // this does not draw well if there is only one value, thus using it only after we got at least two values
                outputDataChart.PlotChart(_data.ToArray());
            }
        }

        //the returned data can be saved directly to a file
        public string getDataString()
        {
            int startindex = _data.Count - ChartDataPoints;
            if (startindex < 0)
            {
                return null;
            }

            string dataToSave = "" + _data[startindex].HeartbeatValue;

            for (int i = (startindex + 1); i < _data.Count; i++)
            {
                dataToSave = dataToSave + "," + _data[i].HeartbeatValue;
            }

            return dataToSave;
        }

        private void zoomButton_Click(object sender, RoutedEventArgs e)
        {
            int visiblePoints = ChartDataPoints;

            int change = 5;
            if (visiblePoints > 100)
            {
                change = (visiblePoints / 10);
            }

            visiblePoints = (sender == zoomOutButton) ? visiblePoints + change : visiblePoints - change;

            if (visiblePoints > _data.Count)
            {
                visiblePoints = _data.Count;
            }

            if (visiblePoints <= MIN_VISIBLE_POINTS)
            {
                visiblePoints = MIN_VISIBLE_POINTS;
            }

            ChartDataPoints = visiblePoints;
            updateChartWithTimer();
        }

        private void saveChartDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (SaveButtonPressed != null)
            {
                SaveButtonPressed(this);
            }
        }
        private ThreadPoolTimer _zoomDelayTimer = null;
        private void updateChartWithTimer()
        {
            if (_zoomDelayTimer != null)
            {
                _zoomDelayTimer.Cancel();
                _zoomDelayTimer = null;
            }

            _zoomDelayTimer = ThreadPoolTimer.CreateTimer(
            async (source) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                {
                    ReDrawChart();
                });

            }, TimeSpan.FromMilliseconds(300));
        }
    }
}


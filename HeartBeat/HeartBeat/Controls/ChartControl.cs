/* Copyright (c) 2016 Microsoft Corporation. This software is licensed under the MIT License.
 * See the license file delivered with this project for further information.
 */

using HeartBeat.Engine;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace HeartBeat.Controls
{
    public sealed class ChartControl : Canvas
    {
        // default values for min & max on graph
        // will be extended if data does not fit into teh range
        private const int MIN_VALUE_DEFAULT = 70;
        private const int MAX_VALUE_DEFAULT = 100;
        // how many points are visible be default
        private const int DEFAULT_DATAPOINTS = 60;

        //how many vertical lines we have
        private const int DEFAULT_GRADIENTS = 5;
        //and the color for them
        private Color LINES_COLOR = Color.FromArgb(0x55, 0x00, 0x00, 0x00);

        //color for the value texts on the left side
        private Color VALUESTEXT_COLOR = Color.FromArgb(0xFF, 0x00, 0x00, 0x00);
        //and the font size for it
        private const int VALUESTEXT_FONTSIZE = 22;
        //margin for the text from left side
        private const int RIGHT_TEXT_MARGIN = 9;
        //bottom margin for the text & line
        private const int BOTTOM_TEXT_MARGIN = 24;

        // the width of the graph line
        private const int GRAPH_STROKETHICKNESS = 2;
        // and the color for it
        private Color GRAPG_COLOR = Colors.Red;

        //Background color for the graph (Do remember set the same value in XAML to avoid flickering)
        private Color BACKGROUND_COLOR = Colors.LightBlue;

        // Private members
        private HeartbeatMeasurement[] _data = null;
        private RenderingOptions _renderingOptions = null;
        private List<DataPoint> _offsetList = null;

        // Number of data points the chart displays
        public int DataPointCount { get; set; }

        public ChartControl()
        {
            this.DataPointCount = DEFAULT_DATAPOINTS;
            Background = new SolidColorBrush(BACKGROUND_COLOR);
            DrawBackground();
        }

        public void PlotChart(HeartbeatMeasurement[] data)
        {
            // First set the data points that we are going to render
            // The functions will use this data to plot the chart
            _data = data;

            //then do the full drawing
            DrawAll();
        }

        private void DrawAll()
        {
            if (this.ActualHeight == 0 || this.ActualWidth == 0
               || _data == null)
            {
                return;
            }
            // Remove previous rendering
            this.Children.Clear();

            //rendering options are calculating min & max values & value texts
            //the values are calculated on the visible area
            //return value defined the first value to show
            int startIndex = CreateRenderingOptions();

            //Draw horizontal lines, and value texts            
            DrawBackground();

            // Preprocess the data for rendering
            FillOffsetList(startIndex);

            // Render the actual chart
            DrawChart();
        }

        private int CreateRenderingOptions()
        {
            int startIndex = 0;
            _renderingOptions = null;
            if (_data != null)
            {
                _renderingOptions = new RenderingOptions();
                _renderingOptions.MinValue = double.MaxValue;
                _renderingOptions.MaxValue = double.MinValue;

                startIndex = (_data.Length  < DataPointCount) ? 0 : _data.Length - DataPointCount;

                System.Diagnostics.Debug.WriteLine("startIndex : " + startIndex + ", DataPointCount : " + DataPointCount + ", _data.Length : " + _data.Length);

                for (int i = startIndex; i < _data.Length; i++)
                {
                    _renderingOptions.MinValue = Math.Min(_data[i].HeartbeatValue, _renderingOptions.MinValue);
                    _renderingOptions.MaxValue = Math.Max(_data[i].HeartbeatValue, _renderingOptions.MaxValue);
                }

                if (_renderingOptions.MinValue > MIN_VALUE_DEFAULT)
                {
                    _renderingOptions.MinValue = MIN_VALUE_DEFAULT;
                }

                if (_renderingOptions.MaxValue < MAX_VALUE_DEFAULT)
                {
                    _renderingOptions.MaxValue = MAX_VALUE_DEFAULT;
                }

                var valueDiff = _renderingOptions.MaxValue - _renderingOptions.MinValue;
               
                var diffBuffer = (valueDiff > 0) ? (valueDiff * 0.1) : 2;
                _renderingOptions.MaxValueBuffered = _renderingOptions.MaxValue + diffBuffer;
                _renderingOptions.MinValueBuffered = _renderingOptions.MinValue - diffBuffer;
                _renderingOptions.MinValueBuffered = (_renderingOptions.MinValueBuffered > 0) ? _renderingOptions.MinValueBuffered : 0;
            }

            return startIndex;
        }

        private void FillOffsetList(int startIndex)
        {
            _offsetList = null;

            if (_data == null || _data.Length <= 0 || _renderingOptions == null)
            {
                return;
            }

            _offsetList = new List<DataPoint>();

            var valueDiff = _renderingOptions.MaxValue - _renderingOptions.MinValue;

            // Calculate the number of data points used
            var pointsDisplayed = (_data.Length > DataPointCount) ? DataPointCount : _data.Length;

            double tickOffset = (ActualWidth  / pointsDisplayed);
            double currentOffset = 0;

            for (int i = startIndex; i < (startIndex + pointsDisplayed); i++)
            {
                if (i < _data.Length)
                {
                    var currentDiff = _renderingOptions.MaxValueBuffered - _data[i].HeartbeatValue;

                    _offsetList.Add(new DataPoint
                    {
                        OffsetX = currentOffset,
                        OffsetY = (currentDiff / valueDiff) * ActualHeight,
                        Value = _data[i].HeartbeatValue
                    });
                    currentOffset += tickOffset;
                }
            }
        }

        private void DrawBackground()
        {
            var tickOffsetY = this.ActualHeight / DEFAULT_GRADIENTS;
            var currentOffsetY = 0.0;
            for (int i = 0; i < (DEFAULT_GRADIENTS + 1); i++)
            {
                Line line = new Line()
                {
                    X1 = 0,
                    Y1 = 0,
                    X2 = this.ActualWidth,
                    Y2 = 0,
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeDashArray = new DoubleCollection() { 5 }
                };

                this.Children.Add(line);
                SetLeft(line, 0);
                SetTop(line, currentOffsetY);
                currentOffsetY += tickOffsetY;
            }

            //Draw value texts
            DrawYAxis();
        }

        private void DrawYAxis()
        {
            if(_renderingOptions == null)
            {
                return;
            }

            TextBlock text = new TextBlock();
            text.FontSize = VALUESTEXT_FONTSIZE;
            text.Foreground = new SolidColorBrush(LINES_COLOR);
            text.Text = _renderingOptions.MaxValueBuffered.ToString("#.#");
            this.Children.Add(text);
            text.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Right;

            var percent = (_renderingOptions.MaxValueBuffered - _renderingOptions.MinValueBuffered) * (1.0 / (DEFAULT_GRADIENTS));
            SetTop(text, 2);

            for (int i = 1; i < DEFAULT_GRADIENTS; i++)
            {
                var percentVal = _renderingOptions.MaxValueBuffered - (percent * i);

                text = new TextBlock();
                text.FontSize = VALUESTEXT_FONTSIZE;
                text.Foreground = new SolidColorBrush(LINES_COLOR);
                text.Text = percentVal.ToString("#.#");
                text.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Right;
                this.Children.Add(text);
                SetTop(text, (i * (ActualHeight / DEFAULT_GRADIENTS)) - RIGHT_TEXT_MARGIN);
            }

            text = new TextBlock();
            text.FontSize = VALUESTEXT_FONTSIZE;
            text.Foreground = new SolidColorBrush(LINES_COLOR);
            text.Text = _renderingOptions.MinValueBuffered.ToString("#.#");
            text.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Right;
            this.Children.Add(text);
            SetTop(text, ActualHeight - BOTTOM_TEXT_MARGIN);
        }

        private void DrawChart()
        {
            if (_offsetList == null || _offsetList.Count <= 0)
            {
                return;
            }

            var path = new Windows.UI.Xaml.Shapes.Path();

            path.Stroke = new SolidColorBrush(GRAPG_COLOR);
            path.StrokeThickness = GRAPH_STROKETHICKNESS;
            path.StrokeLineJoin = PenLineJoin.Round;
            path.StrokeStartLineCap = PenLineCap.Round;
            path.StrokeEndLineCap = PenLineCap.Round;

            var geometry = new PathGeometry();

            var figure = new PathFigure();
            figure.IsClosed = false;
            figure.StartPoint = new Point(_offsetList[0].OffsetX, _offsetList[0].OffsetY);

            for (int i = 0; i < _offsetList.Count; i++)
            {
                var segment = new LineSegment();
                segment.Point = new Point(_offsetList[i].OffsetX, _offsetList[i].OffsetY);
                figure.Segments.Add(segment);
            }
            geometry.Figures.Add(figure);
            path.Data = geometry;
            Children.Add(path);
        }
    }
}

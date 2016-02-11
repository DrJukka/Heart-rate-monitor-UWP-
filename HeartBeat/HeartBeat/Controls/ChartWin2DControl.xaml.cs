/* Copyright (c) 2016 Microsoft Corporation. This software is licensed under the MIT License.
 * See the license file delivered with this project for further information.
 */
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Canvas;
using System.Numerics;
using Microsoft.Graphics.Canvas.Geometry;
using HeartBeat.Engine;

// this control is using Win2D API, and requires the nuget package being installed
// more information see: http://microsoft.github.io/Win2D/html/Introduction.htm 

namespace HeartBeat.Controls
{
    public sealed partial class ChartWin2DControl : UserControl
    {
        // default values for min & max on graph
        // will be extended if data does not fit into teh range
        private const int MIN_VALUE_DEFAULT = 70;
        private const int MAX_VALUE_DEFAULT = 100;

        //how many vertical lines we have
        private const int DEFAULT_GRADIENTS = 5;
        //and the color for them
        private Color LINES_COLOR = Color.FromArgb(0x55, 0x00, 0x00, 0x00);
        //color for the value texts on the left side
        private Color VALUESTEXT_COLOR = Color.FromArgb(0xFF, 0x00, 0x00, 0x00);
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

        //treshold value for y movement to start zoom action
        private const int YTRESHOLD_FOR_ZOOM = 10;
        //treshold value for x movement to start move action
        private const int XTRESHOLD_FOR_MOVE = 30;

        //used to determine pointer changes for move & zoom actions
        private Point _pointerDown;

        private enum MoveDirection
        {
            Done,           //we are done
            Determining,    //we monitoring values untill we reach one of the treshold values
            Horizontal,     //we are monitoring horizontal finger movement (moving)
            Vertical        //we are monitoring vertical finger movement (zooming)
        }
        //this is used to store information on our current move & zoom action state
        private MoveDirection _currentDirection = MoveDirection.Done;

        //zoom action changes this value according to current zoom level
        private int _zoomFactor = 100;
        //defines minimun value for the zoom level
        private int _minZoomFactor = 5;

        //Source react from graph to draw (zoom efect is handled via this)
        private Size _graphDrawingSource;
        // the start point from which the image Rect is drawn from (move efect is handled via this)
        private Point _graphDrawingPoint;

        //off screen canvas for the background, lines & value texts
        private CanvasRenderTarget _offscreenBackGround = null;
        //off screen canvas for the graph
        private CanvasRenderTarget _offscreenChartImage = null;

        //stored last data to be used in size changed events
        private HeartbeatMeasurement[] _data = null;
        public ChartWin2DControl()
        {
            this.InitializeComponent();

            //this will start the determining state
            chartGrid.PointerPressed += ChartGrid_PointerPressed;
            // will first wait untill movement goes over set treshhold 
            // to determine action, then starts either zooming or moving action
            chartGrid.PointerMoved += ChartGrid_PointerMoved;
            //any of these will stop the action
            chartGrid.PointerReleased += ChartGrid_moveZoomDone;
            chartGrid.PointerCanceled += ChartGrid_moveZoomDone;
            chartGrid.PointerCaptureLost += ChartGrid_moveZoomDone;
            chartGrid.PointerExited += ChartGrid_moveZoomDone;
        }

        // this is called externally to give us the data for the graph
        public void PlotChart(HeartbeatMeasurement[] data)
        {
            _data = data;
            DrawChart();
        }

        private void chartGrid_SizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e)
        {
            //re-calculate & draw the graph when orientation changes
            DrawChart();
        }
        public void DrawChart()
        {
            if (_data == null || _data.Length <= 0)
            {
                return;
            }
            CanvasDevice device = CanvasDevice.GetSharedDevice();

            //rendering options are calculating min & max values & value texts
            RenderingOptions renderingOptions = CreateRenderingOptions(_data);

            //_offscreenBackGround is created in here
            DrawBackGround(device, renderingOptions);
            //_offscreenChartImage is created in here
            DrawCharData(device, renderingOptions, _data);

            //forces re-draw
            ChartWin2DCanvas.Invalidate();
        }
        private void ChartGrid_PointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_currentDirection == MoveDirection.Done)
            {
                //no down event, since last release, so we do nothing here
                return;
            }
            Point currentPoint = e.GetCurrentPoint(chartGrid).Position;
            double changeX = _pointerDown.X - currentPoint.X;
            double changeY = _pointerDown.Y - currentPoint.Y;
            e.Handled = true;

            // if we have not reached any teshold, we'll wait untill we reach one of them
            if (_currentDirection == MoveDirection.Determining)
            {
                if (changeX > XTRESHOLD_FOR_MOVE
                 || changeX < -XTRESHOLD_FOR_MOVE)
                {
                    _currentDirection = MoveDirection.Horizontal;
                }
                else if (changeY > YTRESHOLD_FOR_ZOOM
                      || changeY < -YTRESHOLD_FOR_ZOOM)
                {
                    _currentDirection = MoveDirection.Vertical;
                }
                //before we get to the treshold, we don't do anything
                // and once we do, we don't want to start the action with huge jumpstart
                //thus we ignore the treshold
                return;
            }

            //handle the action according to the pointer change
            MoveZoom(changeX, changeY);
            //Reset the pointer
            _pointerDown = currentPoint;
        }

        private void ChartGrid_moveZoomDone(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            //move / zoom action has ended
            _currentDirection = MoveDirection.Done;
            e.Handled = true;
        }

        private void ChartGrid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // we start new action cycle by entering teh state where we first determine which action is starting
            _currentDirection = MoveDirection.Determining;
            _pointerDown = e.GetCurrentPoint(chartGrid).Position;
            e.Handled = true;
        }

        private void MoveZoom(double changeX, double changeY)
        {
            if (_currentDirection == MoveDirection.Done
             || _currentDirection == MoveDirection.Determining
             || _offscreenChartImage == null)
            {
                //Nothing will change on drawing
                return;
            }

            if (_currentDirection == MoveDirection.Horizontal)
            {
                if (changeX == 0)
                {
                    //Nothing will change on drawing
                    return;
                }
                //we simply move the start point according to finger movement
                _graphDrawingPoint.X += changeX;

                //and make sure that we don't go over the actual beginning point
                if (_graphDrawingPoint.X < 0)
                {
                    _graphDrawingPoint.X = 0;
                }
            }
            else if (_currentDirection == MoveDirection.Vertical)
            {
                if (changeY == 0)
                {
                    //Nothing will change on drawing
                    return;
                }

                // use different divider to adjust the zoomefect
                _zoomFactor += (int)(changeY / 2);

                //and lets keep the zoom level in defined range
                if (_zoomFactor < _minZoomFactor)
                {
                    _zoomFactor = _minZoomFactor;
                }
                else if (_zoomFactor > 100)
                {
                    _zoomFactor = 100;
                }

                //zoom efect simply changes the width part of the size what we draw
                _graphDrawingSource = new Size(((_offscreenChartImage.Size.Width * _zoomFactor) / 100), _offscreenChartImage.Size.Height);
            }

            double maxChange = _offscreenChartImage.Size.Width - _graphDrawingSource.Width;
            //the max value changes on zoom, and move value in move
            //so we need to check the max value for movement after each action
            if (_graphDrawingPoint.X > maxChange)
            {
                _graphDrawingPoint.X = maxChange;
            }

            //invalidate forces re-draw
            ChartWin2DCanvas.Invalidate();
        }

        private void ChartWin2DCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_offscreenBackGround != null)
            {
                //background with lines and value texts
                args.DrawingSession.DrawImage(_offscreenBackGround, new Rect(new Point(0, 0), ChartWin2DCanvas.Size), new Rect(new Point(0, 0), _offscreenBackGround.Size));
                if (_offscreenChartImage != null)
                {
                    //the actual graph is drawn in here, according to move & zoom values
                    args.DrawingSession.DrawImage(_offscreenChartImage, new Rect(new Point(0, 0), ChartWin2DCanvas.Size), new Rect(_graphDrawingPoint, _graphDrawingSource));
                }
            }
            else {
                // in the start we don't have data, so we'll just draw the background rect with background color
                args.DrawingSession.DrawRectangle(0, 0, (float)ChartWin2DCanvas.Size.Width, (float)ChartWin2DCanvas.Size.Height, BACKGROUND_COLOR);
            }
        }

        private void DrawBackGround(CanvasDevice device, RenderingOptions options)
        {
            float useHeight = (float)ChartWin2DCanvas.Size.Height;
            float useWidth = (float)ChartWin2DCanvas.Size.Width;

            //this is always drawn in the size of control, so all texts drawn in it are sharp
            _offscreenBackGround = new CanvasRenderTarget(device, useWidth, useHeight, 96);

            using (CanvasDrawingSession ds = _offscreenBackGround.CreateDrawingSession())
            {
                ds.Clear(BACKGROUND_COLOR);
                //draw lines
                DrawGraphValueLines(ds, useWidth, useHeight);
                //draw value texts
                DrawYAxisTexts(ds, useHeight, options);
            }
        }

        private void DrawCharData(CanvasDevice device, RenderingOptions options, HeartbeatMeasurement[] data)
        {
            //Size restrictions descriped in : http://microsoft.github.io/Win2D/html/P_Microsoft_Graphics_Canvas_CanvasDevice_MaximumBitmapSizeInPixels.htm 
            float useHeight = (float)ChartWin2DCanvas.Size.Height > device.MaximumBitmapSizeInPixels ? device.MaximumBitmapSizeInPixels : (float)ChartWin2DCanvas.Size.Height;
            float useWidth = data.Length > device.MaximumBitmapSizeInPixels ? device.MaximumBitmapSizeInPixels : data.Length;

            //this will change the values array to array with drawing-line-points for the graph
            List<DataPoint> dataList = FillOffsetList(data, options, useWidth, useHeight);

            //reset zoom & moving values
            _zoomFactor = 100;
            _graphDrawingPoint = new Point(0, 0);
            _graphDrawingSource = new Size(useWidth, useHeight);
            //create the graph image
            _offscreenChartImage = new CanvasRenderTarget(device, useWidth, useHeight, 96);

            using (CanvasDrawingSession ds = _offscreenChartImage.CreateDrawingSession())
            {
                //This creates drawing geometry from the drawing-line-points
                CanvasGeometry chart = getDrawChartGeometry(device, dataList);
                //and then we simply draw it with defined color
                ds.DrawGeometry(chart, 0, 0, GRAPG_COLOR);
            }
        }

        private RenderingOptions CreateRenderingOptions(HeartbeatMeasurement[] dataSet)
        {
            RenderingOptions renderingOptions = null;
            if (dataSet != null)
            {
                renderingOptions = new RenderingOptions();
                //set initial end-of-range values 
                renderingOptions.MinValue = double.MaxValue;
                renderingOptions.MaxValue = double.MinValue;

                //find if we have bigger or smaller than what we have with current values
                for (int i = 0; i < dataSet.Length; i++)
                {
                    renderingOptions.MinValue = Math.Min(dataSet[i].HeartbeatValue, renderingOptions.MinValue);
                    renderingOptions.MaxValue = Math.Max(dataSet[i].HeartbeatValue, renderingOptions.MaxValue);
                }

                //and see if default values are more suitable
                if (renderingOptions.MinValue > MIN_VALUE_DEFAULT)
                {
                    renderingOptions.MinValue = MIN_VALUE_DEFAULT;
                }

                if (renderingOptions.MaxValue < MAX_VALUE_DEFAULT)
                {
                    renderingOptions.MaxValue = MAX_VALUE_DEFAULT;
                }

                var valueDiff = renderingOptions.MaxValue - renderingOptions.MinValue;
                var diffBuffer = (valueDiff > 0) ? (valueDiff * 0.1) : 2;
                //values used with value texts
                renderingOptions.MaxValueBuffered = renderingOptions.MaxValue + diffBuffer;
                renderingOptions.MinValueBuffered = renderingOptions.MinValue - diffBuffer;
                renderingOptions.MinValueBuffered = (renderingOptions.MinValueBuffered > 0) ? renderingOptions.MinValueBuffered : 0;
            }

            return renderingOptions;
        }

        //find where in graph image each values would correspond to and add this info to a new array
        private List<DataPoint> FillOffsetList(HeartbeatMeasurement[] dataSet, RenderingOptions options, float Width, float Height)
        {
            if (dataSet == null || dataSet.Length <= 0)
            {
                return null;
            }

            var valueDiff = options.MaxValue - options.MinValue;
            float tickOffset = (Width / dataSet.Length);

            List<DataPoint> offsetList = new List<DataPoint>();

            float currentOffset = 0;

            for (int i = 0; i < dataSet.Length; i++)
            {
                var currentDiff = options.MaxValue - dataSet[i].HeartbeatValue;

                offsetList.Add(new DataPoint
                {
                    OffsetX = currentOffset,
                    OffsetY = (currentDiff / valueDiff) * Height,
                    Value = dataSet[i].HeartbeatValue //just in case we would have functionality to show the actual value, we'll store  it here
                });
                currentOffset += tickOffset;
            }

            return offsetList;
        }

        //Drawn the background horizontal lines
        private void DrawGraphValueLines(CanvasDrawingSession ds, float width, float height)
        {
            var tickOffsetY = height / DEFAULT_GRADIENTS;
            float currentOffsetY = 0;

            for (int i = 0; i < (DEFAULT_GRADIENTS + 1); i++)
            {
                float x0 = 0;
                float y0 = currentOffsetY;
                float x1 = width;
                float y1 = currentOffsetY;

                ds.DrawLine(x0, y0, x1, y1, LINES_COLOR); // add CanvasStrokeStyle
                currentOffsetY += tickOffsetY;
            }
        }

        //draws the value texts
        private void DrawYAxisTexts(CanvasDrawingSession ds, float height, RenderingOptions options)
        {
            // if needed do add CanvasTextFormat 
            ds.DrawText(options.MaxValueBuffered.ToString("#.#"), new Vector2(RIGHT_TEXT_MARGIN, 0), VALUESTEXT_COLOR);
            var percent = (options.MaxValueBuffered - options.MinValueBuffered) * (1.0 / (DEFAULT_GRADIENTS));

            for (int i = 1; i < DEFAULT_GRADIENTS; i++)
            {
                var percentVal = options.MaxValueBuffered - (percent * i);
                // do add CanvasTextFormat 
                ds.DrawText(percentVal.ToString("#.#"), new Vector2(RIGHT_TEXT_MARGIN, (i * (height / DEFAULT_GRADIENTS))), VALUESTEXT_COLOR);
            }

            // do add CanvasTextFormat 
            ds.DrawText(options.MinValueBuffered.ToString("#.#"), new Vector2(RIGHT_TEXT_MARGIN, (height - BOTTOM_TEXT_MARGIN)), VALUESTEXT_COLOR);
        }

        // changes the drawing points in graph to be actual graph geometry item which can be used for drawing
        private CanvasGeometry getDrawChartGeometry(CanvasDevice device, List<DataPoint> offsetList)
        {
            if (offsetList == null || offsetList.Count <= 0)
            {
                return null;
            }

            CanvasPathBuilder pathBuilder = new CanvasPathBuilder(device);

            //start with first point
            pathBuilder.BeginFigure((float)offsetList[0].OffsetX, (float)offsetList[0].OffsetY);

            for (int i = 0; i < offsetList.Count; i++)
            {   // add line to the next point in the offset list
                pathBuilder.AddLine((float)offsetList[i].OffsetX, (float)offsetList[i].OffsetY);
            }
            //end it with open loop, we are not closed geometry object but just a chart-graph
            pathBuilder.EndFigure(CanvasFigureLoop.Open);

            return CanvasGeometry.CreatePath(pathBuilder);
        }
    }
}


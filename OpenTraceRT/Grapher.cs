using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenTraceRT {
    class Grapher {

        private Canvas _canvas;
        private double height;

        float cWidth = 5;
        float txtSize = 2.5f;

        private List<int> history = new List<int>();
        private int offset = 0;
        private double[] startDraw = new double[2];
        private int maxRange = 600;
        

        public Grapher(Canvas canvas) {
            _canvas = canvas;
            _canvas.Name = "latencyGraph";
        }


        public void SetHeight(double input) {
            height = input;
        }


        public void SetGraphSizing(float columnWidth, float textSize) {
            cWidth = columnWidth;
            txtSize = textSize;
        }


        public void BuildGraph(String latency, decimal packetLoss, string endIP, bool IsRebuild = false) {


            int total = 0;

            if (latency != "*") {
                total = Convert.ToInt32(latency);
            }     

            //Column
            Rectangle rect = new Rectangle();
            rect.Fill = new SolidColorBrush(Colors.SlateGray);
            rect.Width = cWidth;

            double margin = 0.9;
            double rectHeight = (height * ((double)total / maxRange) * margin);

            if (rectHeight != 0 && rectHeight < 5) { rectHeight = 5; }
            rect.Height = rectHeight;

            _canvas.Children.Add(rect);
            Canvas.SetBottom(rect, 0);
            Canvas.SetLeft(rect, cWidth * offset);

            //Total Latency text
            if (txtSize >= 5) {
                TextBlock txt = new TextBlock();
                txt.Text = total.ToString();
                txt.FontSize = txtSize;
                txt.TextAlignment = System.Windows.TextAlignment.Center;

                _canvas.Children.Add(txt);
                Canvas.SetBottom(txt, rectHeight);
                Canvas.SetLeft(txt, (cWidth * offset) + ((cWidth / 2) - ((txt.ActualWidth / 2) + 4)) );
            }            

            float xOffset = (offset * cWidth) + (cWidth / 2);

            if (IsRebuild) {

                startDraw[0] = xOffset;
                startDraw[1] = height * ((double)packetLoss / 100 );
            }

            DrawLine(xOffset, height * ((double)packetLoss / 100));

            startDraw[0] = xOffset;
            startDraw[1] = height * ((double)packetLoss / 100);

            if (offset == 0 || IsRebuild) {

                DrawScale();
            }            

            offset++;            
        }


        private void DrawLine(double endX, double endY) {

            Line line = new Line();
            line.StrokeThickness = 1;
            line.Stroke = Brushes.Red;
            
            line.Opacity = 0.8;

            line.X1 = startDraw[0];
            line.Y1 = endY;

            line.X2 = endX;
            line.Y2 = startDraw[1];

            _canvas.Children.Add(line);
            Canvas.SetBottom(line, 0);
        }


        private void DrawScale() {

            Line line = new Line();
            line.StrokeThickness = 1;
            line.Stroke = Brushes.Black;

            line.X1 = 3;
            line.Y1 = 5;

            line.X2 = 3;
            line.Y2 = height - 35;

            _canvas.Children.Add(line);

            Line line2 = new Line();
            line2.StrokeThickness = 1;
            line2.Stroke = Brushes.Black;

            line2.X1 = 3;
            line2.Y1 = 5;
            line2.X2 = 7;
            line2.Y2 = 5;

            _canvas.Children.Add(line2);

            TextBlock scaleTxt = new TextBlock();
            scaleTxt.Text = "600";

            _canvas.Children.Add(scaleTxt);
            Canvas.SetLeft(scaleTxt, 5);
            Canvas.SetTop(scaleTxt, 2);
        }


        public void RebuildGraph(List<List<DataItem>> dataList) {            

            _canvas.Children.Clear();
            bool rebuild = true;
            offset = 0;

            for (int i = 0; i < dataList.Count; i++) {

                if (i == 1) {

                    rebuild = false;
                }

                BuildGraph(
                    dataList[i][dataList[i].Count - 1].latency,
                    dataList[i][dataList[i].Count - 1].packetloss,
                    dataList[i][dataList[i].Count - 1].hostname,
                    rebuild
                    );
            }

            return;
        }

    }
}

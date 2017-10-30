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
        private List<int> history = new List<int>();
        private int offset = 0;
        private double[] startDraw = new double[2];
        private int maxRange = 800;
        
        public Grapher(Canvas canvas) {
            _canvas = canvas;
            _canvas.Name = "latencyGraph";
        }

        public void SetHeight(double input) {
            height = input;
        }

        public void BuildGraph(List<String> latencyList, string endIP) {

            int rectWidth = 15;
            double margin = 0.9;
            if (latencyList.Count <= 0) {
                return;
            }
            int index = latencyList.Count - 1;
            Console.WriteLine(latencyList[index]);
            int total = Convert.ToInt32(latencyList[index]);

            //Column
            Rectangle rect = new Rectangle();
            rect.Fill = new SolidColorBrush(Colors.SlateGray);
            rect.Width = rectWidth;
            double rectHeight = (height * ((double)total / maxRange) * margin);

            if (rectHeight < 5) { rectHeight = 5; }
            rect.Height = rectHeight;

            _canvas.Children.Add(rect);
            Canvas.SetBottom(rect, 0);
            Canvas.SetLeft(rect, rectWidth * offset);

            //Total Latency text
            TextBlock txt = new TextBlock();
            txt.Text = total.ToString();
            txt.FontSize = 8;

            _canvas.Children.Add(txt);
            Canvas.SetBottom(txt, rectHeight);
            Canvas.SetLeft(txt, (rectWidth * offset) + ((rectWidth / 2) - txt.ActualWidth) );

            if (offset != 0) {
                DrawLine((offset * rectWidth) + rectWidth, rectHeight);
                startDraw[0] = (rectWidth * offset) + rectWidth;
            }
            else {
                startDraw[0] = (rectWidth * offset);
            }
            
            startDraw[1] = rectHeight;
            offset++;            
        }

        private void DrawLine(double endX, double endY) {
            Line line = new Line();
            line.StrokeThickness = 2;
            line.Stroke = Brushes.Red;
            line.Opacity = 0.8;

            line.X1 = startDraw[0];
            line.Y1 = endY;

            line.X2 = endX;
            line.Y2 = startDraw[1];

            _canvas.Children.Add(line);
            Canvas.SetBottom(line, startDraw[1]);
        }

    }
}

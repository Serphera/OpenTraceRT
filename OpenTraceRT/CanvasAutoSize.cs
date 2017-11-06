using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using System.Text;
using System.Threading.Tasks;

namespace OpenTraceRT {
    public class CanvasAutoSize : Canvas {
        protected override Size MeasureOverride(Size constraint) {

            Size availableSize = new Size(double.PositiveInfinity, double.PositiveInfinity);

            double maxHeight = 0;
            double maxWidth = 0;

            foreach (UIElement element in base.InternalChildren) {

                if (element != null) {

                    element.Measure(availableSize);

                    double left = Canvas.GetLeft(element);
                    double top = Canvas.GetTop(element);

                    left += element.DesiredSize.Width;
                    top += element.DesiredSize.Height;

                    maxWidth = maxWidth < left ? left : maxWidth;
                    maxHeight = maxHeight < top ? top : maxHeight;
                }

            }

            return new Size { Height = maxHeight, Width = maxWidth };

        }
    }

}

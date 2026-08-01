using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace LyricsIsland.App.LayoutEditing
{
    public sealed class IslandInsertionAdorner : Adorner
    {
        private double x;

        public IslandInsertionAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        public void Update(double insertionX)
        {
            x = insertionX;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(220, 22, 119, 255)), 2);
            drawingContext.DrawLine(pen, new Point(x, 4), new Point(x, RenderSize.Height - 4));
        }
    }
}

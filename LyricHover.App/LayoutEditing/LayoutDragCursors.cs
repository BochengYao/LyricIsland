using System;
using System.Windows;
using System.Windows.Input;

namespace LyricHover.App.LayoutEditing
{
    public static class LayoutDragCursors
    {
        private static readonly Lazy<Cursor> OpenHandCursor =
            new Lazy<Cursor>(() => Load("grab-open.cur", Cursors.Hand));
        private static readonly Lazy<Cursor> ClosedHandCursor =
            new Lazy<Cursor>(() => Load("grab-closed.cur", Cursors.Hand));

        public static Cursor OpenHand => OpenHandCursor.Value;

        public static Cursor ClosedHand => ClosedHandCursor.Value;

        private static Cursor Load(string fileName, Cursor fallback)
        {
            try
            {
                var uri = new Uri(
                "pack://application:,,,/LyricHover.App;component/Assets/" + fileName,
                    UriKind.Absolute);
                var resource = Application.GetResourceStream(uri);
                if (resource?.Stream == null)
                {
                    return fallback;
                }

                using (resource.Stream)
                {
                    return new Cursor(resource.Stream);
                }
            }
            catch
            {
                return fallback;
            }
        }
    }
}

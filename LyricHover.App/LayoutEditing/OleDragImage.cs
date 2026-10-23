using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using ComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace LyricHover.App.LayoutEditing
{
    internal static class OleDragImage
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            public int Width;
            public int Height;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ShellDragImage
        {
            public NativeSize Size;
            public NativePoint Offset;
            public IntPtr Bitmap;
            public int ColorKey;
        }

        [ComImport]
        [Guid("4657278A-411B-11D2-839A-00C04FD918D0")]
        private class DragDropHelper
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("DE5BF786-477A-11D2-839D-00C04FD918D0")]
        private interface IDragSourceHelper
        {
            [PreserveSig]
            int InitializeFromBitmap(ref ShellDragImage dragImage, ComDataObject dataObject);

            [PreserveSig]
            int InitializeFromWindow(IntPtr windowHandle, ref NativePoint point, ComDataObject dataObject);
        }

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr objectHandle);

        public static void SuppressSystemImage(DataObject data)
        {
            if (!(data is ComDataObject comData))
            {
                return;
            }

            var colorKey = Color.Magenta;
            IntPtr bitmapHandle = IntPtr.Zero;
            object helper = null;
            try
            {
                using (var bitmap = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
                {
                    bitmap.SetPixel(0, 0, colorKey);
                    bitmapHandle = bitmap.GetHbitmap(colorKey);
                }

                var dragImage = new ShellDragImage
                {
                    Size = new NativeSize { Width = 1, Height = 1 },
                    Offset = new NativePoint(),
                    Bitmap = bitmapHandle,
                    ColorKey = ColorTranslator.ToWin32(colorKey)
                };
                helper = new DragDropHelper();
                ((IDragSourceHelper)helper).InitializeFromBitmap(ref dragImage, comData);
            }
            catch (ExternalException)
            {
                // Older shell configurations can reject custom drag images. The private
                // data format and custom ghost remain valid, so drag-and-drop can proceed.
            }
            catch (InvalidCastException)
            {
                // The WPF data object is not COM-backed in a headless/unit-test context.
            }
            finally
            {
                if (bitmapHandle != IntPtr.Zero)
                {
                    DeleteObject(bitmapHandle);
                }

                if (helper != null && Marshal.IsComObject(helper))
                {
                    Marshal.FinalReleaseComObject(helper);
                }
            }
        }
    }
}

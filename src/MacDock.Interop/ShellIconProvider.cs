using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MacDock.Interop.Native;

namespace MacDock.Interop;

/// <summary>
/// Gets high-resolution app icons from the Windows shell (the same source Explorer uses),
/// preserving the alpha channel, and caches them by target and size.
/// </summary>
public sealed class ShellIconProvider : IIconProvider
{
    private readonly ConcurrentDictionary<(string, int), ImageSource> _cache = new();
    private static readonly Lazy<ImageSource> Placeholder = new(CreatePlaceholder);

    public ImageSource Get(string target, int sizePx)
        => _cache.GetOrAdd((target, sizePx), key => TryLoad(key.Item1, key.Item2) ?? Placeholder.Value);

    private static ImageSource? TryLoad(string target, int sizePx)
    {
        string parsingName = TargetResolver.Resolve(target);
        var iid = Shell32.IID_IShellItemImageFactory;
        if (Shell32.SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out var factory) != 0 || factory is null)
            return null;

        IntPtr hbmp = IntPtr.Zero;
        try
        {
            if (factory.GetImage(new SIZE(sizePx, sizePx), SIIGBF.IconOnly | SIIGBF.BiggerSizeOk, out hbmp) != 0 || hbmp == IntPtr.Zero)
                return null;
            return ToBitmapSource(hbmp);
        }
        catch (COMException) { return null; }
        finally
        {
            if (hbmp != IntPtr.Zero) Gdi32.DeleteObject(hbmp);
            Marshal.FinalReleaseComObject(factory);
        }
    }

    /// <summary>
    /// Copies a 32-bit HBITMAP into a WPF bitmap. <c>Imaging.CreateBitmapSourceFromHBitmap</c> would drop
    /// the alpha channel (icons get black corners), so we read the pixels ourselves with GetDIBits,
    /// asking for top-down rows (negative height) so the image is never upside down.
    /// </summary>
    private static BitmapSource? ToBitmapSource(IntPtr hbmp)
    {
        if (Gdi32.GetObject(hbmp, Marshal.SizeOf<BITMAP>(), out var bm) == 0) return null;
        int w = bm.bmWidth, h = Math.Abs(bm.bmHeight);

        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = w,
            biHeight = -h,
            biPlanes = 1,
            biBitCount = 32,
        };
        var pixels = new byte[w * h * 4];
        IntPtr hdc = Gdi32.GetDC(IntPtr.Zero);
        try
        {
            if (Gdi32.GetDIBits(hdc, hbmp, 0, (uint)h, pixels, ref header, Gdi32.DIB_RGB_COLORS) == 0) return null;
        }
        finally { Gdi32.ReleaseDC(IntPtr.Zero, hdc); }

        var source = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixels, w * 4);
        source.Freeze();
        return source;
    }

    private static ImageSource CreatePlaceholder()
    {
        var drawing = new GeometryDrawing(
            new SolidColorBrush(Color.FromArgb(0xCC, 0x8E, 0x8E, 0x93)),
            null,
            new RectangleGeometry(new System.Windows.Rect(0, 0, 64, 64), 14, 14));
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }
}

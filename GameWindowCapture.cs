using SixLabors.ImageSharp.Formats;
using System.Drawing;
using System.Drawing.Imaging;
using WgcSharp;

namespace ArknightsPresence.Capture;

public sealed class GameWindowCapture
{
    public Bitmap Capture(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            throw new ArgumentException(
                "The window handle is invalid.",
                nameof(windowHandle));
        }

        Bitmap? frame = WindowCapture.CaptureWindow(windowHandle, CaptureStrategy.WgcOnly, timeoutMs: 3000);

        if (frame is null)
        {
            throw new InvalidOperationException("WgcSharp did not return a frame.");
        }

        return frame;
    }

    public string CaptureToFile(IntPtr windowHandle, string outputPath)
    {
        string? directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using Bitmap frame = Capture(windowHandle);

        frame.Save(outputPath, ImageFormat.Png);

        return outputPath;
    }
}
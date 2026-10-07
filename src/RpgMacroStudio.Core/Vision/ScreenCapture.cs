using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace RpgMacroHarness.Vision;

/// <summary>
/// 보안 프로그램에 걸리지 않는 순수 Windows OS 표준 화면 캡처 모듈입니다.
/// 메인 모니터뿐만 아니라 다중 모니터(서브 모니터) 전체 가상 스크린을 완벽하게 지원합니다.
/// </summary>
public static class ScreenCapture
{
    /// <summary>
    /// 모든 연결된 모니터를 포함하는 가상 화면(Virtual Screen) 전체 사각형 영역
    /// </summary>
    public static Rectangle VirtualScreenBounds => SystemInformation.VirtualScreen;

    public static Mat CaptureScreen(OpenCvSharp.Rect? roi = null)
    {
        Rectangle bounds;

        if (roi.HasValue)
        {
            bounds = new Rectangle(roi.Value.X, roi.Value.Y, roi.Value.Width, roi.Value.Height);
        }
        else
        {
            // 다중 모니터 전체 영역 캡처
            bounds = SystemInformation.VirtualScreen;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            }
        }

        using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
        }

        return BitmapConverter.ToMat(bmp);
    }
}

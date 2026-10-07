using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace RpgMacroHarness.Vision;

public record ImageMatchResult(bool Found, double Score, OpenCvSharp.Point Location);

/// <summary>
/// 사용자가 업로드한 이미지를 화면에서 찾아내는 템플릿 매칭 엔진입니다.
/// </summary>
public static class ImageMatcher
{
    /// <summary>
    /// 화면에서 템플릿 이미지를 검색합니다.
    /// </summary>
    /// <param name="screen">캡처된 화면 Mat</param>
    /// <param name="template">찾으려는 원본 이미지 Mat</param>
    /// <param name="accuracyPercent">정확도 슬라이더 값 (0 ~ 100). 예: 85%는 0.85 임계값</param>
    /// <param name="roi">특정 관심 영역 (null이면 전체 화면 검색)</param>
    public static ImageMatchResult Match(Mat screen, Mat template, double accuracyPercent, Rect? roi = null)
    {
        // 0~100 사이의 슬라이더 값을 OpenCV 0.0 ~ 1.0 스케일로 정규화
        double threshold = Math.Clamp(accuracyPercent / 100.0, 0.0, 1.0);

        Mat searchArea = screen;
        Mat? croppedArea = null;
        int offsetX = 0;
        int offsetY = 0;

        try
        {
            if (roi.HasValue)
            {
                // 화면 범위 내로 안전하게 자르기
                var safeRoi = new Rect(
                    Math.Max(0, roi.Value.X),
                    Math.Max(0, roi.Value.Y),
                    Math.Min(screen.Width - roi.Value.X, roi.Value.Width),
                    Math.Min(screen.Height - roi.Value.Y, roi.Value.Height)
                );

                if (safeRoi.Width >= template.Width && safeRoi.Height >= template.Height)
                {
                    croppedArea = new Mat(screen, safeRoi);
                    searchArea = croppedArea;
                    offsetX = safeRoi.X;
                    offsetY = safeRoi.Y;
                }
            }

            if (searchArea.Width < template.Width || searchArea.Height < template.Height)
            {
                return new ImageMatchResult(false, 0, new Point(0, 0));
            }

            using var result = new Mat();
            Cv2.MatchTemplate(searchArea, template, result, TemplateMatchModes.CCoeffNormed);

            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

            bool isFound = maxVal >= threshold;
            var finalLocation = new Point(maxLoc.X + offsetX, maxLoc.Y + offsetY);

            return new ImageMatchResult(isFound, maxVal, finalLocation);
        }
        finally
        {
            croppedArea?.Dispose();
        }
    }
}

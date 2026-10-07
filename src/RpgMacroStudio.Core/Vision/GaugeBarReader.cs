using System.Drawing;
using OpenCvSharp;

namespace RpgMacroHarness.Vision;

public enum GaugeColorType
{
    CustomEyedropper, // 화면 스포이드로 직접 찍은 색상
    RedHp,            // 기본 빨간색 체력바
    BlueMp,           // 기본 파란색 마나/스킬 게이지
    GreenStamina      // 기본 초록색 스태미나
}

/// <summary>
/// AI(머신러닝/OCR)를 전혀 쓰지 않는 100% 순수 영상 처리 기반의 초고속 게이지바 인식 엔진입니다.
/// 화면 스포이드로 찍은 맞춤 색상 및 0~100% 색상 정확도(허용 오차) 슬라이더를 지원합니다.
/// </summary>
public static class GaugeBarReader
{
    public static double ReadPercentage(
        Mat screen, 
        Rect roi, 
        Color? customColor = null, 
        double colorAccuracy = 85.0,
        GaugeColorType colorType = GaugeColorType.CustomEyedropper)
    {
        var safeRoi = new Rect(
            Math.Max(0, roi.X),
            Math.Max(0, roi.Y),
            Math.Min(screen.Width - roi.X, roi.Width),
            Math.Min(screen.Height - roi.Y, roi.Height)
        );

        if (safeRoi.Width <= 0 || safeRoi.Height <= 0) return 0.0;

        using var barMat = new Mat(screen, safeRoi);
        using var hsvMat = new Mat();
        Cv2.CvtColor(barMat, hsvMat, ColorConversionCodes.BGR2HSV);

        using var mask = new Mat();

        if (customColor.HasValue)
        {
            // 스포이드로 찍은 사용자 지정 색상을 HSV로 변환
            using var sampleBgr = new Mat(1, 1, MatType.CV_8UC3, new Scalar(customColor.Value.B, customColor.Value.G, customColor.Value.R));
            using var sampleHsv = new Mat();
            Cv2.CvtColor(sampleBgr, sampleHsv, ColorConversionCodes.BGR2HSV);
            var hsvVec = sampleHsv.Get<Vec3b>(0, 0);

            // 색상 정확도(0~100%)에 따른 H, S, V 허용 오차 계산
            // 정확도 100%일 때 매우 엄격, 정확도가 낮을수록 조명 변화나 그라데이션 허용
            double toleranceRatio = (100.0 - Math.Clamp(colorAccuracy, 0.0, 100.0)) / 100.0;
            int deltaH = (int)(toleranceRatio * 25 + 5);  // H (0~180) 오차
            int deltaS = (int)(toleranceRatio * 90 + 25); // S (0~255) 오차
            int deltaV = (int)(toleranceRatio * 90 + 25); // V (0~255) 오차

            int h = hsvVec[0];
            int s = hsvVec[1];
            int v = hsvVec[2];

            int rawMinH = h - deltaH;
            int rawMaxH = h + deltaH;
            int minS = Math.Clamp(s - deltaS, 0, 255);
            int maxS = Math.Clamp(s + deltaS, 0, 255);
            int minV = Math.Clamp(v - deltaV, 0, 255);
            int maxV = Math.Clamp(v + deltaV, 0, 255);

            // 저채도/무채색(검정, 흰색, 회색) 및 색상환(0~180) 경계선 래핑을 완벽 지원
            if (rawMinH < 0)
            {
                using var mask1 = new Mat();
                using var mask2 = new Mat();
                Cv2.InRange(hsvMat, new Scalar(0, minS, minV), new Scalar(Math.Min(180, rawMaxH), maxS, maxV), mask1);
                Cv2.InRange(hsvMat, new Scalar(180 + rawMinH, minS, minV), new Scalar(180, maxS, maxV), mask2);
                Cv2.BitwiseOr(mask1, mask2, mask);
            }
            else if (rawMaxH > 180)
            {
                using var mask1 = new Mat();
                using var mask2 = new Mat();
                Cv2.InRange(hsvMat, new Scalar(Math.Max(0, rawMinH), minS, minV), new Scalar(180, maxS, maxV), mask1);
                Cv2.InRange(hsvMat, new Scalar(0, minS, minV), new Scalar(rawMaxH - 180, maxS, maxV), mask2);
                Cv2.BitwiseOr(mask1, mask2, mask);
            }
            else
            {
                Cv2.InRange(hsvMat, new Scalar(rawMinH, minS, minV), new Scalar(rawMaxH, maxS, maxV), mask);
            }
        }
        else
        {
            switch (colorType)
            {
                case GaugeColorType.RedHp:
                    using (var mask1 = new Mat())
                    using (var mask2 = new Mat())
                    {
                        Cv2.InRange(hsvMat, new Scalar(0, 70, 50), new Scalar(10, 255, 255), mask1);
                        Cv2.InRange(hsvMat, new Scalar(170, 70, 50), new Scalar(180, 255, 255), mask2);
                        Cv2.BitwiseOr(mask1, mask2, mask);
                    }
                    break;

                case GaugeColorType.BlueMp:
                    Cv2.InRange(hsvMat, new Scalar(100, 70, 50), new Scalar(130, 255, 255), mask);
                    break;

                case GaugeColorType.GreenStamina:
                    Cv2.InRange(hsvMat, new Scalar(35, 70, 50), new Scalar(85, 255, 255), mask);
                    break;
            }
        }

        // 전체 면적 밀도 방식 (Pixel Area Ratio / Density Mode)
        // 사각형(가로/세로), 원형 구체, 대각선 등 모든 형태의 게이지에서 유효 색상 면적 비율(%)을 정밀 측정
        int totalPixels = mask.Width * mask.Height;
        if (totalPixels <= 0) return 0.0;

        int filledPixels = Cv2.CountNonZero(mask);
        double percent = ((double)filledPixels / totalPixels) * 100.0;
        return Math.Round(Math.Clamp(percent, 0.0, 100.0), 1);
    }
}

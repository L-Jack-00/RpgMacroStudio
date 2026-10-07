namespace RpgMacroHarness.Input;

/// <summary>
/// 기계적인 매크로 탐지를 원천 방지하기 위해 인간의 불완전한 물리적 입력을 정밀 모사하는 휴머나이저(Humanizer) 엔진입니다.
/// 가우시안/정규분포 기반의 미세 지연, 손가락 누름 편차, 마우스 좌표 미세 떨림(Jitter)을 생성합니다.
/// 사용자가 딜레이 편차 배율(0%~200%, 기본 100%)을 직접 조절할 수 있습니다.
/// </summary>
public static class InputHumanizer
{
    /// <summary>
    /// 휴머나이저 딜레이 편차 강도 배율 (0~200%, 기본값: 100%) - 하위 호환
    /// </summary>
    public static int JitterScalePercent { get; set; } = 100;

    /// <summary>
    /// 휴머나이저 직접 지연 편차 ms (0 ~ 54ms, 기본값: 27ms)
    /// </summary>
    public static int JitterMs { get; set; } = 27;

    private static double Scale => JitterMs > 0 ? (JitterMs / 27.0) : (Math.Clamp(JitterScalePercent, 0, 300) / 100.0);

    /// <summary>
    /// 인간적인 키 누름 유지 시간(Hold Time)을 계산합니다. (설정값 기준 ± 미세 변동)
    /// </summary>
    public static int GetHumanizedKeyHold(int baseMs)
    {
        baseMs = Math.Max(15, baseMs);
        if (JitterMs == 0 && JitterScalePercent == 0) return baseMs;
        int maxDelta = Math.Max(1, JitterMs / 3);
        int delta = Random.Shared.Next(-maxDelta, maxDelta + 1);
        return Math.Max(15, baseMs + delta);
    }

    /// <summary>
    /// 인간적인 키 입력 후 딜레이(Post Delay)를 계산합니다.
    /// </summary>
    public static int GetHumanizedKeyPostDelay(int baseMs)
    {
        baseMs = Math.Max(20, baseMs);
        if (JitterMs == 0 && JitterScalePercent == 0) return baseMs;
        int delta = Random.Shared.Next(0, JitterMs + 1);
        return Math.Max(15, baseMs + delta);
    }

    /// <summary>
    /// 인간적인 마우스 클릭 좌표를 계산합니다. (정밀 픽셀 중심에서 ±1~2픽셀 미세 편차)
    /// 다중 모니터 전역 가상 스크린(음수 좌표계 포함)을 완벽하게 지원합니다.
    /// </summary>
    public static (int x, int y) GetHumanizedCoordinates(int x, int y, int radius = 2)
    {
        int scaledRadius = Math.Max(0, (int)Math.Round(radius * Scale));
        int jx = x + (scaledRadius > 0 ? Random.Shared.Next(-scaledRadius, scaledRadius + 1) : 0);
        int jy = y + (scaledRadius > 0 ? Random.Shared.Next(-scaledRadius, scaledRadius + 1) : 0);

        var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
        if (vs.Width > 0 && vs.Height > 0)
        {
            return (
                Math.Clamp(jx, vs.Left, Math.Max(vs.Left, vs.Right - 1)),
                Math.Clamp(jy, vs.Top, Math.Max(vs.Top, vs.Bottom - 1))
            );
        }

        return (jx, jy);
    }

    /// <summary>
    /// 마우스 클릭 시 버튼을 누르고 있는 자연스러운 손가락 압력 시간(ms)을 반환합니다. (평균 30~55ms)
    /// </summary>
    public static int GetHumanizedMouseHold()
    {
        int baseHold = 42;
        int delta = (int)(Random.Shared.Next(-12, 14) * Scale);
        return Math.Max(20, baseHold + delta);
    }

    /// <summary>
    /// 마우스 이동 후 클릭 전 착지 안정화 시간(ms)을 반환합니다. (15~35ms)
    /// </summary>
    public static int GetMouseSettlingDelay()
    {
        int baseDelay = 25;
        int delta = (int)(Random.Shared.Next(-10, 11) * Scale);
        return Math.Max(10, baseDelay + delta);
    }

    /// <summary>
    /// 마우스 클릭 후 다음 액션 전 자연스러운 반응 지연(ms)을 반환합니다. (30~65ms)
    /// </summary>
    public static int GetMouseReleaseDelay()
    {
        int baseDelay = 47;
        int delta = (int)(Random.Shared.Next(-17, 19) * Scale);
        return Math.Max(15, baseDelay + delta);
    }

    /// <summary>
    /// 주기적 타이머(Timer Interval)의 기계적 주기성을 깨뜨리기 위한 미세 지연을 반환합니다.
    /// </summary>
    public static int GetHumanizedTimerInterval(int baseIntervalMs)
    {
        int maxJitter = Math.Min(50, Math.Max(10, (int)(baseIntervalMs * 0.05)));
        int delta = (int)(Random.Shared.Next(-maxJitter, maxJitter + 1) * Scale);
        return Math.Max(50, baseIntervalMs + delta);
    }

    /// <summary>
    /// Wait 대기 시간의 인간적 변동을 계산합니다.
    /// </summary>
    public static int GetHumanizedWait(int baseWaitMs)
    {
        if (baseWaitMs <= 50) return baseWaitMs;
        int maxJitter = Math.Min(40, (int)(baseWaitMs * 0.04));
        int delta = (int)(Random.Shared.Next(-maxJitter, maxJitter + 1) * Scale);
        return Math.Max(20, baseWaitMs + delta);
    }
}

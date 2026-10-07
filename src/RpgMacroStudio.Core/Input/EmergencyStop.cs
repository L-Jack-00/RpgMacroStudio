using System.Runtime.InteropServices;

namespace RpgMacroHarness.Input;

/// <summary>
/// 매크로가 오동작할 때 언제든지 즉시 중단할 수 있는 글로벌 비상 정지 핫키 관리자입니다.
/// </summary>
public static class EmergencyStop
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public static bool IsTriggered { get; private set; } = false;
    private static bool _isRunning = false;

    /// <summary>
    /// 백그라운드에서 비상 정지 핫키(기본 F12)를 상시 감시합니다.
    /// </summary>
    public static void StartMonitoring(Keys stopKey = Keys.F12)
    {
        _isRunning = false;
        Thread.Sleep(30); // 기존 스레드 정상 종료 대기
        _isRunning = true;
        IsTriggered = false;

        var thread = new Thread(() =>
        {
            while (_isRunning)
            {
                // 비상 정지 키가 눌렸는지 확인 (최상위 비트 체크)
                if ((GetAsyncKeyState((int)stopKey) & 0x8000) != 0)
                {
                    IsTriggered = true;
                    _isRunning = false; // 실행 상태 플래그 정상 초기화
                    WindowsInputSimulator.ReleaseAllPressedInputs();
                    break;
                }
                Thread.Sleep(50);
            }
        })
        {
            IsBackground = true
        };

        thread.Start();
    }

    public static void Trigger()
    {
        IsTriggered = true;
        WindowsInputSimulator.ReleaseAllPressedInputs();
    }

    public static void Reset()
    {
        IsTriggered = false;
    }

    public static void StopMonitoring()
    {
        _isRunning = false;
        WindowsInputSimulator.ReleaseAllPressedInputs();
    }
}

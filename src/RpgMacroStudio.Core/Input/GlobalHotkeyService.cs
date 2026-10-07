using System.Runtime.InteropServices;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.Input;

/// <summary>
/// 시작/일시정지, 정지, 긴급 비상정지 전역 핫키를 백그라운드에서 감시하는 글로벌 핫키 서비스입니다.
/// 키를 누르고 있을 때 중복 실행되지 않도록 에지 검출(Edge Detection)이 적용되어 있습니다.
/// </summary>
public static class GlobalHotkeyService
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public static event Action? OnRecordPressed;
    public static event Action? OnStartPausePressed;
    public static event Action? OnStopPressed;
    public static event Action? OnEmergencyStopPressed;

    private static bool _isRunning = false;
    private static Thread? _monitoringThread;

    private static Keys _recordKey = Keys.F9;
    private static Keys _startPauseKey = Keys.F10;
    private static Keys _stopKey = Keys.F11;
    private static Keys _emergencyKey = Keys.F12;

    public static void Start()
    {
        UpdateKeysFromSettings();
        if (_isRunning) return;

        _isRunning = true;
        _monitoringThread = new Thread(MonitoringLoop)
        {
            IsBackground = true,
            Name = "GlobalHotkeyListener"
        };
        _monitoringThread.Start();
    }

    public static void Stop()
    {
        _isRunning = false;
        _monitoringThread?.Join(200);
        _monitoringThread = null;
    }

    public static void UpdateKeysFromSettings()
    {
        if (Enum.TryParse<Keys>(AppSettings.Current.RecordHotkey, true, out var rk))
            _recordKey = rk;
        else
            _recordKey = Keys.F9;

        if (Enum.TryParse<Keys>(AppSettings.Current.StartPauseHotkey, true, out var spk))
            _startPauseKey = spk;
        else
            _startPauseKey = Keys.F10;

        if (Enum.TryParse<Keys>(AppSettings.Current.StopHotkey, true, out var sk))
            _stopKey = sk;
        else
            _stopKey = Keys.F11;

        if (Enum.TryParse<Keys>(AppSettings.Current.EmergencyStopHotkey, true, out var ek))
            _emergencyKey = ek;
        else
            _emergencyKey = Keys.F12;
    }

    private static void MonitoringLoop()
    {
        bool wasRecordDown = false;
        bool wasStartPauseDown = false;
        bool wasStopDown = false;
        bool wasEmergencyDown = false;

        while (_isRunning)
        {
            try
            {
                // 1. 긴급 비상정지 감시 (최우선)
                bool isEmergencyDown = (GetAsyncKeyState((int)_emergencyKey) & 0x8000) != 0;
                if (isEmergencyDown && !wasEmergencyDown)
                {
                    EmergencyStop.Trigger();
                    OnEmergencyStopPressed?.Invoke();
                }
                wasEmergencyDown = isEmergencyDown;

                // 2. 완전 정지 감시
                bool isStopDown = (GetAsyncKeyState((int)_stopKey) & 0x8000) != 0;
                if (isStopDown && !wasStopDown)
                {
                    OnStopPressed?.Invoke();
                }
                wasStopDown = isStopDown;

                // 3. 시작 / 일시정지 감시
                bool isStartPauseDown = (GetAsyncKeyState((int)_startPauseKey) & 0x8000) != 0;
                if (isStartPauseDown && !wasStartPauseDown)
                {
                    OnStartPausePressed?.Invoke();
                }
                wasStartPauseDown = isStartPauseDown;

                // 4. 매크로 녹화 시작 / 중지 감시
                bool isRecordDown = (GetAsyncKeyState((int)_recordKey) & 0x8000) != 0;
                if (isRecordDown && !wasRecordDown)
                {
                    OnRecordPressed?.Invoke();
                }
                wasRecordDown = isRecordDown;
            }
            catch
            {
                // 예외 무시
            }

            Thread.Sleep(30);
        }
    }
}

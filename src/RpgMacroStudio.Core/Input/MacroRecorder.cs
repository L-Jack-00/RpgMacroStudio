using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.Input;

/// <summary>
/// 사용자의 실시간 키보드 및 마우스 입력을 저수준(LL) 훅으로 감지하여
/// 지연시간(Delay/Wait)과 함께 MacroAction 목록으로 변환하는 녹화 모듈입니다.
/// 다중 모니터 전역 좌표계(음수 좌표 포함)를 완벽하게 지원합니다.
/// </summary>
public class MacroRecorder : IDisposable
{
    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WM_XBUTTONUP = 0x020C;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
    private const int VK_LBUTTON = 0x01;

    internal enum RawInputType
    {
        KeyDown,
        KeyUp,
        MouseDown,
        MouseUp
    }

    internal class RawInputEvent
    {
        public long TimestampMs { get; set; }
        public RawInputType Type { get; set; }
        public Keys Key { get; set; }
        public MouseButtonType MouseButton { get; set; }
        public Point ScreenPoint { get; set; }
    }

    private class TimedAction
    {
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public MacroAction Action { get; set; } = null!;
    }

    private IntPtr _keyboardHookId = IntPtr.Zero;
    private IntPtr _mouseHookId = IntPtr.Zero;
    private LowLevelProc? _keyboardProc;
    private LowLevelProc? _mouseProc;

    private readonly HashSet<Keys> _currentlyPressedKeys = new();
    private readonly HashSet<MouseButtonType> _currentlyPressedMouseButtons = new();

    private Rectangle? _startButtonBounds;
    private string? _triggerHotkey;
    private bool _ignoreInitialLeftMouseRelease;

    private readonly Stopwatch _stopwatch = new();
    private readonly List<RawInputEvent> _recordedEvents = new();
    private readonly object _lock = new();

    public bool IsRecording { get; private set; } = false;

    public event Action<string>? OnRecordingStatusChanged;
    public event Action<string>? OnInputRecorded;
    public event Action? OnStopHotkeyTriggered;

    public void StartRecording(Rectangle? startButtonBounds = null, string? triggerHotkey = null)
    {
        if (IsRecording) return;

        lock (_lock)
        {
            _recordedEvents.Clear();
            _currentlyPressedKeys.Clear();
            _currentlyPressedMouseButtons.Clear();
            _startButtonBounds = startButtonBounds;
            _triggerHotkey = triggerHotkey;

            // 시작 버튼 클릭으로 진입한 경우, 이미 물리적으로 눌려있는 마우스 좌클릭의 release(Up) 이벤트 무시
            _ignoreInitialLeftMouseRelease = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;

            _stopwatch.Restart();

            _keyboardProc = KeyboardHookCallback;
            _mouseProc = MouseHookCallback;

            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            var hMod = GetModuleHandle(curModule?.ModuleName);

            _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);

            IsRecording = true;
        }

        OnRecordingStatusChanged?.Invoke("녹화 시작됨");
    }

    public List<MacroAction> StopRecording(bool ignoreLastStopTrigger = true)
    {
        if (!IsRecording) return new List<MacroAction>();

        List<RawInputEvent> eventsCopy;

        lock (_lock)
        {
            _stopwatch.Stop();
            IsRecording = false;

            if (_keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
            }
            if (_mouseHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
            }

            eventsCopy = new List<RawInputEvent>(_recordedEvents);
            _recordedEvents.Clear();
        }

        OnRecordingStatusChanged?.Invoke("녹화 중지됨");

        // 녹화 중지 버튼 클릭 또는 종료 조작으로 인한 마지막 입력 트리밍
        if (ignoreLastStopTrigger && eventsCopy.Count > 0)
        {
            long lastTime = eventsCopy[^1].TimestampMs;
            while (eventsCopy.Count > 0 && (lastTime - eventsCopy[^1].TimestampMs) < 350)
            {
                eventsCopy.RemoveAt(eventsCopy.Count - 1);
            }
        }

        return ConvertToMacroActions(eventsCopy);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsRecording)
        {
            int msg = wParam.ToInt32();
            int vkCode = Marshal.ReadInt32(lParam);
            var key = (Keys)vkCode;

            // 녹화 단축키(예: F9) 처리
            long now = _stopwatch.ElapsedMilliseconds;
            if (!string.IsNullOrEmpty(_triggerHotkey) &&
                string.Equals(key.ToString(), _triggerHotkey, StringComparison.OrdinalIgnoreCase))
            {
                if (now < 400)
                {
                    // 녹화 시작 직후(400ms 이내) 시작 트리거 핫키 입력(잔여 KeyDown/KeyUp) 무시
                    return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                }
                else
                {
                    // 녹화 중(400ms 이후) 녹화 중단 단축키 입력 감지: 매크로에 기록하지 않고 즉각 중단 트리거 발동
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        OnStopHotkeyTriggered?.Invoke();
                    }
                    return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                }
            }

            RawInputType? type = null;
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                type = RawInputType.KeyDown;
            }
            else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
            {
                type = RawInputType.KeyUp;
            }

            if (type == RawInputType.KeyDown)
            {
                lock (_lock)
                {
                    if (!_currentlyPressedKeys.Add(key))
                    {
                        // OS 키 자동 반복(Key Repeat) 무시
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }
                }
            }
            else if (type == RawInputType.KeyUp)
            {
                lock (_lock)
                {
                    _currentlyPressedKeys.Remove(key);
                }
            }

            if (type.HasValue)
            {
                lock (_lock)
                {
                    _recordedEvents.Add(new RawInputEvent
                    {
                        TimestampMs = now,
                        Type = type.Value,
                        Key = key
                    });
                }
                OnInputRecorded?.Invoke($"[Key {type.Value}] {key} (+{now}ms)");
            }
        }

        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsRecording)
        {
            int msg = wParam.ToInt32();
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var screenPt = new Point(hookStruct.pt.x, hookStruct.pt.y);

            // 1. 녹화 시작 버튼 클릭 후 손을 뗄 때 발생하는 잔여 MouseUp 무시
            if (_ignoreInitialLeftMouseRelease && msg == WM_LBUTTONUP)
            {
                _ignoreInitialLeftMouseRelease = false;
                return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
            }

            // 2. 녹화 시작 직후(400ms 이내)에 [녹화 시작 버튼] 내부에서 발생한 마우스 이벤트 무시
            long now = _stopwatch.ElapsedMilliseconds;
            if (now < 400 && _startButtonBounds.HasValue && _startButtonBounds.Value.Contains(screenPt))
            {
                return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
            }

            RawInputType? type = null;
            MouseButtonType btn = MouseButtonType.Left;

            if (msg == WM_LBUTTONDOWN)
            {
                type = RawInputType.MouseDown;
                btn = MouseButtonType.Left;
            }
            else if (msg == WM_LBUTTONUP)
            {
                type = RawInputType.MouseUp;
                btn = MouseButtonType.Left;
            }
            else if (msg == WM_RBUTTONDOWN)
            {
                type = RawInputType.MouseDown;
                btn = MouseButtonType.Right;
            }
            else if (msg == WM_RBUTTONUP)
            {
                type = RawInputType.MouseUp;
                btn = MouseButtonType.Right;
            }
            else if (msg == WM_MBUTTONDOWN)
            {
                type = RawInputType.MouseDown;
                btn = MouseButtonType.Middle;
            }
            else if (msg == WM_MBUTTONUP)
            {
                type = RawInputType.MouseUp;
                btn = MouseButtonType.Middle;
            }
            else if (msg == WM_XBUTTONDOWN)
            {
                type = RawInputType.MouseDown;
                int xBtn = (int)((hookStruct.mouseData >> 16) & 0xFFFF);
                btn = (xBtn == 2) ? MouseButtonType.XButton2 : MouseButtonType.XButton1;
            }
            else if (msg == WM_XBUTTONUP)
            {
                type = RawInputType.MouseUp;
                int xBtn = (int)((hookStruct.mouseData >> 16) & 0xFFFF);
                btn = (xBtn == 2) ? MouseButtonType.XButton2 : MouseButtonType.XButton1;
            }

            if (type == RawInputType.MouseDown)
            {
                lock (_lock)
                {
                    if (!_currentlyPressedMouseButtons.Add(btn))
                    {
                        return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
                    }
                }
            }
            else if (type == RawInputType.MouseUp)
            {
                lock (_lock)
                {
                    _currentlyPressedMouseButtons.Remove(btn);
                }
            }

            if (type.HasValue)
            {
                now = _stopwatch.ElapsedMilliseconds;
                lock (_lock)
                {
                    _recordedEvents.Add(new RawInputEvent
                    {
                        TimestampMs = now,
                        Type = type.Value,
                        MouseButton = btn,
                        ScreenPoint = screenPt
                    });
                }
                OnInputRecorded?.Invoke($"[Mouse {type.Value}] {btn} ({screenPt.X}, {screenPt.Y}) (+{now}ms)");
            }
        }

        return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    internal static List<MacroAction> ConvertToMacroActions(List<RawInputEvent> rawEvents)
    {
        var actions = new List<MacroAction>();
        if (rawEvents.Count == 0) return actions;

        var timedActions = new List<TimedAction>();
        var handledEvents = new HashSet<RawInputEvent>();

        // 1. 키보드/마우스 이벤트 분류 (단독 입력 KeyDownUp vs 동시/조합 입력 KeyDown/KeyUp)
        for (int i = 0; i < rawEvents.Count; i++)
        {
            var ev = rawEvents[i];
            if (ev.Type == RawInputType.KeyDown)
            {
                // 대응하는 다음 KeyUp 검색
                RawInputEvent? matchingUp = null;
                bool hasInterleavedEvent = false;

                for (int j = i + 1; j < rawEvents.Count; j++)
                {
                    var nextEv = rawEvents[j];
                    if (nextEv.Type == RawInputType.KeyUp && nextEv.Key == ev.Key)
                    {
                        matchingUp = nextEv;
                        break;
                    }
                    // 중간에 다른 키 또는 마우스 이벤트가 개입했는지 감지
                    hasInterleavedEvent = true;
                }

                if (matchingUp != null && !hasInterleavedEvent)
                {
                    // [단독 입력]: 다른 키/마우스와 겹치지 않는 순수 단일 키 탭 -> KeyDownUp
                    int holdTime = Math.Clamp((int)(matchingUp.TimestampMs - ev.TimestampMs), 15, 5000);
                    timedActions.Add(new TimedAction
                    {
                        StartTime = ev.TimestampMs,
                        EndTime = ev.TimestampMs + holdTime,
                        Action = new MacroAction
                        {
                            Type = ActionType.KeyPress,
                            Key = ev.Key.ToString(),
                            KeyMode = KeyStrokeMode.KeyDownUp,
                            HoldTimeMs = holdTime,
                            PostDelayMs = 0,
                            Description = $"키 입력: [{AppKeyHelper.FormatKey(ev.Key.ToString())}] ({holdTime}ms)"
                        }
                    });
                    handledEvents.Add(ev);
                    handledEvents.Add(matchingUp);
                }
                else
                {
                    // [동시/조합 입력]: 키를 누른 채 다른 키/마우스가 동작함 -> KeyDown 분리
                    timedActions.Add(new TimedAction
                    {
                        StartTime = ev.TimestampMs,
                        EndTime = ev.TimestampMs,
                        Action = new MacroAction
                        {
                            Type = ActionType.KeyPress,
                            Key = ev.Key.ToString(),
                            KeyMode = KeyStrokeMode.KeyDown,
                            HoldTimeMs = 0,
                            PostDelayMs = 0,
                            Description = $"⬇️ 키다운: [{AppKeyHelper.FormatKey(ev.Key.ToString())}]"
                        }
                    });
                    handledEvents.Add(ev);
                }
            }
            else if (ev.Type == RawInputType.KeyUp)
            {
                // 이미 KeyDownUp으로 묶이지 않은 KeyUp -> 개별 KeyUp 액션 생성
                if (!handledEvents.Contains(ev))
                {
                    timedActions.Add(new TimedAction
                    {
                        StartTime = ev.TimestampMs,
                        EndTime = ev.TimestampMs,
                        Action = new MacroAction
                        {
                            Type = ActionType.KeyPress,
                            Key = ev.Key.ToString(),
                            KeyMode = KeyStrokeMode.KeyUp,
                            HoldTimeMs = 0,
                            PostDelayMs = 0,
                            Description = $"⬆️ 키업: [{AppKeyHelper.FormatKey(ev.Key.ToString())}]"
                        }
                    });
                    handledEvents.Add(ev);
                }
            }
            else if (ev.Type == RawInputType.MouseDown)
            {
                // 대응하는 다음 MouseUp 검색
                RawInputEvent? matchingMouseUp = null;
                bool hasInterleavedEvent = false;

                for (int j = i + 1; j < rawEvents.Count; j++)
                {
                    var nextEv = rawEvents[j];
                    if (nextEv.Type == RawInputType.MouseUp && nextEv.MouseButton == ev.MouseButton)
                    {
                        matchingMouseUp = nextEv;
                        break;
                    }
                    hasInterleavedEvent = true;
                }

                if (matchingMouseUp != null && !hasInterleavedEvent)
                {
                    // [단독 입력]: 다른 키/마우스와 겹치지 않는 순수 단일 클릭 -> MouseDownUp
                    int holdTime = Math.Clamp((int)(matchingMouseUp.TimestampMs - ev.TimestampMs), 15, 5000);
                    timedActions.Add(new TimedAction
                    {
                        StartTime = ev.TimestampMs,
                        EndTime = ev.TimestampMs + holdTime,
                        Action = new MacroAction
                        {
                            Type = ActionType.MouseClick,
                            MouseButton = ev.MouseButton,
                            MouseMode = MouseStrokeMode.MouseDownUp,
                            TargetMode = MouseTargetMode.Coordinates,
                            CustomX = ev.ScreenPoint.X,
                            CustomY = ev.ScreenPoint.Y,
                            HoldTimeMs = holdTime,
                            PostDelayMs = 0,
                            Description = $"🖱️ 마우스 {ev.MouseButton} 클릭 ({ev.ScreenPoint.X}, {ev.ScreenPoint.Y}) ({holdTime}ms)"
                        }
                    });
                    handledEvents.Add(ev);
                    handledEvents.Add(matchingMouseUp);
                }
                else
                {
                    // [동시/드래그 입력]: 누른 채 다른 키/마우스가 동작함 -> MouseDown 분리
                    timedActions.Add(new TimedAction
                    {
                        StartTime = ev.TimestampMs,
                        EndTime = ev.TimestampMs,
                        Action = new MacroAction
                        {
                            Type = ActionType.MouseClick,
                            MouseButton = ev.MouseButton,
                            MouseMode = MouseStrokeMode.MouseDown,
                            TargetMode = MouseTargetMode.Coordinates,
                            CustomX = ev.ScreenPoint.X,
                            CustomY = ev.ScreenPoint.Y,
                            HoldTimeMs = 0,
                            PostDelayMs = 0,
                            Description = $"⬇️ 마우스다운: {ev.MouseButton} ({ev.ScreenPoint.X}, {ev.ScreenPoint.Y})"
                        }
                    });
                    handledEvents.Add(ev);
                }
            }
            else if (ev.Type == RawInputType.MouseUp)
            {
                // 선행 MouseDown이 없는 고아 MouseUp(예: 녹화 시작 버튼 릴리즈 등)은 필터링
                bool hasPrecedingDown = false;
                for (int k = 0; k < i; k++)
                {
                    if (rawEvents[k].Type == RawInputType.MouseDown && rawEvents[k].MouseButton == ev.MouseButton)
                    {
                        hasPrecedingDown = true;
                        break;
                    }
                }

                if (!hasPrecedingDown)
                {
                    continue;
                }

                // 이미 MouseDownUp으로 묶이지 않은 MouseUp -> 개별 MouseUp 액션 생성
                if (!handledEvents.Contains(ev))
                {
                    timedActions.Add(new TimedAction
                    {
                        StartTime = ev.TimestampMs,
                        EndTime = ev.TimestampMs,
                        Action = new MacroAction
                        {
                            Type = ActionType.MouseClick,
                            MouseButton = ev.MouseButton,
                            MouseMode = MouseStrokeMode.MouseUp,
                            TargetMode = MouseTargetMode.Coordinates,
                            CustomX = ev.ScreenPoint.X,
                            CustomY = ev.ScreenPoint.Y,
                            HoldTimeMs = 0,
                            PostDelayMs = 0,
                            Description = $"⬆️ 마우스업: {ev.MouseButton} ({ev.ScreenPoint.X}, {ev.ScreenPoint.Y})"
                        }
                    });
                    handledEvents.Add(ev);
                }
            }
        }

        // 2. 타임라인 순으로 정렬
        timedActions.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

        // 3. 지연(Wait) 액션 자동 삽입
        var actionsWithWaits = new List<MacroAction>();
        long lastEndTime = -1;

        for (int i = 0; i < timedActions.Count; i++)
        {
            var ta = timedActions[i];
            if (lastEndTime >= 0)
            {
                long gap = ta.StartTime - lastEndTime;
                if (gap >= 20)
                {
                    actionsWithWaits.Add(new MacroAction
                    {
                        Type = ActionType.Wait,
                        WaitDurationMs = (int)gap,
                        Description = $"대기 {gap}ms"
                    });
                }
            }
            actionsWithWaits.Add(ta.Action);
            lastEndTime = Math.Max(lastEndTime, ta.EndTime);
        }

        // 4. 연속된 대기(Wait) 액션 자동 합산
        var mergedActions = new List<MacroAction>();
        foreach (var act in actionsWithWaits)
        {
            if (act.Type == ActionType.Wait && mergedActions.Count > 0 && mergedActions[^1].Type == ActionType.Wait)
            {
                mergedActions[^1].WaitDurationMs += act.WaitDurationMs;
                mergedActions[^1].Description = $"대기 {mergedActions[^1].WaitDurationMs}ms";
            }
            else
            {
                mergedActions.Add(act);
            }
        }

        // 5. 키 영구 눌림 방지 안전망: KeyDown만 있고 대응 KeyUp이 없는 미해제 키가 있으면 마지막에 KeyUp 추가
        var unreleasedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var act in mergedActions)
        {
            if (act.Type == ActionType.KeyPress)
            {
                if (act.KeyMode == KeyStrokeMode.KeyDown)
                {
                    unreleasedKeys.Add(act.Key);
                }
                else if (act.KeyMode == KeyStrokeMode.KeyUp)
                {
                    unreleasedKeys.Remove(act.Key);
                }
            }
        }

        foreach (var unreleased in unreleasedKeys)
        {
            mergedActions.Add(new MacroAction
            {
                Type = ActionType.KeyPress,
                Key = unreleased,
                KeyMode = KeyStrokeMode.KeyUp,
                PostDelayMs = 0,
                Description = $"⬆️ 키업: [{AppKeyHelper.FormatKey(unreleased)}] (녹화 종료 자동 해제)"
            });
        }

        // 6. 마우스 영구 눌림 방지 안전망: MouseDown만 있고 대응 MouseUp이 없는 미해제 버튼이 있으면 마지막에 MouseUp 추가
        var unreleasedMouseButtons = new HashSet<MouseButtonType>();
        Point lastMousePt = Point.Empty;
        foreach (var act in mergedActions)
        {
            if (act.Type == ActionType.MouseClick)
            {
                lastMousePt = new Point(act.CustomX, act.CustomY);
                if (act.MouseMode == MouseStrokeMode.MouseDown)
                {
                    unreleasedMouseButtons.Add(act.MouseButton);
                }
                else if (act.MouseMode == MouseStrokeMode.MouseUp)
                {
                    unreleasedMouseButtons.Remove(act.MouseButton);
                }
            }
        }

        foreach (var unreleasedBtn in unreleasedMouseButtons)
        {
            mergedActions.Add(new MacroAction
            {
                Type = ActionType.MouseClick,
                MouseButton = unreleasedBtn,
                MouseMode = MouseStrokeMode.MouseUp,
                TargetMode = MouseTargetMode.Coordinates,
                CustomX = lastMousePt.X,
                CustomY = lastMousePt.Y,
                PostDelayMs = 0,
                Description = $"⬆️ 마우스업: {unreleasedBtn} ({lastMousePt.X}, {lastMousePt.Y}) (녹화 종료 자동 해제)"
            });
        }

        return mergedActions;
    }

    public void Dispose()
    {
        if (IsRecording)
        {
            StopRecording(false);
        }
    }
}

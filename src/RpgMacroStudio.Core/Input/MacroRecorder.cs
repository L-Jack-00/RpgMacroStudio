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

    private enum RawInputType
    {
        KeyDown,
        KeyUp,
        MouseDown,
        MouseUp
    }

    private class RawInputEvent
    {
        public long TimestampMs { get; set; }
        public RawInputType Type { get; set; }
        public Keys Key { get; set; }
        public MouseButtonType MouseButton { get; set; }
        public Point ScreenPoint { get; set; }
    }

    private IntPtr _keyboardHookId = IntPtr.Zero;
    private IntPtr _mouseHookId = IntPtr.Zero;
    private LowLevelProc? _keyboardProc;
    private LowLevelProc? _mouseProc;

    private readonly HashSet<Keys> _currentlyPressedKeys = new();
    private readonly HashSet<MouseButtonType> _currentlyPressedMouseButtons = new();

    private readonly Stopwatch _stopwatch = new();
    private readonly List<RawInputEvent> _recordedEvents = new();
    private readonly object _lock = new();

    public bool IsRecording { get; private set; } = false;

    public event Action<string>? OnRecordingStatusChanged;
    public event Action<string>? OnInputRecorded;

    public void StartRecording()
    {
        if (IsRecording) return;

        lock (_lock)
        {
            _recordedEvents.Clear();
            _currentlyPressedKeys.Clear();
            _currentlyPressedMouseButtons.Clear();
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
                long now = _stopwatch.ElapsedMilliseconds;
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
                long now = _stopwatch.ElapsedMilliseconds;
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

    private static List<MacroAction> ConvertToMacroActions(List<RawInputEvent> rawEvents)
    {
        var actions = new List<MacroAction>();
        if (rawEvents.Count == 0) return actions;

        long lastEventTime = rawEvents[0].TimestampMs;
        var pendingKeys = new Dictionary<Keys, long>();
        var pendingMouseDown = new Dictionary<MouseButtonType, (long Time, Point Pt)>();

        for (int i = 0; i < rawEvents.Count; i++)
        {
            var ev = rawEvents[i];

            long gap = ev.TimestampMs - lastEventTime;
            if (gap >= 20 && actions.Count > 0)
            {
                actions.Add(new MacroAction
                {
                    Type = ActionType.Wait,
                    WaitDurationMs = (int)gap,
                    Description = $"대기 {gap}ms"
                });
            }

            switch (ev.Type)
            {
                case RawInputType.KeyDown:
                    if (!pendingKeys.ContainsKey(ev.Key))
                    {
                        pendingKeys[ev.Key] = ev.TimestampMs;
                    }
                    break;

                case RawInputType.KeyUp:
                    int holdTime = 50;
                    if (pendingKeys.TryGetValue(ev.Key, out long downTime))
                    {
                        holdTime = Math.Clamp((int)(ev.TimestampMs - downTime), 10, 5000);
                        pendingKeys.Remove(ev.Key);
                    }
                    actions.Add(new MacroAction
                    {
                        Type = ActionType.KeyPress,
                        Key = ev.Key.ToString(),
                        HoldTimeMs = holdTime,
                        PostDelayMs = 30,
                        Description = $"키 입력: {ev.Key} ({holdTime}ms)"
                    });
                    break;

                case RawInputType.MouseDown:
                    pendingMouseDown[ev.MouseButton] = (ev.TimestampMs, ev.ScreenPoint);
                    break;

                case RawInputType.MouseUp:
                    Point pt = ev.ScreenPoint;
                    int mouseHold = 30;
                    if (pendingMouseDown.TryGetValue(ev.MouseButton, out var downInfo))
                    {
                        mouseHold = Math.Clamp((int)(ev.TimestampMs - downInfo.Time), 10, 3000);
                        pt = downInfo.Pt;
                        pendingMouseDown.Remove(ev.MouseButton);
                    }
                    actions.Add(new MacroAction
                    {
                        Type = ActionType.MouseClick,
                        MouseButton = ev.MouseButton,
                        TargetMode = MouseTargetMode.Coordinates,
                        CustomX = pt.X,
                        CustomY = pt.Y,
                        Description = $"마우스 {ev.MouseButton} 클릭 ({pt.X}, {pt.Y})"
                    });
                    break;
            }

            lastEventTime = ev.TimestampMs;
        }

        foreach (var kvp in pendingKeys)
        {
            actions.Add(new MacroAction
            {
                Type = ActionType.KeyPress,
                Key = kvp.Key.ToString(),
                HoldTimeMs = 50,
                PostDelayMs = 30,
                Description = $"키 입력: {kvp.Key} (50ms)"
            });
        }

        // 연속된 대기(Wait) 액션 자동 합산(Merge/Coalesce)
        var mergedActions = new List<MacroAction>();
        foreach (var act in actions)
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

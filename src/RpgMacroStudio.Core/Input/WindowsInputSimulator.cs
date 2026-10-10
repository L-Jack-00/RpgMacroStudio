using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace RpgMacroHarness.Input;

/// <summary>
/// 윈도우 OS의 Win32 API(keybd_event, SendInput, mouse_event)를 사용하여 마우스/키보드 입력을 시뮬레이션합니다.
/// 게임 클라이언트 메모리를 후킹하거나 변조하지 않는 순수 외부 입력 방식이며,
/// DirectInput/DirectX 게임 및 일반 윈도우 앱 모두에서 100% 실입력이 보장되도록 하드웨어 스캔코드와 휴머나이저(Humanizer)를 지원합니다.
/// </summary>
public static class WindowsInputSimulator
{
    private static readonly object _hardwareInputLock = new();
    private static readonly ConcurrentDictionary<Keys, byte> _activePressedKeys = new();
    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    // 64-bit 윈도우에서 sizeof(INPUT) = 40 bytes (Win32 API 완전 호환 구조체)
    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT
    {
        [FieldOffset(0)]
        public int type;
        [FieldOffset(8)]
        public MOUSEINPUT mi;
        [FieldOffset(8)]
        public KEYBDINPUT ki;
        [FieldOffset(8)]
        public HARDWAREINPUT hi;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_XDOWN = 0x0080;
    private const uint MOUSEEVENTF_XUP = 0x0100;
    private const uint XBUTTON1 = 0x0001;
    private const uint XBUTTON2 = 0x0002;

    /// <summary>
    /// 인간적인 손가락 누름 시간과 딜레이를 반영하여 키를 눌렀다 뗍니다.
    /// 하드웨어 스캔코드와 가상 키코드를 함께 주입하여 모든 게임/앱에서 확실하게 입력됩니다.
    /// </summary>
    /// <param name="vkCode">가상 키 코드 (예: Keys.Space, Keys.D1 등)</param>
    /// <param name="holdTimeBaseMs">누르고 있는 기본 시간 (기본 35ms)</param>
    /// <param name="postDelayBaseMs">누른 후 다음 입력까지의 기본 딜레이 (기본 50ms)</param>
    public static void SendKeyPress(Keys vkCode, int holdTimeBaseMs = 35, int postDelayBaseMs = 50)
    {
        lock (_hardwareInputLock)
        {
            byte vk = (byte)vkCode;
            byte scan = (byte)MapVirtualKey(vk, 0);

            // 방향키 및 특수키(Insert, Delete, Home, End 등) 확장키 플래그 판별
            bool isExtended = IsExtendedKey(vkCode);
            uint extFlag = isExtended ? KEYEVENTF_EXTENDEDKEY : 0u;

            // 1. 휴머나이저(Humanizer)로 인간적인 손가락 누름 및 반응 지연 산출
            int humanHold = InputHumanizer.GetHumanizedKeyHold(holdTimeBaseMs);
            int humanPost = InputHumanizer.GetHumanizedKeyPostDelay(postDelayBaseMs);

            // 2. 키 다운 (하드웨어 스캔코드와 가상 키코드를 함께 주입하여 단 1회 정확하게 입력)
            keybd_event(vk, scan, extFlag, UIntPtr.Zero);

            try
            {
                if (humanHold > 0) Thread.Sleep(humanHold);
            }
            finally
            {
                // 3. 키 업 (물리적 손가락 뗌 - 예외 또는 비상 정지 시에도 강제 해제 보장)
                keybd_event(vk, scan, extFlag | KEYEVENTF_KEYUP, UIntPtr.Zero);
            }

            // 4. 다음 동작 전 휴머나이저 지연
            if (humanPost > 0) Thread.Sleep(humanPost);
        }
    }

    /// <summary>
    /// 지정된 키를 물리적으로 누른 상태(KeyDown)로 유지합니다. (동시 입력 조합 시작)
    /// </summary>
    public static void SendKeyDown(Keys vkCode, int postDelayBaseMs = 0)
    {
        lock (_hardwareInputLock)
        {
            byte vk = (byte)vkCode;
            byte scan = (byte)MapVirtualKey(vk, 0);
            uint extFlag = IsExtendedKey(vkCode) ? KEYEVENTF_EXTENDEDKEY : 0u;

            keybd_event(vk, scan, extFlag, UIntPtr.Zero);
            _activePressedKeys[vkCode] = scan;

            int humanPost = InputHumanizer.GetHumanizedKeyPostDelay(postDelayBaseMs);
            if (humanPost > 0) Thread.Sleep(humanPost);
        }
    }

    /// <summary>
    /// 눌려있던 지정 키를 물리적으로 뗍니다(KeyUp). (동시 입력 종료)
    /// </summary>
    public static void SendKeyUp(Keys vkCode, int postDelayBaseMs = 0)
    {
        lock (_hardwareInputLock)
        {
            byte vk = (byte)vkCode;
            byte scan = (byte)MapVirtualKey(vk, 0);
            uint extFlag = IsExtendedKey(vkCode) ? KEYEVENTF_EXTENDEDKEY : 0u;

            keybd_event(vk, scan, extFlag | KEYEVENTF_KEYUP, UIntPtr.Zero);
            _activePressedKeys.TryRemove(vkCode, out _);

            int humanPost = InputHumanizer.GetHumanizedKeyPostDelay(postDelayBaseMs);
            if (humanPost > 0) Thread.Sleep(humanPost);
        }
    }

    /// <summary>
    /// 현재 물리적으로 눌려 있는(KeyDown 유지 중인) 키 목록을 반환합니다.
    /// </summary>
    public static IReadOnlyCollection<Keys> GetActivePressedKeys() => _activePressedKeys.Keys.ToList();

    /// <summary>
    /// 현재 물리적으로 눌려 있는(MouseDown 유지 중인) 마우스 버튼 목록을 반환합니다.
    /// </summary>
    public static IReadOnlyCollection<Data.MouseButtonType> GetActivePressedMouseButtons() => _activePressedMouseButtons.Keys.ToList();

    internal static void TrackPressedMouseButtonForTesting(Data.MouseButtonType button) => _activePressedMouseButtons[button] = 1;

    private static bool IsExtendedKey(Keys vkCode) =>
        vkCode == Keys.Up || vkCode == Keys.Down || vkCode == Keys.Left || vkCode == Keys.Right ||
        vkCode == Keys.Home || vkCode == Keys.End || vkCode == Keys.PageUp || vkCode == Keys.PageDown ||
        vkCode == Keys.Insert || vkCode == Keys.Delete;

    private static void TrySendInput(Keys vkCode, byte scan, bool isExtended, bool isKeyUp)
    {
        try
        {
            uint flags = 0;
            if (isExtended) flags |= KEYEVENTF_EXTENDEDKEY;
            if (isKeyUp) flags |= KEYEVENTF_KEYUP;

            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].ki.wVk = (ushort)vkCode;
            inputs[0].ki.wScan = scan;
            inputs[0].ki.dwFlags = flags;
            inputs[0].ki.time = 0;
            inputs[0].ki.dwExtraInfo = IntPtr.Zero;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
        catch { }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Win32Point lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Point
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// 인간적인 손 움직임을 모사하여 현재 마우스 커서 위치에서 목표 좌표까지 부드러운 곡선(베지어 보간)으로 이동합니다.
    /// 기계적인 순간이동(Teleport) 감지를 원천 차단합니다.
    /// </summary>
    public static void MoveMouseSmoothly(int targetX, int targetY, int durationMs = 35)
    {
        if (!GetCursorPos(out var start))
        {
            SetCursorPos(targetX, targetY);
            return;
        }

        int dx = targetX - start.X;
        int dy = targetY - start.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        // 거리가 20px 미만이거나 초고속 모드 시 즉시 설정
        if (dist < 20 || durationMs <= 5)
        {
            SetCursorPos(targetX, targetY);
            return;
        }

        int steps = Math.Clamp((int)(dist / 25), 4, 12);
        int stepDelay = Math.Max(2, durationMs / steps);

        // 미세한 곡선 제어점 생성 (직선이 아닌 인간다운 미세 휨 모사)
        int ctrlX = start.X + dx / 2 + Random.Shared.Next(-15, 16);
        int ctrlY = start.Y + dy / 2 + Random.Shared.Next(-15, 16);

        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            double u = 1.0 - t;
            int curX = (int)(u * u * start.X + 2 * u * t * ctrlX + t * t * targetX);
            int curY = (int)(u * u * start.Y + 2 * u * t * ctrlY + t * t * targetY);

            SetCursorPos(curX, curY);
            Thread.Sleep(stepDelay);
        }

        SetCursorPos(targetX, targetY);
    }

    private static readonly ConcurrentDictionary<Data.MouseButtonType, byte> _activePressedMouseButtons = new();

    private static void GetMouseButtonFlags(Data.MouseButtonType button, out uint downFlag, out uint upFlag, out uint mouseData)
    {
        downFlag = MOUSEEVENTF_LEFTDOWN;
        upFlag = MOUSEEVENTF_LEFTUP;
        mouseData = 0;

        switch (button)
        {
            case Data.MouseButtonType.Left:
                downFlag = MOUSEEVENTF_LEFTDOWN;
                upFlag = MOUSEEVENTF_LEFTUP;
                break;
            case Data.MouseButtonType.Right:
                downFlag = MOUSEEVENTF_RIGHTDOWN;
                upFlag = MOUSEEVENTF_RIGHTUP;
                break;
            case Data.MouseButtonType.Middle:
                downFlag = MOUSEEVENTF_MIDDLEDOWN;
                upFlag = MOUSEEVENTF_MIDDLEUP;
                break;
            case Data.MouseButtonType.XButton1:
                downFlag = MOUSEEVENTF_XDOWN;
                upFlag = MOUSEEVENTF_XUP;
                mouseData = XBUTTON1;
                break;
            case Data.MouseButtonType.XButton2:
                downFlag = MOUSEEVENTF_XDOWN;
                upFlag = MOUSEEVENTF_XUP;
                mouseData = XBUTTON2;
                break;
        }
    }

    /// <summary>
    /// 휴머나이저를 적용하여 지정된 화면 좌표로 마우스를 이동하고 자연스럽게 클릭합니다.
    /// (미세 손떨림 오차, 인간 곡선 궤적 이동, 클릭 유지 시간, 릴리즈 지연 반영)
    /// </summary>
    public static void SendMouseClick(int x, int y, Data.MouseButtonType button = Data.MouseButtonType.Left, int holdTimeBaseMs = 35, int postDelayBaseMs = 50)
    {
        lock (_hardwareInputLock)
        {
            // 1. 휴머나이저: 기계적인 정확도 탐지를 피하기 위한 ±1~2px 미세 손떨림 좌표 생성
            var (hx, hy) = InputHumanizer.GetHumanizedCoordinates(x, y, 2);

            // 2. 인간적인 베지어 곡선 이동 및 착지 지연
            int settling = InputHumanizer.GetMouseSettlingDelay();
            MoveMouseSmoothly(hx, hy, settling);

            GetMouseButtonFlags(button, out uint downFlag, out uint upFlag, out uint mouseData);

            // 3. 마우스 버튼 누름
            mouse_event(downFlag, 0, 0, mouseData, UIntPtr.Zero);

            try
            {
                // 4. 인간적인 손가락 누름 유지 시간 반영
                int humanHold = Math.Max(InputHumanizer.GetHumanizedMouseHold(), InputHumanizer.GetHumanizedKeyHold(holdTimeBaseMs));
                Thread.Sleep(humanHold);
            }
            finally
            {
                // 5. 마우스 버튼 뗌 (예외 또는 중단 시에도 강제 해제)
                mouse_event(upFlag, 0, 0, mouseData, UIntPtr.Zero);
            }

            // 6. 클릭 후 회복 지연
            int humanPost = Math.Max(InputHumanizer.GetMouseReleaseDelay(), InputHumanizer.GetHumanizedKeyPostDelay(postDelayBaseMs));
            if (humanPost > 0) Thread.Sleep(humanPost);
        }
    }

    /// <summary>
    /// 지정된 화면 좌표로 마우스를 이동하고 마우스 버튼을 누른 상태(MouseDown)로 유지합니다. (드래그 및 동시 조작 시작)
    /// </summary>
    public static void SendMouseDown(int x, int y, Data.MouseButtonType button = Data.MouseButtonType.Left, int postDelayBaseMs = 0)
    {
        lock (_hardwareInputLock)
        {
            var (hx, hy) = InputHumanizer.GetHumanizedCoordinates(x, y, 2);
            int settling = InputHumanizer.GetMouseSettlingDelay();
            MoveMouseSmoothly(hx, hy, settling);

            GetMouseButtonFlags(button, out uint downFlag, out _, out uint mouseData);
            mouse_event(downFlag, 0, 0, mouseData, UIntPtr.Zero);
            _activePressedMouseButtons[button] = 1;

            int humanPost = InputHumanizer.GetHumanizedKeyPostDelay(postDelayBaseMs);
            if (humanPost > 0) Thread.Sleep(humanPost);
        }
    }

    /// <summary>
    /// 지정된 화면 좌표로 마우스를 이동하고 눌려있던 마우스 버튼을 물리적으로 뗍니다(MouseUp). (드래그 및 조작 종료)
    /// </summary>
    public static void SendMouseUp(int x, int y, Data.MouseButtonType button = Data.MouseButtonType.Left, int postDelayBaseMs = 0)
    {
        lock (_hardwareInputLock)
        {
            var (hx, hy) = InputHumanizer.GetHumanizedCoordinates(x, y, 2);
            int settling = InputHumanizer.GetMouseSettlingDelay();
            MoveMouseSmoothly(hx, hy, settling);

            GetMouseButtonFlags(button, out _, out uint upFlag, out uint mouseData);
            mouse_event(upFlag, 0, 0, mouseData, UIntPtr.Zero);
            _activePressedMouseButtons.TryRemove(button, out _);

            int humanPost = InputHumanizer.GetHumanizedKeyPostDelay(postDelayBaseMs);
            if (humanPost > 0) Thread.Sleep(humanPost);
        }
    }

    /// <summary>
    /// 긴급 중단 또는 엔진 정지 시 물리적으로 눌려있을 수 있는 마우스 및 키보드 입력을 강제 해제합니다.
    /// </summary>
    public static void ReleaseAllPressedInputs()
    {
        try
        {
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_XUP, 0, 0, XBUTTON1, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_XUP, 0, 0, XBUTTON2, UIntPtr.Zero);

            lock (_hardwareInputLock)
            {
                foreach (var kvp in _activePressedMouseButtons)
                {
                    GetMouseButtonFlags(kvp.Key, out _, out uint upFlag, out uint mouseData);
                    mouse_event(upFlag, 0, 0, mouseData, UIntPtr.Zero);
                }
                _activePressedMouseButtons.Clear();
            }

            // 1. SendKeyDown으로 물리적으로 눌린 채 유지 중인 모든 키 강제 해제 (키 씹힘/영구 눌림 원천 방지)
            lock (_hardwareInputLock)
            {
                foreach (var kvp in _activePressedKeys)
                {
                    byte vk = (byte)kvp.Key;
                    byte scan = kvp.Value;
                    uint extFlag = IsExtendedKey(kvp.Key) ? KEYEVENTF_EXTENDEDKEY : 0u;
                    keybd_event(vk, scan, extFlag | KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
                _activePressedKeys.Clear();
            }

            // 2. 주요 제어키(Ctrl, Shift, Alt 등) 릴리즈
            Keys[] modifierKeys = [Keys.ControlKey, Keys.ShiftKey, Keys.Menu, Keys.LWin, Keys.RWin];
            foreach (var k in modifierKeys)
            {
                byte vk = (byte)k;
                byte scan = (byte)MapVirtualKey(vk, 0);
                keybd_event(vk, scan, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
        }
        catch { }
    }
}

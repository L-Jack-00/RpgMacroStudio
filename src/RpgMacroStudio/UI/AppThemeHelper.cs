using System.Drawing;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

/// <summary>
/// 다크 모드 및 라이트 모드 전환 시 메인 폼, 인스펙터, 설정창 및 모든 팝업 다이얼로그의 일관된 테마를 적용하는 통합 헬퍼입니다.
/// 버튼 텍스트의 줄바꿈 방지(AutoEllipsis) 및 라이트 모드에서의 비활성화 버튼 스타일(동일 배경색 + 연한 회색 텍스트)을 일괄 보장합니다.
/// </summary>
public static class AppThemeHelper
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetPreferredAppMode(int mode);

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", EntryPoint = "#133", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool AllowDarkModeForWindow(IntPtr hWnd, bool allow);

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    public static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private const uint WM_NCACTIVATE = 0x0086;
    private const uint WM_NCPAINT = 0x0085;

    private const uint RDW_INVALIDATE = 0x0001;
    private const uint RDW_UPDATENOW = 0x0100;
    private const uint RDW_ERASENOW = 0x0200;
    private const uint RDW_FRAME = 0x0400;

    private const uint LVM_GETHEADER = 0x101F;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool UpdateWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);
    public const int SB_HORZ = 0;
    public const int SB_VERT = 1;
    public const int SB_CTL = 2;
    public const int SB_BOTH = 3;

    /// <summary>
    /// 전역 통일 테마 색상 팔레트 시스템입니다.
    /// 모든 폼, 패널, 컨트롤, 다이얼로그 및 스크롤바가 이 표준 팔레트를 참조하여 일관된 룩앤필을 보장합니다.
    /// </summary>
    public static class Palette
    {
        public static class Dark
        {
            public static readonly Color Background = Color.FromArgb(18, 24, 34);
            public static readonly Color Surface = Color.FromArgb(22, 30, 42);
            public static readonly Color SurfaceElevated = Color.FromArgb(26, 34, 48);
            public static readonly Color InputBackground = Color.FromArgb(13, 17, 24);
            public static readonly Color HeaderBackground = Color.FromArgb(24, 32, 46);
            public static readonly Color TopBarBackground = Color.FromArgb(20, 26, 36);

            public static readonly Color Border = Color.FromArgb(44, 58, 80);
            public static readonly Color BorderSubtle = Color.FromArgb(32, 42, 58);

            public static readonly Color TextPrimary = Color.FromArgb(235, 240, 245);
            public static readonly Color TextSecondary = Color.FromArgb(160, 175, 195);
            public static readonly Color TextMuted = Color.FromArgb(110, 125, 145);

            public static readonly Color AccentBlue = Color.FromArgb(41, 128, 185);
            public static readonly Color AccentGreen = Color.FromArgb(39, 174, 96);
            public static readonly Color AccentRed = Color.FromArgb(192, 57, 43);
            public static readonly Color AccentOrange = Color.FromArgb(211, 84, 0);

            public static readonly Color SelectionBg = Color.FromArgb(41, 65, 95);
            public static readonly Color SelectionFg = Color.White;

            // 다크모드 스크롤바 색상 (가시성 확보를 위해 기존 55, 70, 95 대비 밝고 부드럽게 조정)
            public static readonly Color ScrollTrack = Color.FromArgb(22, 28, 38);
            public static readonly Color ScrollThumb = Color.FromArgb(70, 88, 118);
            public static readonly Color ScrollThumbHover = Color.FromArgb(100, 122, 158);
            public static readonly Color ScrollThumbDrag = Color.FromArgb(130, 155, 195);
        }

        public static class Light
        {
            public static readonly Color Background = Color.FromArgb(248, 249, 251);
            public static readonly Color Surface = Color.FromArgb(245, 247, 250);
            public static readonly Color SurfaceElevated = Color.White;
            public static readonly Color InputBackground = Color.White;
            public static readonly Color HeaderBackground = SystemColors.Control;
            public static readonly Color TopBarBackground = Color.FromArgb(246, 248, 250);

            public static readonly Color Border = Color.FromArgb(210, 218, 226);
            public static readonly Color BorderSubtle = Color.FromArgb(228, 232, 238);

            public static readonly Color TextPrimary = Color.FromArgb(20, 25, 30);
            public static readonly Color TextSecondary = Color.FromArgb(75, 85, 95);
            public static readonly Color TextMuted = Color.FromArgb(130, 140, 150);

            public static readonly Color AccentBlue = Color.FromArgb(52, 152, 219);
            public static readonly Color AccentGreen = Color.FromArgb(46, 204, 113);
            public static readonly Color AccentRed = Color.FromArgb(231, 76, 60);
            public static readonly Color AccentOrange = Color.FromArgb(230, 126, 34);

            public static readonly Color SelectionBg = Color.FromArgb(215, 230, 248);
            public static readonly Color SelectionFg = Color.FromArgb(20, 40, 70);

            public static readonly Color ScrollTrack = Color.FromArgb(240, 243, 246);
            public static readonly Color ScrollThumb = Color.FromArgb(185, 195, 208);
            public static readonly Color ScrollThumbHover = Color.FromArgb(150, 162, 178);
            public static readonly Color ScrollThumbDrag = Color.FromArgb(120, 132, 148);
        }
    }

    /// <summary>
    /// 현재 활성화된 테마가 다크 모드인지 여부를 실시간 추적합니다. (설정창 미리보기 및 버튼 상태 갱신에 사용)
    /// </summary>
    public static bool CurrentThemeIsDark { get; set; } = AppSettings.Current.IsDarkMode;

    /// <summary>
    /// 설정창 등에서 테마를 변경할 때 뒤에 있는 메인 창 및 열린 폼들이 지연 없이 실시간으로 반응할 수 있도록 알리는 이벤트입니다.
    /// </summary>
    public static event Action<bool>? ThemePreviewChanged;

    public static void NotifyThemePreview(bool isDark)
    {
        CurrentThemeIsDark = isDark;
        ThemePreviewChanged?.Invoke(isDark);
    }

    public static void ApplyTheme(Form form, bool isDark)
    {
        CurrentThemeIsDark = isDark;
        form.BackColor = isDark ? Color.FromArgb(18, 24, 34) : Color.FromArgb(248, 249, 251);
        form.ForeColor = isDark ? Color.White : Color.FromArgb(44, 62, 80);

        if (form.IsHandleCreated)
        {
            UpdateFormTitleBarTheme(form, isDark);
        }
        else
        {
            // 폼 생성자 호출 시점에는 Handle이 아직 생성되지 않았으므로 핸들 생성 즉시 타이틀바 다크 테마 적용
            form.HandleCreated += (s, e) => UpdateFormTitleBarTheme(form, isDark);
        }

        form.Shown += (s, e) => UpdateFormTitleBarTheme(form, isDark);

        ApplyThemeToControls(form, isDark);
    }

    public static void UpdateFormTitleBarTheme(Form form, bool isDark)
    {
        try
        {
            if (!form.IsHandleCreated || form.IsDisposed) return;

            // DWM Immersive Dark Mode 속성 (20: Win10 20H1+, Win11 / 19: Win10 1809-1909)
            int val = isDark ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, 20, ref val, sizeof(int));
            DwmSetWindowAttribute(form.Handle, 19, ref val, sizeof(int));

            // Windows 11 DWM 타이틀바 캡션 및 텍스트 색상 직접 제어 (35: CAPTION_COLOR, 36: TEXT_COLOR)
            int captionColor = isDark ? 0x00221812 : 0x00FBFAF8; // 0x00BBGGRR 형식
            int textColor = isDark ? 0x00FFFFFF : 0x00503E2C;
            DwmSetWindowAttribute(form.Handle, 35, ref captionColor, sizeof(int));
            DwmSetWindowAttribute(form.Handle, 36, ref textColor, sizeof(int));

            try
            {
                SetPreferredAppMode(isDark ? 2 : 3);
                AllowDarkModeForWindow(form.Handle, isDark);
            }
            catch { }

            // 마우스 호버나 윈도우 클릭을 기다리지 않고 DWM 타이틀바 프레임을 0ms 지연 없이 즉시 다시 그리도록 강제 전송
            SetWindowPos(form.Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            RedrawWindow(form.Handle, IntPtr.Zero, IntPtr.Zero, RDW_FRAME | RDW_INVALIDATE | RDW_UPDATENOW | RDW_ERASENOW);
            SendMessage(form.Handle, WM_NCPAINT, (IntPtr)1, IntPtr.Zero);
            SendMessage(form.Handle, WM_NCACTIVATE, (IntPtr)(Form.ActiveForm == form ? 1 : 0), IntPtr.Zero);
        }
        catch { }
    }

    public static void ApplyThemeToControls(Control parent, bool isDark)
    {
        foreach (Control c in parent.Controls)
        {
            ApplyThemeToControl(c, isDark);
            if (c.HasChildren && !(c is NumericUpDown) && !(c is ComboBox))
            {
                ApplyThemeToControls(c, isDark);
            }
        }
    }

    public static void ApplyThemeToControl(Control c, bool isDark)
    {
        if (c is Button btn)
        {
            // 의도된 멀티라인/강조 버튼(PrimaryBlueButton 등)을 제외하고 버튼 내 텍스트 줄바꿈 제거
            string tagStr = btn.Tag as string ?? "";
            if (!tagStr.Contains("PrimaryBlueButton") && !tagStr.Contains("KeepMultiline"))
            {
                if (btn.Text.Contains('\n') || btn.Text.Contains('\r'))
                {
                    btn.Text = btn.Text.Replace("\r", "").Replace("\n", " ").Trim();
                }
                btn.AutoEllipsis = true;
            }

            // 이미 테마 이벤트가 등록되어 있지 않은 경우에만 EnabledChanged 리스너 연결
            if (btn.Tag as string != "ThemeHandled" && !tagStr.Contains("ThemeHandled"))
            {
                btn.EnabledChanged += (s, e) =>
                {
                    UpdateButtonState(btn, CurrentThemeIsDark);
                    btn.Invalidate();
                };
                btn.Paint += (s, e) => DrawCustomDisabledButton(btn, e, CurrentThemeIsDark);
                if (string.IsNullOrEmpty(tagStr)) btn.Tag = "ThemeHandled";
            }

            UpdateButtonState(btn, isDark);
            btn.Invalidate();
        }
        else if (c is GroupBox gb)
        {
            gb.ForeColor = isDark ? Color.FromArgb(160, 200, 255) : Color.FromArgb(41, 128, 185);
            gb.BackColor = isDark ? Color.FromArgb(22, 30, 42) : Color.FromArgb(245, 247, 250);
        }
        else if (c is Label lbl)
        {
            // 구분선 및 특수 강조 라벨은 고유 색상 유지
            if (lbl.BorderStyle == BorderStyle.Fixed3D) return;
            if (lbl.ForeColor == Color.Red || lbl.ForeColor == Color.DarkRed || lbl.ForeColor == Color.DarkGreen || lbl.ForeColor == Color.FromArgb(39, 174, 96)) return;

            lbl.ForeColor = isDark ? Color.White : Color.FromArgb(44, 62, 80);
        }
        else if (c is TextBox tb)
        {
            tb.BackColor = isDark ? Color.FromArgb(26, 34, 48) : Color.White;
            tb.ForeColor = isDark ? Color.White : Color.Black;
            if (tb.IsHandleCreated)
            {
                try { SetWindowTheme(tb.Handle, isDark ? "DarkMode_Explorer" : null, null); } catch { }
            }
            else
            {
                tb.HandleCreated += (s, e) =>
                {
                    try { SetWindowTheme(tb.Handle, CurrentThemeIsDark ? "DarkMode_Explorer" : null, null); } catch { }
                };
            }
        }
        else if (c is RichTextBox rtb)
        {
            rtb.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
            rtb.ForeColor = isDark ? Color.White : Color.Black;
            if (rtb.IsHandleCreated)
            {
                try { SetWindowTheme(rtb.Handle, isDark ? "DarkMode_Explorer" : null, null); } catch { }
            }
            else
            {
                rtb.HandleCreated += (s, e) =>
                {
                    try { SetWindowTheme(rtb.Handle, CurrentThemeIsDark ? "DarkMode_Explorer" : null, null); } catch { }
                };
            }
        }
        else if (c is CustomCheckBox customChk)
        {
            customChk.IsDark = isDark;
            customChk.Invalidate();
        }
        else if (c is CustomComboBox customCbo)
        {
            customCbo.IsDark = isDark;
            customCbo.Invalidate();
        }
        else if (c is CustomRadioButton customRb)
        {
            customRb.IsDark = isDark;
            customRb.Invalidate();
        }
        else if (c is DarkScrollBar dsb)
        {
            dsb.IsDark = isDark;
            dsb.Invalidate();
        }
        else if (c is NumericUpDown num)
        {
            num.BackColor = isDark ? Palette.Dark.InputBackground : Palette.Light.InputBackground;
            num.ForeColor = isDark ? Palette.Dark.TextPrimary : Palette.Light.TextPrimary;
        }
        else if (c is ComboBox cb)
        {
            cb.FlatStyle = FlatStyle.Flat;
            cb.BackColor = isDark ? Palette.Dark.InputBackground : Palette.Light.InputBackground;
            cb.ForeColor = isDark ? Palette.Dark.TextPrimary : Palette.Light.TextPrimary;
            cb.Tag = isDark ? "DarkCombo" : "LightCombo";

            if (cb.DrawMode != DrawMode.OwnerDrawFixed)
            {
                cb.DrawMode = DrawMode.OwnerDrawFixed;
                cb.DrawItem += (s, e) =>
                {
                    if (e.Index < 0 || e.Index >= cb.Items.Count) return;
                    bool dark = (cb.Tag as string) == "DarkCombo" || CurrentThemeIsDark;
                    bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

                    Color itemBg = isSelected
                        ? (dark ? Palette.Dark.SelectionBg : Palette.Light.SelectionBg)
                        : (dark ? Palette.Dark.InputBackground : Palette.Light.InputBackground);

                    Color itemFg = isSelected
                        ? (dark ? Palette.Dark.SelectionFg : Palette.Light.SelectionFg)
                        : (dark ? Palette.Dark.TextPrimary : Palette.Light.TextPrimary);

                    using (var brush = new SolidBrush(itemBg))
                    {
                        e.Graphics.FillRectangle(brush, e.Bounds);
                    }

                    string itemText = cb.GetItemText(cb.Items[e.Index]) ?? "";
                    using (var textBrush = new SolidBrush(itemFg))
                    {
                        var textRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                        AppFontHelper.DrawMixedString(e.Graphics, itemText, 9f, textBrush, textRect, StringAlignment.Near, StringAlignment.Center, FontStyle.Regular);
                    }
                };
            }
            try { SetWindowTheme(cb.Handle, isDark ? "DarkMode_Explorer" : null, null); } catch { }
            cb.Invalidate();
        }
        else if (c is CheckBox chk)
        {
            chk.ForeColor = isDark ? Palette.Dark.TextPrimary : Palette.Light.TextPrimary;
        }
        else if (c is RadioButton rb)
        {
            rb.ForeColor = isDark ? Palette.Dark.TextPrimary : Palette.Light.TextPrimary;
        }
        else if (c is ListView lv)
        {
            lv.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
            lv.ForeColor = isDark ? Color.White : Color.Black;
            if (lv.IsHandleCreated)
            {
                try { SetWindowTheme(lv.Handle, isDark ? "DarkMode_Explorer" : null, null); } catch { }
                RefreshListViewHeader(lv, isDark);
            }
            else
            {
                lv.HandleCreated += (s, e) =>
                {
                    try { SetWindowTheme(lv.Handle, CurrentThemeIsDark ? "DarkMode_Explorer" : null, null); } catch { }
                    RefreshListViewHeader(lv, CurrentThemeIsDark);
                };
            }
            lv.Invalidate();
        }
        else if (c is Panel pnl && !(c is UserControl))
        {
            // 색상 샘플 패널 등 크기가 작은 특수 뷰는 고유 배경색 유지
            if (pnl.Width <= 50 && pnl.Height <= 50) return;
            pnl.BackColor = isDark ? Color.FromArgb(22, 30, 42) : Color.FromArgb(245, 247, 250);
            if (pnl.IsHandleCreated)
            {
                try { SetWindowTheme(pnl.Handle, isDark ? "DarkMode_Explorer" : null, null); } catch { }
            }
            else
            {
                pnl.HandleCreated += (s, e) =>
                {
                    try { SetWindowTheme(pnl.Handle, CurrentThemeIsDark ? "DarkMode_Explorer" : null, null); } catch { }
                };
            }
        }
        else if (c is TreeView tv)
        {
            tv.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
            tv.ForeColor = isDark ? Color.White : Color.Black;
            if (tv.IsHandleCreated)
            {
                try { SetWindowTheme(tv.Handle, isDark ? "DarkMode_Explorer" : null, null); } catch { }
            }
            else
            {
                tv.HandleCreated += (s, e) =>
                {
                    try { SetWindowTheme(tv.Handle, CurrentThemeIsDark ? "DarkMode_Explorer" : null, null); } catch { }
                };
            }
        }
        else if (c is ListBox lb)
        {
            lb.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
            lb.ForeColor = isDark ? Color.White : Color.Black;
            if (lb.IsHandleCreated)
            {
                try { SetWindowTheme(lb.Handle, isDark ? "DarkMode_Explorer" : null, null); } catch { }
            }
            else
            {
                lb.HandleCreated += (s, e) =>
                {
                    try { SetWindowTheme(lb.Handle, CurrentThemeIsDark ? "DarkMode_Explorer" : null, null); } catch { }
                };
            }
        }
    }

    /// <summary>
    /// ListView의 컬럼 헤더(SysHeader32) 창을 강제 무효화 및 갱신하여 라이트/다크 모드 변경 시 즉시 반영되도록 합니다.
    /// </summary>
    public static void RefreshListViewHeader(ListView lv, bool isDark)
    {
        if (lv == null || !lv.IsHandleCreated || lv.IsDisposed) return;
        try
        {
            IntPtr hHeader = SendMessage(lv.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
            if (hHeader != IntPtr.Zero)
            {
                try { SetWindowTheme(hHeader, isDark ? "DarkMode_ItemsView" : null, null); } catch { }
                InvalidateRect(hHeader, IntPtr.Zero, true);
                UpdateWindow(hHeader);
                RedrawWindow(hHeader, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_UPDATENOW | RDW_ERASENOW | RDW_FRAME);
            }
        }
        catch { }
    }

    public static void UpdateButtonState(Button btn, bool isDark)
    {
        // Undo / Redo 버튼은 전용 스타일 분기 처리
        if (btn.Text == "↶" || btn.Text == "↷" || btn.Name == "_btnUndo" || btn.Name == "_btnRedo")
        {
            UpdateUndoRedoButtonState(btn, isDark);
            return;
        }

        // 확인/저장/시작/도우미 마법사 등 강조 버튼(초록/파랑/빨강)의 기본 배경은 유지하고 비활성화 시 적절히 디밍
        bool isPrimaryGreen = (btn.BackColor.G > 180 && btn.BackColor.R < 100);
        bool isPrimaryRed = (btn.BackColor.R > 180 && btn.BackColor.G < 100);
        bool isPrimaryBlue = ((btn.Tag as string)?.Contains("PrimaryBlueButton") == true) || (btn.BackColor.B > 170 && btn.BackColor.R < 60);

        if (isPrimaryGreen || isPrimaryRed || isPrimaryBlue)
        {
            if (btn.Enabled)
            {
                btn.ForeColor = Color.White;
            }
            else
            {
                btn.ForeColor = isDark ? Color.FromArgb(140, 140, 140) : Color.FromArgb(180, 188, 200);
            }
            btn.Invalidate();
            return;
        }

        btn.FlatStyle = FlatStyle.Flat;

        if (isDark)
        {
            if (btn.Enabled)
            {
                btn.BackColor = Color.FromArgb(26, 34, 48);
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = Color.FromArgb(44, 58, 80);
                btn.FlatAppearance.BorderSize = 1;
            }
            else
            {
                btn.BackColor = Color.FromArgb(18, 22, 28);
                btn.ForeColor = Color.FromArgb(85, 95, 110);
                btn.FlatAppearance.BorderColor = Color.FromArgb(30, 36, 46);
                btn.FlatAppearance.BorderSize = 1;
            }
        }
        else
        {
            if (btn.Enabled)
            {
                btn.BackColor = SystemColors.ControlLight;
                btn.ForeColor = Color.Black;
                btn.FlatAppearance.BorderColor = Color.FromArgb(200, 208, 218);
                btn.FlatAppearance.BorderSize = 1;
            }
            else
            {
                // 라이트 모드 비활성화: 뽀얗게(milky soft pale) 처리
                btn.BackColor = Color.FromArgb(246, 248, 251);
                btn.ForeColor = Color.FromArgb(180, 188, 200);
                btn.FlatAppearance.BorderColor = Color.FromArgb(226, 230, 238);
                btn.FlatAppearance.BorderSize = 1;
            }
        }

        btn.Invalidate();
    }

    /// <summary>
    /// 실행 취소(Undo) / 다시 실행(Redo) 버튼 전용 상태 렌더러입니다.
    /// 라이트 모드 활성화 시 상단 툴바와 어울리는 라이트 배경에 선명한 텍스트, 비활성화 시 뽀얗게(milky) 처리합니다.
    /// </summary>
    public static void UpdateUndoRedoButtonState(Button btn, bool isDark)
    {
        btn.FlatStyle = FlatStyle.Flat;

        if (isDark)
        {
            if (btn.Enabled)
            {
                btn.BackColor = Color.FromArgb(26, 34, 48);
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = Color.FromArgb(44, 58, 80);
                btn.FlatAppearance.BorderSize = 1;
            }
            else
            {
                btn.BackColor = Color.FromArgb(18, 22, 28);
                btn.ForeColor = Color.FromArgb(85, 95, 110);
                btn.FlatAppearance.BorderColor = Color.FromArgb(30, 36, 46);
                btn.FlatAppearance.BorderSize = 1;
            }
        }
        else
        {
            if (btn.Enabled)
            {
                // 라이트 모드 활성화: 상단 툴바(열기, 저장)와 일치하는 라이트 배경 + 진한 텍스트
                btn.BackColor = SystemColors.ControlLight;
                btn.ForeColor = Color.FromArgb(44, 62, 80);
                btn.FlatAppearance.BorderColor = Color.FromArgb(200, 208, 218);
                btn.FlatAppearance.BorderSize = 1;
            }
            else
            {
                // 라이트 모드 비활성화: 뽀얗게(milky soft pale) 처리
                btn.BackColor = Color.FromArgb(246, 248, 251);
                btn.ForeColor = Color.FromArgb(180, 188, 200);
                btn.FlatAppearance.BorderColor = Color.FromArgb(226, 230, 238);
                btn.FlatAppearance.BorderSize = 1;
            }
        }

        btn.Invalidate();
    }

    /// <summary>
    /// WinForms 버튼이 비활성화(Enabled == false)되었을 때 표시되는 고질적인 3D 엠보싱(어두운 그림자)을 덮고,
    /// 현대적인 뽀얀(milky soft pale) 비활성화 스타일로 완벽하게 렌더링합니다.
    /// </summary>
    public static void DrawCustomDisabledButton(Button btn, PaintEventArgs e, bool isDark)
    {
        if (btn.Enabled) return;

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Color bg;
        Color fg;
        Color border;

        if (isDark)
        {
            bg = Color.FromArgb(18, 22, 28);
            fg = Color.FromArgb(85, 95, 110);
            border = Color.FromArgb(30, 36, 46);
        }
        else
        {
            // 라이트 모드: 비활성화된 버튼과 텍스트를 뽀얗게(milky soft pale) 처리!
            bg = Color.FromArgb(246, 248, 251);       // 뽀얀 소프트 밀키 배경
            fg = Color.FromArgb(180, 188, 200);       // 뽀얗고 부드러운 연회색-화이트 텍스트
            border = Color.FromArgb(226, 230, 238);   // 뽀얀 은은한 테두리
        }

        using (var brush = new SolidBrush(bg))
        {
            g.FillRectangle(brush, btn.ClientRectangle);
        }

        using (var pen = new Pen(border, 1))
        {
            g.DrawRectangle(pen, 0, 0, btn.Width - 1, btn.Height - 1);
        }

        using (var textBrush = new SolidBrush(fg))
        {
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.DrawString(btn.Text, btn.Font, textBrush, btn.ClientRectangle, sf);
        }
    }
}

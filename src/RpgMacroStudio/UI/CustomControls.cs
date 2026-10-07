using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

/// <summary>
/// 라이트 모드와 다크 모드를 매끄럽게 넘나드는 고품질 벡터 커스텀 체크박스입니다.
/// Windows 기본 픽셀화된 체크박스를 대체하여 모던하고 부드러운 라운딩 박스와 안티에일리어싱 체크 마크(✔)를 제공합니다.
/// </summary>
public class CustomCheckBox : CheckBox
{
    private bool _isHovered = false;
    private bool _isDark = AppThemeHelper.CurrentThemeIsDark;

    public bool IsDark
    {
        get => _isDark;
        set
        {
            if (_isDark != value)
            {
                _isDark = value;
                Invalidate();
            }
        }
    }

    public CustomCheckBox()
    {
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = AppFontHelper.GetDefaultFont(9.5f);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    private Color GetEffectiveBackColor()
    {
        if (BackColor != Color.Transparent && BackColor.A > 0)
        {
            return BackColor;
        }
        Control? current = Parent;
        while (current != null)
        {
            if (current.BackColor != Color.Transparent && current.BackColor.A > 0)
            {
                return current.BackColor;
            }
            current = current.Parent;
        }
        return _isDark ? AppThemeHelper.Palette.Dark.Background : AppThemeHelper.Palette.Light.Background;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return new Size(24, 24);
        }

        using var bmp = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bmp);
        using var sfTypo = new StringFormat(StringFormat.GenericTypographic);
        var (iconPart, textPart) = AppFontHelper.SplitEmojiAndText(Text);

        float iconWidth = 0;
        if (!string.IsNullOrEmpty(iconPart))
        {
            using var iconFont = AppFontHelper.GetEmojiFont(Font?.Size ?? 9.5f, FontStyle.Regular);
            var iconSz = g.MeasureString(iconPart, iconFont, PointF.Empty, sfTypo);
            iconWidth = Math.Max((Font?.Size ?? 9.5f) + 2, iconSz.Width);
        }

        float textWidth = 0;
        if (!string.IsNullOrEmpty(textPart))
        {
            using var textFont = AppFontHelper.GetDefaultFont(Font?.Size ?? 9.5f, Font?.Style ?? FontStyle.Regular);
            textWidth = g.MeasureString(textPart, textFont, PointF.Empty, sfTypo).Width;
        }

        float gap = (!string.IsNullOrEmpty(iconPart) && !string.IsNullOrEmpty(textPart)) ? 5f : 0f;
        // 16px 체크박스 + 8px 간격 + 아이콘 폭 + 5px 간격 + 텍스트 폭 + 14px 안전 버퍼
        int totalWidth = (int)Math.Ceiling((double)(16 + 8 + iconWidth + gap + textWidth + 14));
        int totalHeight = Math.Max(24, (int)Math.Ceiling((double)((Font?.Height ?? 18) + 4)));

        return new Size(totalWidth, totalHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 부모 배경색으로 클라이언트 영역을 완전히 지워 이전 텍스트 잔상 및 폰트 겹침 원천 방지
        Color effectiveBg = GetEffectiveBackColor();
        using (var bgClearBrush = new SolidBrush(effectiveBg))
        {
            g.FillRectangle(bgClearBrush, ClientRectangle);
        }

        const int boxSize = 16;
        int boxY = (Height - boxSize) / 2;
        var boxRect = new Rectangle(0, boxY, boxSize, boxSize);

        // 1. 체크박스 박스 배경 및 테두리 결정
        Color boxBg;
        Color boxBorder;

        if (Checked)
        {
            boxBg = _isDark ? AppThemeHelper.Palette.Dark.AccentBlue : AppThemeHelper.Palette.Light.AccentBlue;
            boxBorder = _isDark ? Color.FromArgb(60, 150, 215) : Color.FromArgb(41, 128, 185);
        }
        else
        {
            boxBg = _isDark ? AppThemeHelper.Palette.Dark.InputBackground : AppThemeHelper.Palette.Light.InputBackground;
            boxBorder = _isHovered
                ? (_isDark ? Color.FromArgb(70, 130, 190) : Color.FromArgb(41, 128, 185))
                : (_isDark ? AppThemeHelper.Palette.Dark.Border : AppThemeHelper.Palette.Light.Border);
        }

        using (var path = CreateRoundedRectanglePath(boxRect, 3))
        {
            using (var brush = new SolidBrush(boxBg))
            {
                g.FillPath(brush, path);
            }
            using (var pen = new Pen(boxBorder, 1.2f))
            {
                g.DrawPath(pen, path);
            }
        }

        // 2. 체크 마크 (✔) 벡터 드로잉
        if (Checked)
        {
            using var checkPen = new Pen(Color.White, 2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            var p1 = new PointF(boxRect.X + 3.5f, boxRect.Y + 8f);
            var p2 = new PointF(boxRect.X + 6.5f, boxRect.Y + 11.5f);
            var p3 = new PointF(boxRect.X + 12.5f, boxRect.Y + 4.5f);

            g.DrawLines(checkPen, new[] { p1, p2, p3 });
        }

        // 3. 텍스트 렌더링
        Color textColor = Enabled
            ? (_isDark ? AppThemeHelper.Palette.Dark.TextPrimary : AppThemeHelper.Palette.Light.TextPrimary)
            : (_isDark ? AppThemeHelper.Palette.Dark.TextMuted : AppThemeHelper.Palette.Light.TextMuted);

        var textRect = new RectangleF(boxSize + 8, 0, Math.Max(0, Width - (boxSize + 8)), Height);
        using var textBrush = new SolidBrush(textColor);
        AppFontHelper.DrawMixedString(g, Text, Font?.Size ?? 9.5f, textBrush, textRect, StringAlignment.Near, StringAlignment.Center, Font?.Style ?? FontStyle.Regular);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int r = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2);
        if (r <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
        path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
        path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// 라이트 모드와 다크 모드를 완벽히 지원하는 커스텀 드롭다운 콤보박스입니다.
/// OS 기본 흰색 화살표 버튼을 세련된 벡터 드롭다운 그래픽으로 대체하고 모든 아이템을 테마 팔레트에 맞춰 렌더링합니다.
/// </summary>
public class CustomComboBox : ComboBox
{
    private bool _isHovered = false;
    private bool _isDark = AppThemeHelper.CurrentThemeIsDark;

    public bool IsDark
    {
        get => _isDark;
        set
        {
            if (_isDark != value)
            {
                _isDark = value;
                BackColor = _isDark ? AppThemeHelper.Palette.Dark.InputBackground : AppThemeHelper.Palette.Light.InputBackground;
                ForeColor = _isDark ? AppThemeHelper.Palette.Dark.TextPrimary : AppThemeHelper.Palette.Light.TextPrimary;
                Invalidate();
            }
        }
    }

    public CustomComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        ItemHeight = 22;
        Font = AppFontHelper.GetDefaultFont(9.5f);
        BackColor = _isDark ? AppThemeHelper.Palette.Dark.InputBackground : AppThemeHelper.Palette.Light.InputBackground;
        ForeColor = _isDark ? AppThemeHelper.Palette.Dark.TextPrimary : AppThemeHelper.Palette.Light.TextPrimary;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;

        bool isSelected = (e.State & DrawItemState.Selected) != 0;
        Color bg = isSelected
            ? (_isDark ? AppThemeHelper.Palette.Dark.SelectionBg : AppThemeHelper.Palette.Light.SelectionBg)
            : (_isDark ? AppThemeHelper.Palette.Dark.InputBackground : AppThemeHelper.Palette.Light.InputBackground);

        Color fg = isSelected
            ? (_isDark ? AppThemeHelper.Palette.Dark.SelectionFg : AppThemeHelper.Palette.Light.SelectionFg)
            : (_isDark ? AppThemeHelper.Palette.Dark.TextPrimary : AppThemeHelper.Palette.Light.TextPrimary);

        using (var bgBrush = new SolidBrush(bg))
        {
            e.Graphics.FillRectangle(bgBrush, e.Bounds);
        }

        using (var fgBrush = new SolidBrush(fg))
        {
            string txt = Items[e.Index]?.ToString() ?? "";
            var textRect = new RectangleF(e.Bounds.X + 6, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 28), e.Bounds.Height);
            AppFontHelper.DrawMixedString(e.Graphics, txt, Font?.Size ?? 9.5f, fgBrush, textRect, StringAlignment.Near, StringAlignment.Center, Font?.Style ?? FontStyle.Regular);
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        const int WM_PAINT = 0x000F;
        if (m.Msg == WM_PAINT)
        {
            using var g = Graphics.FromHwnd(Handle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. 외곽 테두리 렌더링
            Color borderColor = Focused || _isHovered
                ? (_isDark ? AppThemeHelper.Palette.Dark.AccentBlue : AppThemeHelper.Palette.Light.AccentBlue)
                : (_isDark ? AppThemeHelper.Palette.Dark.Border : AppThemeHelper.Palette.Light.Border);

            using (var borderPen = new Pen(borderColor, 1.2f))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }

            // 2. 우측 드롭다운 버튼 영역 (22px 폭) 벡터 렌더링
            int btnW = 22;
            var btnRect = new Rectangle(Width - btnW, 1, btnW - 1, Height - 2);

            Color btnBg = _isHovered
                ? (_isDark ? AppThemeHelper.Palette.Dark.SurfaceElevated : Color.FromArgb(232, 236, 242))
                : (_isDark ? AppThemeHelper.Palette.Dark.Surface : AppThemeHelper.Palette.Light.Surface);

            using (var btnBrush = new SolidBrush(btnBg))
            {
                g.FillRectangle(btnBrush, btnRect);
            }

            // 구분선
            using (var sepPen = new Pen(borderColor, 1f))
            {
                g.DrawLine(sepPen, btnRect.Left, btnRect.Top, btnRect.Left, btnRect.Bottom);
            }

            // 역삼각형 화살표 (▼) 벡터
            Color arrowColor = _isDark ? AppThemeHelper.Palette.Dark.TextSecondary : AppThemeHelper.Palette.Light.TextSecondary;
            float cx = btnRect.X + btnRect.Width / 2f;
            float cy = btnRect.Y + btnRect.Height / 2f;

            var p1 = new PointF(cx - 4.5f, cy - 2f);
            var p2 = new PointF(cx + 4.5f, cy - 2f);
            var p3 = new PointF(cx, cy + 3f);

            using var arrowBrush = new SolidBrush(arrowColor);
            g.FillPolygon(arrowBrush, new[] { p1, p2, p3 });
        }
    }
}

/// <summary>
/// 라이트 모드와 다크 모드를 완벽히 지원하는 커스텀 벡터 라디오 버튼입니다.
/// </summary>
public class CustomRadioButton : RadioButton
{
    private bool _isHovered = false;
    private bool _isDark = AppThemeHelper.CurrentThemeIsDark;

    public bool IsDark
    {
        get => _isDark;
        set
        {
            if (_isDark != value)
            {
                _isDark = value;
                Invalidate();
            }
        }
    }

    public CustomRadioButton()
    {
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = AppFontHelper.GetDefaultFont(9.5f);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    private Color GetEffectiveBackColor()
    {
        if (BackColor != Color.Transparent && BackColor.A > 0)
        {
            return BackColor;
        }
        Control? current = Parent;
        while (current != null)
        {
            if (current.BackColor != Color.Transparent && current.BackColor.A > 0)
            {
                return current.BackColor;
            }
            current = current.Parent;
        }
        return _isDark ? AppThemeHelper.Palette.Dark.Background : AppThemeHelper.Palette.Light.Background;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return new Size(24, 24);
        }

        using var bmp = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bmp);
        using var sfTypo = new StringFormat(StringFormat.GenericTypographic);
        var (iconPart, textPart) = AppFontHelper.SplitEmojiAndText(Text);

        float iconWidth = 0;
        if (!string.IsNullOrEmpty(iconPart))
        {
            using var iconFont = AppFontHelper.GetEmojiFont(Font?.Size ?? 9.5f, FontStyle.Regular);
            var iconSz = g.MeasureString(iconPart, iconFont, PointF.Empty, sfTypo);
            iconWidth = Math.Max((Font?.Size ?? 9.5f) + 2, iconSz.Width);
        }

        float textWidth = 0;
        if (!string.IsNullOrEmpty(textPart))
        {
            using var textFont = AppFontHelper.GetDefaultFont(Font?.Size ?? 9.5f, Font?.Style ?? FontStyle.Regular);
            textWidth = g.MeasureString(textPart, textFont, PointF.Empty, sfTypo).Width;
        }

        float gap = (!string.IsNullOrEmpty(iconPart) && !string.IsNullOrEmpty(textPart)) ? 5f : 0f;
        // 16px 원형 + 8px 간격 + 아이콘 폭 + 5px 간격 + 텍스트 폭 + 14px 안전 버퍼
        int totalWidth = (int)Math.Ceiling((double)(16 + 8 + iconWidth + gap + textWidth + 14));
        int totalHeight = Math.Max(24, (int)Math.Ceiling((double)((Font?.Height ?? 18) + 4)));

        return new Size(totalWidth, totalHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 부모 배경색으로 클라이언트 영역을 완전히 지워 이전 텍스트 잔상 및 폰트 겹침 원천 방지
        Color effectiveBg = GetEffectiveBackColor();
        using (var bgClearBrush = new SolidBrush(effectiveBg))
        {
            g.FillRectangle(bgClearBrush, ClientRectangle);
        }

        const int circleSize = 16;
        int circleY = (Height - circleSize) / 2;
        var circleRect = new Rectangle(0, circleY, circleSize, circleSize);

        // 1. 외곽 원형 테두리 및 배경
        Color bg = _isDark ? AppThemeHelper.Palette.Dark.InputBackground : AppThemeHelper.Palette.Light.InputBackground;
        Color border = Checked
            ? (_isDark ? AppThemeHelper.Palette.Dark.AccentBlue : AppThemeHelper.Palette.Light.AccentBlue)
            : (_isHovered
                ? (_isDark ? Color.FromArgb(70, 130, 190) : Color.FromArgb(41, 128, 185))
                : (_isDark ? AppThemeHelper.Palette.Dark.Border : AppThemeHelper.Palette.Light.Border));

        using (var bgBrush = new SolidBrush(bg))
        {
            g.FillEllipse(bgBrush, circleRect);
        }
        using (var borderPen = new Pen(border, 1.4f))
        {
            g.DrawEllipse(borderPen, circleRect);
        }

        // 2. 선택 시 중앙 채움 도트
        if (Checked)
        {
            Color dotColor = _isDark ? AppThemeHelper.Palette.Dark.AccentBlue : AppThemeHelper.Palette.Light.AccentBlue;
            const int dotSize = 8;
            int dotY = (Height - dotSize) / 2;
            var dotRect = new Rectangle(circleRect.X + 4, dotY, dotSize, dotSize);
            using var dotBrush = new SolidBrush(dotColor);
            g.FillEllipse(dotBrush, dotRect);
        }

        // 3. 텍스트
        Color textColor = Enabled
            ? (_isDark ? AppThemeHelper.Palette.Dark.TextPrimary : AppThemeHelper.Palette.Light.TextPrimary)
            : (_isDark ? AppThemeHelper.Palette.Dark.TextMuted : AppThemeHelper.Palette.Light.TextMuted);

        var textRect = new RectangleF(circleSize + 8, 0, Math.Max(0, Width - (circleSize + 8)), Height);
        using var textBrush = new SolidBrush(textColor);
        AppFontHelper.DrawMixedString(g, Text, Font?.Size ?? 9.5f, textBrush, textRect, StringAlignment.Near, StringAlignment.Center, Font?.Style ?? FontStyle.Regular);
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using RpgMacroHarness.Core;
using RpgMacroHarness.Data;
using RpgMacroHarness.Input;
using RpgMacroHarness.Vision;

namespace RpgMacroHarness.UI;

public partial class MainForm
{
    private void BuildSlimSideBar()
    {
        _slimSideBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(32, 35, 44),
            Padding = new Padding(4, 6, 4, 6),
            Visible = true
        };

        _btnSlimExpand = CreateSlimIconButton("◀", Localization.Get("BtnUnfoldInspector"), Color.FromArgb(41, 128, 185));
        _btnSlimExpand.Top = 8;
        _btnSlimExpand.Left = 4;
        _btnSlimExpand.Width = 36;
        _btnSlimExpand.Height = 36;
        _btnSlimExpand.Click += (s, e) => SetInspectorCollapsed(false);

        _slimSideBar.Controls.Add(_btnSlimExpand);
    }

    private Button CreateSlimIconButton(string text, string tooltip, Color bg)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = bg,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetEmojiFont(10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        _slimToolTip.SetToolTip(btn, tooltip);
        return btn;
    }

    private void BuildInspectorPanel()
    {
        _pnlInspector = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = false,
            Padding = Padding.Empty,
            BackColor = Color.FromArgb(248, 249, 251),
            Visible = false
        };
        _pnlInspector.Resize += (s, e) => ResizeInspectorContents();
        _pnlInspector.MouseWheel += (s, e) => BubbleScrollToInspector(e);

        _pnlInspectorContent = new Panel
        {
            Location = new Point(0, 0),
            Width = _pnlInspector.ClientSize.Width,
            Height = 670,
            BackColor = Color.Transparent,
            Padding = new Padding(10, 6, 10, 8)
        };
        _pnlInspectorContent.MouseWheel += (s, e) => BubbleScrollToInspector(e);

        // 상단 헤더 바 (인스펙터 제목 및 [▶ 접기] 버튼)
        _inspectorHeaderBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 2, 2, 2)
        };

        _btnCollapseInspector = CreateSlimIconButton("▶", Localization.Get("BtnFoldInspector"), Color.FromArgb(41, 128, 185));
        _btnCollapseInspector.Size = new Size(30, 26);
        _btnCollapseInspector.Dock = DockStyle.Right;
        _btnCollapseInspector.Click += (s, e) => SetInspectorCollapsed(true);

        _lblInspectorTitle = new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(44, 62, 80)
        };
        _lblInspectorTitle.Paint += (s, e) =>
        {
            using var brush = new SolidBrush(_lblInspectorTitle.ForeColor);
            string txt = Localization.Get("GrpInspector");
            AppFontHelper.DrawMixedString(e.Graphics, txt, 10.5f, brush, new RectangleF(0, 0, _lblInspectorTitle.Width, _lblInspectorTitle.Height), StringAlignment.Near, StringAlignment.Center, FontStyle.Bold);
        };

        _inspectorHeaderBar.Controls.Add(_lblInspectorTitle);
        _inspectorHeaderBar.Controls.Add(_btnCollapseInspector);
        _btnCollapseInspector.BringToFront();

        // 1. 상태 노드 속성 그룹 (TableLayoutPanel 기반 슬림 압축 레이아웃)
        _grpStateProps = new GroupBox
        {
            Text = Localization.Get("GrpStateProps"),
            Dock = DockStyle.Top,
            Height = 66,
            Padding = new Padding(8, 0, 8, 8)
        };

        var stateTbl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        stateTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        stateTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34f));
        stateTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34f));
        stateTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _txtStateName = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = AppFontHelper.GetDefaultFont(9.5f),
            Margin = new Padding(0, 4, 4, 4)
        };
        _txtStateName.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitStateRenameFromTextBox();
                e.SuppressKeyPress = true;
            }
        };
        _txtStateName.Leave += (s, e) => CommitStateRenameFromTextBox();

        _btnSetInitial = new Button
        {
            Text = "★",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetDefaultFont(11f, FontStyle.Bold),
            Margin = new Padding(2, 3, 2, 3)
        };
        _btnSetInitial.Click += (s, e) => SetSelectedAsInitialState();
        _slimToolTip.SetToolTip(_btnSetInitial, Localization.Get("BtnSetInitial"));

        _btnDeleteState = new Button
        {
            Text = "🗑",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetEmojiFont(9.5f),
            Margin = new Padding(2, 3, 0, 3)
        };
        _btnDeleteState.Click += (s, e) => DeleteSelectedState();
        _slimToolTip.SetToolTip(_btnDeleteState, Localization.Get("BtnDeleteState"));

        stateTbl.Controls.Add(_txtStateName, 0, 0);
        stateTbl.Controls.Add(_btnSetInitial, 1, 0);
        stateTbl.Controls.Add(_btnDeleteState, 2, 0);

        _grpStateProps.Controls.Add(stateTbl);

        // 2. 이벤트 속성 그룹 (다중 조건 + 행동) - 세로 스크롤 보장을 위해 DockStyle.Top 및 고정 높이 555px 지정
        _grpEventProps = new GroupBox
        {
            Text = Localization.Get("GrpEventProps"),
            Dock = DockStyle.Top,
            Height = 555,
            Padding = new Padding(10)
        };

        _lblEvtName = new Label { Text = Localization.Get("LblEventName"), Top = 25, Left = 12, AutoSize = true };
        _txtEventName = new TextBox { Top = 22, Left = 105, Width = 280 };
        _txtEventName.TextChanged += (s, e) =>
        {
            if (_isUpdatingUi || CurrentSelectedEvent == null) return;
            CurrentSelectedEvent.Name = _txtEventName.Text;
            _isDirty = true;
            _canvas.Invalidate();
            _classicControl.RefreshSelectedEventItem();
        };

        _chkEventEnabled = new CustomCheckBox
        {
            Text = Localization.IsKorean ? "이벤트 활성화" : "Event Enabled",
            Top = 54,
            Left = 14,
            AutoSize = true,
            Checked = true,
            Font = AppFontHelper.GetDefaultFont(9f, FontStyle.Bold)
        };
        _chkEventEnabled.CheckedChanged += (s, e) =>
        {
            if (_isUpdatingUi || CurrentSelectedEvent == null) return;
            RecordUndo(Localization.IsKorean ? $"이벤트 '{CurrentSelectedEvent.Name}' 활성화 토글" : $"Toggle '{CurrentSelectedEvent.Name}' Enabled");
            CurrentSelectedEvent.IsEnabled = _chkEventEnabled.Checked;
            _isDirty = true;
            _canvas.Invalidate();
            _classicControl.RefreshSelectedEventItem();
        };

        _chkParallel = new CustomCheckBox
        {
            Text = Localization.Get("ChkParallel"),
            Top = 54,
            Left = 155,
            AutoSize = true,
            Font = AppFontHelper.GetDefaultFont(9f)
        };
        _chkParallel.CheckedChanged += (s, e) =>
        {
            if (_isUpdatingUi || CurrentSelectedEvent == null) return;
            RecordUndo(Localization.IsKorean ? $"이벤트 '{CurrentSelectedEvent.Name}' 병렬 여부 변경" : $"Toggle '{CurrentSelectedEvent.Name}' Parallel");
            CurrentSelectedEvent.IsParallel = _chkParallel.Checked;
            _isDirty = true;
            _canvas.Invalidate();
            _classicControl.RefreshSelectedEventItem();
        };

        _lblHumanizerDelay = new Label { Text = string.Format(Localization.Get("LblHumanizerDelay"), 27), Top = 80, Left = 12, AutoSize = true };
        _sliderHumanizerDelay = new TrackBar { Top = 104, Left = 12, Width = 385, Height = 28, Minimum = 0, Maximum = 54, Value = 27, TickStyle = TickStyle.None };
        _sliderHumanizerDelay.Scroll += (s, e) =>
        {
            if (_isUpdatingUi) return;
            int val = _sliderHumanizerDelay.Value;
            _lblHumanizerDelay.Text = string.Format(Localization.Get("LblHumanizerDelay"), val);
            InputHumanizer.JitterMs = val;
            if (CurrentSelectedEvent != null)
            {
                CurrentSelectedEvent.HumanizerJitterMs = val;
                CurrentSelectedEvent.HumanizerJitterPercent = (int)Math.Round((val / 27.0) * 100);
                _isDirty = true;
                _canvas.Invalidate();
            }
        };

        // 다중 조건 섹션 (DarkListView 기반 완벽 다크 렌더링)
        _lblTriggerHeader = new Label
        {
            Text = "",
            Top = 158,
            Left = 12,
            Width = 385,
            Height = 22,
            ForeColor = Color.FromArgb(41, 128, 185)
        };
        _lblTriggerHeader.Paint += (s, e) =>
        {
            using var brush = new SolidBrush(_lblTriggerHeader.ForeColor);
            string txt = Localization.Get("GrpTrigger");
            AppFontHelper.DrawMixedString(e.Graphics, txt, 9.5f, brush, new RectangleF(0, 0, _lblTriggerHeader.Width, _lblTriggerHeader.Height), StringAlignment.Near, StringAlignment.Center, FontStyle.Bold);
        };
        _lvTriggers = new DarkListView
        {
            Top = 180,
            Left = 12,
            Width = 385,
            Height = 100,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            OwnerDraw = true,
            BorderStyle = BorderStyle.FixedSingle,
            Font = AppFontHelper.GetDefaultFont(9f)
        };
        _lvTriggers.MouseWheel += (s, e) => BubbleScrollToInspector(e);
        _lvTriggers.DrawColumnHeader += (s, e) =>
        {
            bool isDark = AppSettings.Current.IsDarkMode;
            using var bgBrush = new SolidBrush(isDark ? Color.FromArgb(24, 32, 46) : SystemColors.Control);
            using var borderPen = new Pen(isDark ? Color.FromArgb(44, 58, 80) : SystemColors.ControlDark);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);
            e.Graphics.DrawRectangle(borderPen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);

            if (e.ColumnIndex == _lvTriggers.Columns.Count - 1 && e.Bounds.Right < _lvTriggers.ClientSize.Width)
            {
                var extraHeaderRect = new Rectangle(e.Bounds.Right, e.Bounds.Y, _lvTriggers.ClientSize.Width - e.Bounds.Right, e.Bounds.Height);
                e.Graphics.FillRectangle(bgBrush, extraHeaderRect);
            }

            using var textBrush = new SolidBrush(isDark ? Color.White : Color.Black);
            using var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near };
            var textRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            e.Graphics.DrawString(e.Header?.Text ?? "", e.Font ?? _lvTriggers.Font, textBrush, textRect, sf);
        };
        _lvTriggers.DrawItem += (s, e) => { };
        _lvTriggers.DrawSubItem += (s, e) =>
        {
            if (e.Item == null) return;
            bool isDark = AppSettings.Current.IsDarkMode;
            bool isSelected = e.Item.Selected;
            Color bgColor = isSelected
                ? (isDark ? Color.FromArgb(41, 65, 95) : Color.FromArgb(215, 230, 248))
                : (isDark ? Color.FromArgb(13, 17, 24) : Color.White);

            using (var bgBrush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
                if (e.ColumnIndex == _lvTriggers.Columns.Count - 1 && e.Bounds.Right < _lvTriggers.ClientSize.Width)
                {
                    var extraRect = new Rectangle(e.Bounds.Right, e.Bounds.Y, _lvTriggers.ClientSize.Width - e.Bounds.Right, e.Bounds.Height);
                    e.Graphics.FillRectangle(bgBrush, extraRect);
                }
            }

            Color textColor = isSelected
                ? (isDark ? Color.White : Color.FromArgb(20, 40, 70))
                : (isDark ? Color.FromArgb(230, 235, 245) : Color.Black);

            using var textBrush = new SolidBrush(textColor);
            using var sf = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Alignment = (e.ColumnIndex == 0) ? StringAlignment.Center : StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter
            };
            var textRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            if (e.ColumnIndex == 2)
            {
                AppFontHelper.DrawMixedString(e.Graphics, e.SubItem?.Text ?? "", 9f, textBrush, textRect, StringAlignment.Near, StringAlignment.Center, FontStyle.Regular);
            }
            else
            {
                e.Graphics.DrawString(e.SubItem?.Text ?? "", _lvTriggers.Font, textBrush, textRect, sf);
            }
        };
        _lvTriggers.Columns.Add(Localization.Get("ColTriggerIndex"), 45);
        _lvTriggers.Columns.Add(Localization.Get("ColTriggerType"), 85);
        _lvTriggers.Columns.Add(Localization.Get("ColTriggerSummary"), 255);
        _lvTriggers.DoubleClick += (s, e) => EditSelectedTrigger();
        _lvTriggers.Resize += (s, e) => FitInspectorListColumns();

        _btnAddTrigger = new Button { Text = Localization.Get("BtnAddTrigger"), Top = 286, Left = 12, Width = 125, Height = 28, FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetDefaultFont(9f) };
        _btnAddTrigger.Click += (s, e) => AddTrigger();

        _btnEditTrigger = new Button { Text = Localization.Get("BtnEditTrigger"), Top = 286, Left = 143, Width = 125, Height = 28, FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetDefaultFont(9f) };
        _btnEditTrigger.Click += (s, e) => EditSelectedTrigger();

        _btnDeleteTrigger = new Button { Text = Localization.Get("BtnDeleteTrigger"), Top = 286, Left = 274, Width = 85, Height = 28, FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetDefaultFont(9f) };
        _btnDeleteTrigger.Click += (s, e) => DeleteSelectedTrigger();

        // 행동 목록 섹션 (DarkListView 기반 완벽 다크 렌더링)
        _lblActionHeader = new Label
        {
            Text = "",
            Top = 324,
            Left = 12,
            Width = 385,
            Height = 22,
            ForeColor = Color.FromArgb(211, 84, 0)
        };
        _lblActionHeader.Paint += (s, e) =>
        {
            using var brush = new SolidBrush(_lblActionHeader.ForeColor);
            string txt = Localization.Get("GrpActions");
            AppFontHelper.DrawMixedString(e.Graphics, txt, 9.5f, brush, new RectangleF(0, 0, _lblActionHeader.Width, _lblActionHeader.Height), StringAlignment.Near, StringAlignment.Center, FontStyle.Bold);
        };
        _lvActions = new DarkListView
        {
            Top = 346,
            Left = 12,
            Width = 385,
            Height = 110,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            OwnerDraw = true,
            BorderStyle = BorderStyle.FixedSingle,
            Font = AppFontHelper.GetDefaultFont(9f)
        };
        _lvActions.MouseWheel += (s, e) => BubbleScrollToInspector(e);
        _lvActions.DrawColumnHeader += (s, e) =>
        {
            bool isDark = AppSettings.Current.IsDarkMode;
            using var bgBrush = new SolidBrush(isDark ? Color.FromArgb(24, 32, 46) : SystemColors.Control);
            using var borderPen = new Pen(isDark ? Color.FromArgb(44, 58, 80) : SystemColors.ControlDark);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);
            e.Graphics.DrawRectangle(borderPen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);

            if (e.ColumnIndex == _lvActions.Columns.Count - 1 && e.Bounds.Right < _lvActions.ClientSize.Width)
            {
                var extraHeaderRect = new Rectangle(e.Bounds.Right, e.Bounds.Y, _lvActions.ClientSize.Width - e.Bounds.Right, e.Bounds.Height);
                e.Graphics.FillRectangle(bgBrush, extraHeaderRect);
            }

            using var textBrush = new SolidBrush(isDark ? Color.White : Color.Black);
            using var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near };
            var textRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            e.Graphics.DrawString(e.Header?.Text ?? "", e.Font ?? _lvActions.Font, textBrush, textRect, sf);
        };
        _lvActions.DrawItem += (s, e) => { };
        _lvActions.DrawSubItem += (s, e) =>
        {
            if (e.Item == null) return;
            bool isDark = AppSettings.Current.IsDarkMode;
            bool isSelected = e.Item.Selected;
            Color bgColor = isSelected
                ? (isDark ? Color.FromArgb(41, 65, 95) : Color.FromArgb(215, 230, 248))
                : (isDark ? Color.FromArgb(13, 17, 24) : Color.White);

            using (var bgBrush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
                if (e.ColumnIndex == _lvActions.Columns.Count - 1 && e.Bounds.Right < _lvActions.ClientSize.Width)
                {
                    var extraRect = new Rectangle(e.Bounds.Right, e.Bounds.Y, _lvActions.ClientSize.Width - e.Bounds.Right, e.Bounds.Height);
                    e.Graphics.FillRectangle(bgBrush, extraRect);
                }
            }

            Color textColor = isSelected
                ? (isDark ? Color.White : Color.FromArgb(20, 40, 70))
                : (isDark ? Color.FromArgb(230, 235, 245) : Color.Black);

            using var textBrush = new SolidBrush(textColor);
            using var sf = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Alignment = (e.ColumnIndex == 0) ? StringAlignment.Center : StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter
            };
            var textRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            if (e.ColumnIndex == 2)
            {
                AppFontHelper.DrawMixedString(e.Graphics, e.SubItem?.Text ?? "", 9f, textBrush, textRect, StringAlignment.Near, StringAlignment.Center, FontStyle.Regular);
            }
            else
            {
                e.Graphics.DrawString(e.SubItem?.Text ?? "", _lvActions.Font, textBrush, textRect, sf);
            }
        };
        _lvActions.Columns.Add(Localization.Get("ColActionIndex"), 45);
        _lvActions.Columns.Add(Localization.Get("ColActionType"), 85);
        _lvActions.Columns.Add(Localization.Get("ColActionDetail"), 255);
        _lvActions.DoubleClick += (s, e) => EditSelectedAction();
        _lvActions.Resize += (s, e) => FitInspectorListColumns();

        _btnAddAction = new Button { Text = Localization.Get("BtnAddAction"), Top = 462, Left = 12, Width = 110, Height = 28, FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetDefaultFont(9f) };
        _btnAddAction.Click += (s, e) => AddAction();

        _btnEditAction = new Button { Text = Localization.Get("BtnEditAction"), Top = 462, Left = 128, Width = 65, Height = 28, FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetDefaultFont(9f) };
        _btnEditAction.Click += (s, e) => EditSelectedAction();

        _btnDeleteAction = new Button { Text = Localization.Get("BtnDeleteAction"), Top = 462, Left = 198, Width = 65, Height = 28, FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetDefaultFont(9f) };
        _btnDeleteAction.Click += (s, e) => DeleteSelectedAction();

        _btnMoveUpAction = new Button { Text = "▲", Top = 462, Left = 268, Width = 40, Height = 28, FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetEmojiFont(8f) };
        _btnMoveUpAction.Click += (s, e) => MoveAction(-1);

        _btnMoveDownAction = new Button { Text = "▼", Top = 462, Left = 312, Width = 40, Height = 28, FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetEmojiFont(8f) };
        _btnMoveDownAction.Click += (s, e) => MoveAction(1);

        // 녹화 버튼은 텍스트가 잘리지 않도록 아래 줄에 넉넉한 너비로 배치
        _btnRecordMacro = new Button
        {
            Text = $"{Localization.Get("BtnRecord")} [{AppSettings.Current.RecordHotkey}]",
            Top = 498,
            Left = 12,
            Width = 385,
            Height = 34,
            BackColor = Color.FromArgb(192, 57, 43),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetEmojiFont(9.5f, FontStyle.Bold)
        };
        _btnRecordMacro.Click += (s, e) => ToggleMacroRecording();

        _grpEventProps.Controls.AddRange(new Control[]
        {
            _lblEvtName, _txtEventName, _chkEventEnabled, _chkParallel, _lblHumanizerDelay, _sliderHumanizerDelay,
            _lblTriggerHeader, _lvTriggers, _btnAddTrigger, _btnEditTrigger, _btnDeleteTrigger,
            _lblActionHeader, _lvActions, _btnAddAction, _btnEditAction, _btnDeleteAction, _btnMoveUpAction, _btnMoveDownAction, _btnRecordMacro
        });

        var inspectorSpacer = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent };
        _pnlInspectorContent.Controls.Add(_grpEventProps);
        _pnlInspectorContent.Controls.Add(inspectorSpacer);
        _pnlInspectorContent.Controls.Add(_grpStateProps);
        _pnlInspectorContent.Controls.Add(_inspectorHeaderBar);
        _pnlInspector.Controls.Add(_pnlInspectorContent);
    }

    private void BubbleScrollToInspector(MouseEventArgs e)
    {
        if (_pnlInspector == null || !_pnlInspector.Visible || _scrollInspector == null) return;
        if (e is HandledMouseEventArgs hme)
        {
            hme.Handled = true;
        }
        int scrollAmount = (e.Delta / 120) * 48;
        _scrollInspector.Value -= scrollAmount;
        if (_pnlInspectorContent != null)
        {
            _pnlInspectorContent.Top = -_scrollInspector.Value;
        }
    }

    /// <summary>
    /// 오른쪽 UI(인스펙터)를 접거나 펼칩니다.
    /// </summary>
    private void ResizeInspectorContents()
    {
        if (_grpEventProps == null || _pnlInspector == null || _pnlInspectorContent == null) return;
        _pnlInspectorContent.Width = _pnlInspector.ClientSize.Width;
        int totalContentH = _grpEventProps.Bottom + 16;
        _pnlInspectorContent.Height = Math.Max(_pnlInspector.ClientSize.Height, totalContentH);

        int maxScroll = Math.Max(0, _pnlInspectorContent.Height - _pnlInspector.ClientSize.Height);
        if (_scrollInspector != null)
        {
            _scrollInspector.Maximum = maxScroll;
            _scrollInspector.LargeChange = Math.Max(1, _pnlInspector.ClientSize.Height);
            _scrollInspector.Visible = maxScroll > 0;
            _pnlInspectorContent.Top = -Math.Min(_scrollInspector.Value, maxScroll);
        }

        int contentWidth = Math.Max(180, _pnlInspector.ClientSize.Width - 36);

        if (_txtEventName != null)
        {
            _txtEventName.Width = Math.Max(120, contentWidth - _txtEventName.Left + 12);
        }
        if (_sliderHumanizerDelay != null)
        {
            _sliderHumanizerDelay.Width = contentWidth;
        }
        if (_lvTriggers != null)
        {
            _lvTriggers.Width = contentWidth;
        }

        // 인스펙터 패널 너비에 정밀하게 적응하는 버튼 축소 (조건 3개: 290px 미만 / 행동 5개: 345px 미만)
        bool isTriggerCompact = contentWidth < 290;
        bool isActionCompact = contentWidth < 345;

        // 조건 버튼 3개 (추가, 수정, 삭제) 가중치 비대칭 분배 및 축소 시 아이콘화 (상단 UI 방식)
        if (_btnAddTrigger != null && _btnEditTrigger != null && _btnDeleteTrigger != null)
        {
            if (isTriggerCompact)
            {
                _btnAddTrigger.Text = "➕";
                _btnEditTrigger.Text = "✏️";
                _btnDeleteTrigger.Text = "🗑️";
                _slimToolTip.SetToolTip(_btnAddTrigger, Localization.Get("BtnAddTrigger"));
                _slimToolTip.SetToolTip(_btnEditTrigger, Localization.Get("BtnEditTrigger"));
                _slimToolTip.SetToolTip(_btnDeleteTrigger, Localization.Get("BtnDeleteTrigger"));

                // 컴팩트 모드: 상단 UI처럼 32px 슬림 정사각형 아이콘 크기로 버튼 크기 실제 축소
                int compactBtnW = 32;
                int compactGap = 5;

                _btnAddTrigger.Left = 12;
                _btnAddTrigger.Width = compactBtnW;

                _btnEditTrigger.Left = _btnAddTrigger.Right + compactGap;
                _btnEditTrigger.Width = compactBtnW;

                _btnDeleteTrigger.Left = _btnEditTrigger.Right + compactGap;
                _btnDeleteTrigger.Width = compactBtnW;
            }
            else
            {
                _btnAddTrigger.Text = Localization.Get("BtnAddTrigger");
                _btnEditTrigger.Text = Localization.Get("BtnEditTrigger");
                _btnDeleteTrigger.Text = Localization.Get("BtnDeleteTrigger");
                _slimToolTip.SetToolTip(_btnAddTrigger, "");
                _slimToolTip.SetToolTip(_btnEditTrigger, "");
                _slimToolTip.SetToolTip(_btnDeleteTrigger, "");

                int gap = 5;
                int totalW = contentWidth - gap * 2;
                int addW = Math.Max(90, (int)(totalW * 0.44f));
                int editW = Math.Max(70, (int)(totalW * 0.33f));
                int deleteW = Math.Max(30, totalW - addW - editW);

                _btnAddTrigger.Left = 12;
                _btnAddTrigger.Width = addW;

                _btnEditTrigger.Left = _btnAddTrigger.Right + gap;
                _btnEditTrigger.Width = editW;

                _btnDeleteTrigger.Left = _btnEditTrigger.Right + gap;
                _btnDeleteTrigger.Width = deleteW;
            }
        }

        if (_lvActions != null)
        {
            _lvActions.Width = contentWidth;
        }

        // 행동 버튼 5개 (추가, 수정, 삭제, 위로, 아래로) 텍스트 길이 최적 분배 및 축소 시 아이콘화
        if (_btnAddAction != null && _btnEditAction != null && _btnDeleteAction != null && _btnMoveUpAction != null && _btnMoveDownAction != null)
        {
            if (isActionCompact)
            {
                _btnAddAction.Text = "➕";
                _btnEditAction.Text = "✏️";
                _btnDeleteAction.Text = "🗑️";
                _btnMoveUpAction.Text = "▲";
                _btnMoveDownAction.Text = "▼";
                _slimToolTip.SetToolTip(_btnAddAction, Localization.Get("BtnAddAction"));
                _slimToolTip.SetToolTip(_btnEditAction, Localization.Get("BtnEditAction"));
                _slimToolTip.SetToolTip(_btnDeleteAction, Localization.Get("BtnDeleteAction"));
                _slimToolTip.SetToolTip(_btnMoveUpAction, Localization.Get("BtnMoveUpAction"));
                _slimToolTip.SetToolTip(_btnMoveDownAction, Localization.Get("BtnMoveDownAction"));

                // 컴팩트 모드: 상단 UI처럼 32px 및 26px 슬림 아이콘 크기로 버튼 크기 실제 축소
                int compactBtnW = 32;
                int upDownW = 26;
                int compactGap = 4;

                _btnAddAction.Left = 12;
                _btnAddAction.Width = compactBtnW;

                _btnEditAction.Left = _btnAddAction.Right + compactGap;
                _btnEditAction.Width = compactBtnW;

                _btnDeleteAction.Left = _btnEditAction.Right + compactGap;
                _btnDeleteAction.Width = compactBtnW;

                _btnMoveUpAction.Left = _btnDeleteAction.Right + compactGap;
                _btnMoveUpAction.Width = upDownW;

                _btnMoveDownAction.Left = _btnMoveUpAction.Right + compactGap;
                _btnMoveDownAction.Width = upDownW;
            }
            else
            {
                _btnAddAction.Text = Localization.Get("BtnAddAction");
                _btnEditAction.Text = Localization.Get("BtnEditAction");
                _btnDeleteAction.Text = Localization.Get("BtnDeleteAction");
                _btnMoveUpAction.Text = "▲";
                _btnMoveDownAction.Text = "▼";
                _slimToolTip.SetToolTip(_btnAddAction, "");
                _slimToolTip.SetToolTip(_btnEditAction, "");
                _slimToolTip.SetToolTip(_btnDeleteAction, "");
                _slimToolTip.SetToolTip(_btnMoveUpAction, Localization.Get("BtnMoveUpAction"));
                _slimToolTip.SetToolTip(_btnMoveDownAction, Localization.Get("BtnMoveDownAction"));

                int upDownW = 26;
                int gap = 4;
                int remainingW = contentWidth - (upDownW * 2 + gap * 4);
                int addW = Math.Max(84, (int)(remainingW * 0.48f));
                int editW = Math.Max(46, (remainingW - addW) / 2);
                int deleteW = Math.Max(26, remainingW - addW - editW);

                _btnAddAction.Left = 12;
                _btnAddAction.Width = addW;

                _btnEditAction.Left = _btnAddAction.Right + gap;
                _btnEditAction.Width = editW;

                _btnDeleteAction.Left = _btnEditAction.Right + gap;
                _btnDeleteAction.Width = deleteW;

                _btnMoveUpAction.Left = _btnDeleteAction.Right + gap;
                _btnMoveUpAction.Width = upDownW;

                _btnMoveDownAction.Left = _btnMoveUpAction.Right + gap;
            }
        }

        if (_btnRecordMacro != null)
        {
            _btnRecordMacro.Width = contentWidth;
        }

        FitInspectorListColumns();
    }

    private void FitInspectorListColumns()
    {
        if (_lvTriggers != null && _lvTriggers.Columns.Count >= 3)
        {
            int clientW = _lvTriggers.ClientSize.Width;
            if (clientW > 80)
            {
                int col0 = 32;
                int col1 = Math.Max(60, clientW / 4);
                int col2 = Math.Max(40, clientW - col0 - col1 - 2);
                _lvTriggers.Columns[0].Width = col0;
                _lvTriggers.Columns[1].Width = col1;
                _lvTriggers.Columns[2].Width = col2;
            }
            if (_lvTriggers.IsHandleCreated)
            {
                try { AppThemeHelper.ShowScrollBar(_lvTriggers.Handle, AppThemeHelper.SB_HORZ, false); } catch { }
            }
        }
        if (_lvActions != null && _lvActions.Columns.Count >= 3)
        {
            int clientW = _lvActions.ClientSize.Width;
            if (clientW > 80)
            {
                int col0 = 32;
                int col1 = Math.Max(60, clientW / 4);
                int col2 = Math.Max(40, clientW - col0 - col1 - 2);
                _lvActions.Columns[0].Width = col0;
                _lvActions.Columns[1].Width = col1;
                _lvActions.Columns[2].Width = col2;
            }
            if (_lvActions.IsHandleCreated)
            {
                try { AppThemeHelper.ShowScrollBar(_lvActions.Handle, AppThemeHelper.SB_HORZ, false); } catch { }
            }
        }
    }

    private void SetInspectorCollapsed(bool collapsed)
    {
        _isInspectorCollapsed = collapsed;
        _splitMain.SuspendLayout();
        try
        {
            if (collapsed)
            {
                _pnlInspector.Visible = false;
                _slimSideBar.Visible = false;
                _splitMain.Panel2Collapsed = true;
            }
            else
            {
                _splitMain.Panel2Collapsed = false;
                _slimSideBar.Visible = false;
                _pnlInspector.Visible = true;
                _splitMain.SplitterDistance = Math.Max(200, _splitMain.Width - 460);
                ResizeInspectorContents();
            }

            if (_btnToggleInspector != null)
            {
                _btnToggleInspector.Text = collapsed ? "◀" : "▶";
                _toolbarToolTip.SetToolTip(_btnToggleInspector, collapsed
                    ? (Localization.IsKorean ? "인스펙터 속성창 열기" : "Open Inspector Panel")
                    : (Localization.IsKorean ? "인스펙터 속성창 닫기" : "Close Inspector Panel"));
            }
        }
        catch { }
        finally
        {
            _splitMain.ResumeLayout();
        }
    }

    private void SetLogCollapsed(bool collapsed)
    {
        _isLogCollapsed = collapsed;
        if (_logContainer != null) _logContainer.Visible = !collapsed;
        if (_scrollLog != null) _scrollLog.Visible = !collapsed;
        if (_isLogCollapsed)
        {
            _logBox.Visible = false;
            _bottomBar.Height = 28;
            _btnToggleLog.Text = "▲";
        }
        else
        {
            _logBox.Visible = true;
            _bottomBar.Height = 130;
            _btnToggleLog.Text = "▼";
            _scrollLog?.UpdateMetrics();
        }
    }

    private void ToggleLogPanel()
    {
        SetLogCollapsed(!_isLogCollapsed);
        AppSettings.Current.IsLogCollapsed = _isLogCollapsed;
        AppSettings.Save();
    }


    public void ApplyLanguage()
    {
        UpdateWindowTitle();

        _btnOpen.Text = "📂";
        _toolbarToolTip.SetToolTip(_btnOpen, Localization.Get("TooltipOpen"));
        _btnSave.Text = "💾";
        _toolbarToolTip.SetToolTip(_btnSave, Localization.Get("TooltipSave"));
        _btnUndo.Text = "↶";
        _toolbarToolTip.SetToolTip(_btnUndo, Localization.Get("TooltipUndo"));
        _btnRedo.Text = "↷";
        _toolbarToolTip.SetToolTip(_btnRedo, Localization.Get("TooltipRedo"));

        if (!_isMacroRunning)
        {
            _btnStart.Text = Localization.Get("BtnStart");
            _lblStatus.Text = Localization.Get("StatusWaiting");
        }
        else if (_harness != null && _harness.IsPaused)
        {
            _btnStart.Text = Localization.Get("BtnResume");
            _lblStatus.Text = Localization.Get("StatusPaused");
        }
        else
        {
            _btnStart.Text = Localization.Get("BtnPause");
            _lblStatus.Text = Localization.Get("StatusRunning");
        }

        _btnStop.Text = Localization.Get("BtnStop");
        _btnAddStateNode.Text = "➕";
        _toolbarToolTip.SetToolTip(_btnAddStateNode, Localization.IsKorean ? "새 상태 노드 추가" : "Add New State Node");
        _btnAddEventToNode.Text = "⚡";
        _toolbarToolTip.SetToolTip(_btnAddEventToNode, Localization.IsKorean ? "새 이벤트 추가" : "Add New Event");
        _btnAutoLayout.Text = "📐";
        _toolbarToolTip.SetToolTip(_btnAutoLayout, Localization.IsKorean ? "노드 자동 정렬" : "Auto Layout Nodes");
        _btnTemplateWizard.Text = "✨";
        _toolbarToolTip.SetToolTip(_btnTemplateWizard, Localization.IsKorean ? "도우미 마법사 (실전 템플릿 간이 생성)" : "Helper Wizard");
        _btnAiAssistant.Text = "🧠";
        _toolbarToolTip.SetToolTip(_btnAiAssistant, Localization.IsKorean ? "AI 매크로 어시스턴트 (자연어 자동 생성)" : "AI Macro Assistant");
        _btnSettings.Text = "⚙️";
        _toolbarToolTip.SetToolTip(_btnSettings, Localization.IsKorean ? "프로그램 환경설정 및 패치노트" : "Settings & Patch Notes");
        if (_btnToggleInspector != null)
        {
            _btnToggleInspector.Text = _isInspectorCollapsed ? "◀" : "▶";
            _toolbarToolTip.SetToolTip(_btnToggleInspector, _isInspectorCollapsed
                ? (Localization.IsKorean ? "인스펙터 속성창 열기" : "Open Inspector Panel")
                : (Localization.IsKorean ? "인스펙터 속성창 닫기" : "Close Inspector Panel"));
        }
        _lblEmergency.Text = Localization.Get("EmergencyStop");

        _btnTabFsm.Text = Localization.IsKorean ? "📦 FSM 모드" : "📦 FSM Mode";
        _btnTabPin.Text = Localization.IsKorean ? "🔀 핀 모드" : "🔀 Pin Mode";
        _btnTabClassic.Text = Localization.IsKorean ? "☰ 클래식 모드" : "☰ Classic Mode";
        UpdateViewTabButtons();
        _classicControl.ApplyLanguage();

        string curState = _harness?.CurrentState ?? _canvas.ActiveState ?? _profile.InitialState;
        _lblActiveState.Text = $"{Localization.Get("ActiveStatePrefix")} [{MacroEventProfile.FormatStateDisplay(curState, Localization.IsKorean)}]";
        _lblLogTitle.Text = "Log";

        // Inspector Controls
        _lblInspectorTitle.Text = "";
        _lblInspectorTitle.Invalidate();
        _btnCollapseInspector.Text = "▶";
        _slimToolTip.SetToolTip(_btnCollapseInspector, Localization.Get("BtnFoldInspector"));
        _grpStateProps.Text = Localization.Get("GrpStateProps");
        _slimToolTip.SetToolTip(_btnSetInitial, Localization.Get("BtnSetInitial"));
        _slimToolTip.SetToolTip(_btnDeleteState, Localization.Get("BtnDeleteState"));

        _grpEventProps.Text = Localization.Get("GrpEventProps");
        _lblEvtName.Text = Localization.Get("LblEventName");
        _chkEventEnabled.Text = Localization.IsKorean ? "이벤트 활성화" : "Event Enabled";
        _chkParallel.Text = Localization.Get("ChkParallel");

        _lblTriggerHeader.Text = "";
        _lblTriggerHeader.Invalidate();
        if (_lvTriggers.Columns.Count >= 3)
        {
            _lvTriggers.Columns[0].Text = Localization.Get("ColTriggerIndex");
            _lvTriggers.Columns[1].Text = Localization.Get("ColTriggerType");
            _lvTriggers.Columns[2].Text = Localization.Get("ColTriggerSummary");
        }
        _btnAddTrigger.Text = Localization.Get("BtnAddTrigger");
        _btnEditTrigger.Text = Localization.Get("BtnEditTrigger");
        _btnDeleteTrigger.Text = Localization.Get("BtnDeleteTrigger");

        _lblActionHeader.Text = "";
        _lblActionHeader.Invalidate();
        if (_lvActions.Columns.Count >= 3)
        {
            _lvActions.Columns[0].Text = Localization.Get("ColActionIndex");
            _lvActions.Columns[1].Text = Localization.Get("ColActionType");
            _lvActions.Columns[2].Text = Localization.Get("ColActionDetail");
        }
        _btnAddAction.Text = Localization.Get("BtnAddAction");
        _btnEditAction.Text = Localization.Get("BtnEditAction");
        _btnDeleteAction.Text = Localization.Get("BtnDeleteAction");
        _btnRecordMacro.Text = _macroRecorder.IsRecording
            ? $"{Localization.Get("BtnStopRecord")} [{AppSettings.Current.RecordHotkey}]"
            : $"{Localization.Get("BtnRecord")} [{AppSettings.Current.RecordHotkey}]";

        _lblHumanizerDelay.Text = Localization.Get("LblHumanizerDelay");

        // Slim Sidebar Tooltips
        _slimToolTip.SetToolTip(_btnSlimExpand, Localization.Get("BtnUnfoldInspector"));

        InitCanvasContextMenu();
        OnCanvasSelectionChanged(_canvas.SelectedState, _canvas.SelectedEvent);
        _canvas.Invalidate();
    }


    // -------------------------------------------------------------
    // 4대 통합 뷰 모드 전환 및 동기화 엔진
    // -------------------------------------------------------------
    public void SwitchViewMode(AppViewMode newMode)
    {
        CurrentViewMode = newMode;
        AppSettings.Current.IsClassicView = IsClassicMode;
        AppSettings.Save();

        if (newMode == AppViewMode.CanvasFsm || newMode == AppViewMode.CanvasPinGraph)
        {
            _lastGraphicMode = newMode;
        }
        else
        {
            _lastClassicMode = newMode;
        }

        switch (CurrentViewMode)
        {
            case AppViewMode.CanvasFsm:
                _canvas.Visible = true;
                _classicControl.Visible = false;
                _canvas.IsPinGraphMode = false;
                _btnAutoLayout.Enabled = true;
                break;
            case AppViewMode.CanvasPinGraph:
                _canvas.Visible = true;
                _classicControl.Visible = false;
                _canvas.IsPinGraphMode = true;
                _btnAutoLayout.Enabled = true;
                break;
            case AppViewMode.ClassicList:
            case AppViewMode.ClassicTree:
                _canvas.Visible = false;
                _classicControl.Visible = true;
                _classicControl.SetTreeViewMode(true);
                _btnAutoLayout.Enabled = false;
                SetInspectorCollapsed(false);
                break;
        }

        UpdateViewTabButtons();
        SyncAndRefreshAllViews();
        string modeName = CurrentViewMode switch
        {
            AppViewMode.CanvasFsm => Localization.IsKorean ? "📦 FSM 모드" : "FSM Mode",
            AppViewMode.CanvasPinGraph => Localization.IsKorean ? "🔀 핀 모드" : "Pin Mode",
            _ => Localization.IsKorean ? "☰ 클래식 모드" : "Classic Mode"
        };
        Log(Localization.IsKorean ? $"뷰 모드 전환: {modeName}" : $"Switched view to {modeName}");
    }

    private void UpdateViewTabButtons()
    {
        bool isDark = AppSettings.Current.IsDarkMode;
        Color activeBg = isDark ? Color.FromArgb(41, 128, 185) : Color.FromArgb(52, 152, 219);
        Color activeFg = Color.White;
        Color inactiveBg = isDark ? Color.FromArgb(26, 34, 48) : SystemColors.ControlLight;
        Color inactiveFg = isDark ? Color.FromArgb(200, 210, 225) : Color.FromArgb(60, 70, 80);
        Color borderColor = isDark ? Color.FromArgb(44, 58, 80) : Color.FromArgb(200, 208, 218);

        void StyleTab(Button btn, bool isActive, Padding normalMargin)
        {
            btn.Text = "";
            btn.BackColor = isActive ? activeBg : inactiveBg;
            btn.ForeColor = isActive ? activeFg : inactiveFg;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = isActive ? activeBg : borderColor;

            if (_isCompactToolbar)
            {
                btn.AutoSize = false;
                btn.MinimumSize = Size.Empty;
                btn.Size = new Size(34, 30);
                btn.Padding = Padding.Empty;
                btn.Margin = new Padding(0, 0, 4, 0);
            }
            else
            {
                btn.AutoSize = true;
                btn.MinimumSize = new Size(88, 30);
                btn.Padding = new Padding(8, 0, 8, 0);
                btn.Margin = normalMargin;
                btn.Height = 30;
            }
            btn.Invalidate();
        }

        bool isFsm = (CurrentViewMode == AppViewMode.CanvasFsm);
        bool isPin = (CurrentViewMode == AppViewMode.CanvasPinGraph);
        bool isClassic = IsClassicMode;

        StyleTab(_btnTabFsm, isFsm, new Padding(0, 0, 2, 0));
        StyleTab(_btnTabPin, isPin, new Padding(0, 0, 2, 0));
        StyleTab(_btnTabClassic, isClassic, new Padding(0, 0, 8, 0));
    }

    private void UpdateResponsiveToolbar()
    {
        // 툴바 버튼들이 실제로 잘릴 정도로 창 폭이 좁아졌을 때만(920px 미만) 아이콘 전용 모드로 컴팩트 축소
        bool shouldBeCompact = ClientSize.Width < 920;
        if (_isCompactToolbar != shouldBeCompact)
        {
            _isCompactToolbar = shouldBeCompact;
        }

        if (_isCompactToolbar)
        {
            _btnStart.Text = "▶";
            _btnStart.AutoSize = false;
            _btnStart.MinimumSize = Size.Empty;
            _btnStart.Size = new Size(34, 30);
            _btnStart.Padding = Padding.Empty;
            _btnStart.Margin = new Padding(0, 0, 4, 0);
            _btnStart.Font = AppFontHelper.GetEmojiFont(10f, FontStyle.Bold);

            _btnStop.Text = "⏹";
            _btnStop.AutoSize = false;
            _btnStop.MinimumSize = Size.Empty;
            _btnStop.Size = new Size(34, 30);
            _btnStop.Padding = Padding.Empty;
            _btnStop.Margin = new Padding(0, 0, 6, 0);
            _btnStop.Font = AppFontHelper.GetEmojiFont(10f, FontStyle.Bold);
        }
        else
        {
            _btnStart.MinimumSize = new Size(80, 30);
            _btnStart.AutoSize = true;
            _btnStart.Padding = new Padding(8, 0, 8, 0);
            _btnStart.Margin = new Padding(0, 0, 4, 0);
            _btnStart.Font = AppFontHelper.GetEmojiFont(9f, FontStyle.Bold);
            _btnStart.Text = !_isMacroRunning
                ? Localization.Get("BtnStart")
                : (_harness != null && _harness.IsPaused ? Localization.Get("BtnResume") : Localization.Get("BtnPause"));

            _btnStop.MinimumSize = new Size(80, 30);
            _btnStop.AutoSize = true;
            _btnStop.Padding = new Padding(8, 0, 8, 0);
            _btnStop.Margin = new Padding(0, 0, 6, 0);
            _btnStop.Font = AppFontHelper.GetEmojiFont(9f, FontStyle.Bold);
            _btnStop.Text = Localization.Get("BtnStop");
        }

        _toolbarToolTip.SetToolTip(_btnStart, Localization.Get("BtnStart"));
        _toolbarToolTip.SetToolTip(_btnStop, Localization.Get("BtnStop"));
        _toolbarToolTip.SetToolTip(_btnTabFsm, Localization.IsKorean ? "📦 FSM 모드" : "FSM Mode");
        _toolbarToolTip.SetToolTip(_btnTabPin, Localization.IsKorean ? "🔀 핀 모드" : "Pin Mode");
        _toolbarToolTip.SetToolTip(_btnTabClassic, Localization.IsKorean ? "☰ 클래식 모드" : "Classic Mode");

        UpdateViewTabButtons();
    }

    public void SyncAndRefreshAllViews()
    {
        _canvas.PinGraph = PinGraphConverter.FromProfile(_profile);
        _canvas.Invalidate();
        _classicControl.Profile = _profile;
        _classicControl.PopulateHierarchicalTable();
    }


    public void ApplyTheme(bool isDark)
    {
        _currentIsDark = isDark;
        AppThemeHelper.CurrentThemeIsDark = isDark;

        // 윈도우 배경색: 짙은 다크 네이비 (Antigravity Dark Navy)
        BackColor = isDark ? Color.FromArgb(13, 17, 24) : SystemColors.Control;
        UpdateImmersiveDarkMode(isDark);

        // 1. 노드 캔버스 및 클래식 뷰 테마
        _canvas.ApplyTheme(isDark);
        _classicControl.ApplyTheme(isDark);

        // 2. 상단 툴바 테마
        _topContainer.BackColor = isDark ? Color.FromArgb(20, 26, 36) : Color.FromArgb(246, 248, 250);
        _topBar.BackColor = isDark ? Color.FromArgb(20, 26, 36) : Color.FromArgb(246, 248, 250);
        _pnlTopRight.BackColor = isDark ? Color.FromArgb(20, 26, 36) : Color.FromArgb(246, 248, 250);
        Color btnBg = isDark ? Color.FromArgb(26, 34, 48) : SystemColors.ControlLight;
        Color btnFg = isDark ? Color.White : Color.Black;
        Color btnBorder = isDark ? Color.FromArgb(44, 58, 80) : Color.FromArgb(200, 208, 218);

        void StyleFlatBtn(Button b, Color bg, Color fg, Color border)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = bg;
            b.ForeColor = fg;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = border;
        }

        StyleFlatBtn(_btnOpen, btnBg, btnFg, btnBorder);
        StyleFlatBtn(_btnSave, btnBg, btnFg, btnBorder);
        AppThemeHelper.UpdateUndoRedoButtonState(_btnUndo, isDark);
        AppThemeHelper.UpdateUndoRedoButtonState(_btnRedo, isDark);
        StyleFlatBtn(_btnAddStateNode, btnBg, btnFg, btnBorder);
        StyleFlatBtn(_btnAddEventToNode, btnBg, btnFg, btnBorder);
        StyleFlatBtn(_btnAutoLayout, btnBg, btnFg, btnBorder);
        StyleFlatBtn(_btnSettings, btnBg, btnFg, btnBorder);
        StyleFlatBtn(_btnToggleInspector, btnBg, btnFg, btnBorder);
        UpdateViewTabButtons();

        _lblStatus.ForeColor = isDark ? Color.FromArgb(220, 230, 245) : Color.DarkSlateGray;
        _lblActiveState.ForeColor = isDark ? Color.FromArgb(120, 195, 255) : Color.FromArgb(41, 128, 185);
        _lblEmergency.ForeColor = isDark ? Color.FromArgb(255, 120, 120) : Color.DarkRed;

        // 3. 슬림 사이드바 테마
        _slimSideBar.BackColor = isDark ? Color.FromArgb(20, 26, 36) : Color.FromArgb(235, 240, 248);
        Color slimBtnBg = isDark ? Color.FromArgb(26, 34, 48) : Color.FromArgb(220, 226, 235);
        Color slimBtnFg = isDark ? Color.White : Color.FromArgb(30, 40, 55);
        Color slimBtnBorder = isDark ? Color.FromArgb(44, 58, 80) : Color.FromArgb(195, 205, 218);
        foreach (Control c in _slimSideBar.Controls)
        {
            if (c is Button b)
            {
                b.BackColor = slimBtnBg;
                b.ForeColor = slimBtnFg;
                b.FlatAppearance.BorderColor = slimBtnBorder;
            }
        }

        // 4. 하단 로그창 테마 (박스 내부 짙은 네이비, 텍스트 화이트)
        _bottomBar.BackColor = isDark ? Color.FromArgb(20, 26, 36) : Color.FromArgb(246, 248, 250);
        _lblLogTitle.ForeColor = isDark ? Color.White : Color.Black;
        StyleFlatBtn(_btnToggleLog, btnBg, btnFg, btnBorder);
        if (_logContainer != null) _logContainer.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
        _logBox.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
        _logBox.ForeColor = isDark ? Color.White : Color.Black;
        _logBox.Invalidate();
        if (_scrollLog != null)
        {
            _scrollLog.IsDark = isDark;
            _scrollLog.UpdateMetrics();
        }

        // 5. 우측 인스펙터 패널 테마 (텍스트는 완전한 하얀색, 박스 내부는 짙은 네이비)
        _pnlInspector.BackColor = isDark ? Color.FromArgb(18, 24, 34) : Color.FromArgb(248, 249, 251);
        if (_scrollInspector != null) _scrollInspector.IsDark = isDark;
        _lblInspectorTitle.ForeColor = isDark ? Color.White : Color.FromArgb(44, 62, 80);
        _lblInspectorTitle.Invalidate();
        _btnCollapseInspector.BackColor = Color.FromArgb(41, 128, 185);
        _btnCollapseInspector.ForeColor = Color.White;

        _grpStateProps.ForeColor = isDark ? Color.White : Color.FromArgb(44, 62, 80);
        _txtStateName.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
        _txtStateName.ForeColor = isDark ? Color.White : Color.Black;
        StyleFlatBtn(_btnSetInitial, btnBg, isDark ? Color.FromArgb(241, 196, 15) : Color.FromArgb(212, 143, 0), btnBorder);
        StyleFlatBtn(_btnDeleteState, btnBg, isDark ? Color.FromArgb(231, 76, 60) : Color.FromArgb(192, 57, 43), btnBorder);

        _grpEventProps.ForeColor = isDark ? Color.White : Color.FromArgb(44, 62, 80);
        _lblEvtName.ForeColor = isDark ? Color.White : Color.Black;
        _txtEventName.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
        _txtEventName.ForeColor = isDark ? Color.White : Color.Black;
        _chkEventEnabled.IsDark = isDark;
        _chkEventEnabled.ForeColor = isDark ? Color.White : Color.Black;
        _chkParallel.IsDark = isDark;
        _chkParallel.ForeColor = isDark ? Color.White : Color.Black;
        _lblHumanizerDelay.ForeColor = isDark ? Color.White : Color.Black;
        _sliderHumanizerDelay.BackColor = isDark ? Color.FromArgb(18, 24, 34) : Color.FromArgb(248, 249, 251);

        _lblTriggerHeader.ForeColor = isDark ? Color.White : Color.FromArgb(41, 128, 185);
        _lblTriggerHeader.Invalidate();
        _lvTriggers.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
        _lvTriggers.ForeColor = isDark ? Color.White : Color.Black;
        if (_lvTriggers.IsHandleCreated)
        {
            try { AppThemeHelper.SetWindowTheme(_lvTriggers.Handle, isDark ? "DarkMode_Explorer" : null, null); } catch { }
        }
        AppThemeHelper.RefreshListViewHeader(_lvTriggers, isDark);
        _lvTriggers.Invalidate();
        StyleFlatBtn(_btnAddTrigger, btnBg, btnFg, btnBorder);
        StyleFlatBtn(_btnEditTrigger, btnBg, btnFg, btnBorder);
        StyleFlatBtn(_btnDeleteTrigger, btnBg, btnFg, btnBorder);

        _lblActionHeader.ForeColor = isDark ? Color.White : Color.FromArgb(211, 84, 0);
        _lblActionHeader.Invalidate();
        _lvActions.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
        _lvActions.ForeColor = isDark ? Color.White : Color.Black;
        if (_lvActions.IsHandleCreated)
        {
            try { AppThemeHelper.SetWindowTheme(_lvActions.Handle, isDark ? "DarkMode_Explorer" : null, null); } catch { }
        }
        AppThemeHelper.RefreshListViewHeader(_lvActions, isDark);
        _lvActions.Invalidate();
        StyleFlatBtn(_btnAddAction, btnBg, btnFg, btnBorder);
        StyleFlatBtn(_btnEditAction, btnBg, btnFg, btnBorder);
        StyleFlatBtn(_btnDeleteAction, btnBg, btnFg, btnBorder);
        StyleFlatBtn(_btnMoveUpAction, btnBg, btnFg, btnBorder);
        StyleFlatBtn(_btnMoveDownAction, btnBg, btnFg, btnBorder);

        if (!_macroRecorder.IsRecording)
        {
            _btnRecordMacro.BackColor = isDark ? Color.FromArgb(192, 57, 43) : Color.FromArgb(231, 76, 60);
            _btnRecordMacro.ForeColor = Color.White;
        }

        if (AppSettings.Current.HasOpenedTemplateWizard)
        {
            StyleFlatBtn(_btnTemplateWizard, btnBg, btnFg, btnBorder);
        }
        else
        {
            StyleFlatBtn(_btnTemplateWizard, isDark ? Color.FromArgb(41, 128, 185) : Color.FromArgb(52, 152, 219), Color.White, btnBorder);
        }

        // AI 어시스턴트 버튼은 신비로운 퍼플 톤으로 포인트를 주어 시각적 인지성 극대화
        StyleFlatBtn(_btnAiAssistant, isDark ? Color.FromArgb(142, 68, 173) : Color.FromArgb(155, 89, 182), Color.White, btnBorder);

        _splitMain.BackColor = isDark ? Color.FromArgb(28, 36, 50) : Color.FromArgb(220, 224, 230);
        UpdateUndoRedoButtons();
    }


}

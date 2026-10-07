using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

public partial class NodeCanvasControl
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 1. 다크 블루프린트 격자 배경 렌더링
        DrawGrid(g);

        // 뷰포트 월드 변환 적용
        var originalTransform = g.Transform;
        using var worldMatrix = new Matrix();
        worldMatrix.Translate(PanOffset.X, PanOffset.Y);
        worldMatrix.Scale(Zoom, Zoom);
        g.Transform = worldMatrix;

        if (IsPinGraphMode)
        {
            // 2-A. 완전 자유형 핀 그래프 와이어 렌더링 (ComfyUI / 블루프린트 베지어 곡선)
            DrawPinGraphWires(g);

            // 3-A. 핀 드래그 연결 중일 때 마우스 포인터를 따라가는 가상 와이어
            if (IsPinDragging && DraggingSourcePin != null)
            {
                DrawPinDraggingCurve(g);
            }

            // 4-A. 자유형 핀 그래프 노드 렌더링 (독립 조건/행동/분기 블록)
            DrawPinGraphNodes(g);
        }
        else
        {
            // 2-B. 레거시 FSM 상태 전이 곡선(Transitions / Edges) 렌더링
            DrawTransitions(g);

            // 3-B. 포트 연결 중일 때 마우스 포인터를 따라가는 가상 전이선 렌더링
            if (_isConnecting)
            {
                DrawConnectingCurve(g);
            }

            // 4-B. 상태 노드(State Nodes) 및 인라인 조건/결과 칩 렌더링
            DrawStateNodes(g);
        }

        // 5. 빈 영역 마키 드래그 선택 박스 렌더링
        if (_isMarqueeSelecting)
        {
            float mx = Math.Min(_marqueeStartWorldPt.X, _marqueeCurrentWorldPt.X);
            float my = Math.Min(_marqueeStartWorldPt.Y, _marqueeCurrentWorldPt.Y);
            float mw = Math.Abs(_marqueeCurrentWorldPt.X - _marqueeStartWorldPt.X);
            float mh = Math.Abs(_marqueeCurrentWorldPt.Y - _marqueeStartWorldPt.Y);
            var marqueeRect = new RectangleF(mx, my, mw, mh);

            using var marqueeBrush = new SolidBrush(Color.FromArgb(35, 52, 152, 219));
            using var marqueeBorder = new Pen(Color.FromArgb(52, 152, 219), 1.2f) { DashStyle = DashStyle.Dash };
            g.FillRectangle(marqueeBrush, marqueeRect);
            g.DrawRectangle(marqueeBorder, marqueeRect.X, marqueeRect.Y, marqueeRect.Width, marqueeRect.Height);
        }

        // 뷰포트 원복
        g.Transform = originalTransform;

        // 5. 우측 상단 미니 HUD
        DrawHudOverlay(g);
    }

    private void DrawGrid(Graphics g)
    {
        float gridSize = 40f * Zoom;
        float offsetX = PanOffset.X % gridSize;
        float offsetY = PanOffset.Y % gridSize;

        using var penMinor = new Pen(IsDarkMode ? Color.FromArgb(22, 30, 42) : Color.FromArgb(215, 226, 238), 1);
        using var penMajor = new Pen(IsDarkMode ? Color.FromArgb(32, 44, 62) : Color.FromArgb(190, 206, 224), 1.5f);

        for (float x = offsetX; x < Width; x += gridSize)
        {
            int cellIndex = (int)Math.Round((x - PanOffset.X) / gridSize);
            g.DrawLine(cellIndex % 4 == 0 ? penMajor : penMinor, x, 0, x, Height);
        }

        for (float y = offsetY; y < Height; y += gridSize)
        {
            int cellIndex = (int)Math.Round((y - PanOffset.Y) / gridSize);
            g.DrawLine(cellIndex % 4 == 0 ? penMajor : penMinor, 0, y, Width, y);
        }
    }

    private void DrawStateNodes(Graphics g)
    {
        using var fontHeader = AppFontHelper.GetEmojiFont(10.5f, FontStyle.Bold);
        using var fontEvtTitle = AppFontHelper.GetEmojiFont(9f, FontStyle.Bold);
        using var fontSecTitle = AppFontHelper.GetEmojiFont(8f, FontStyle.Bold);
        using var fontChip = AppFontHelper.GetEmojiFont(8f, FontStyle.Regular);
        using var fontChipBold = AppFontHelper.GetEmojiFont(8f, FontStyle.Bold);
        using var fontBadge = AppFontHelper.GetEmojiFont(7.2f, FontStyle.Bold);
        using var fontKeycap = new Font("Consolas", 8.5f, FontStyle.Bold);
        using var fontBtn = AppFontHelper.GetEmojiFont(7.5f, FontStyle.Bold);
        using var fontEmpty = AppFontHelper.GetEmojiFont(7.8f, FontStyle.Italic);

        foreach (var state in _profile.States)
        {
            if (!_profile.NodePositions.TryGetValue(state, out var pos)) continue;

            var layout = CalculateStateLayout(state, pos);
            var nodeRect = layout.Bounds;
            bool isSelected = (SelectedState == state);
            bool isActive = (_activeState == state);
            bool isHoveredTarget = (_connectingTargetHoverState == state);

            float width = layout.Bounds.Width;
            float nodeScale = Math.Clamp(width / 340f, 0.7f, 3.0f);
            float fontScale = Math.Clamp((float)Math.Pow(nodeScale, 0.65), 0.85f, 2.2f);
            bool customScale = Math.Abs(fontScale - 1.0f) > 0.03f;

            Font fHeader = customScale ? AppFontHelper.GetEmojiFont(10.5f * fontScale, FontStyle.Bold) : fontHeader;
            Font fEvtTitle = customScale ? AppFontHelper.GetEmojiFont(9f * fontScale, FontStyle.Bold) : fontEvtTitle;
            Font fSecTitle = customScale ? AppFontHelper.GetEmojiFont(8f * fontScale, FontStyle.Bold) : fontSecTitle;
            Font fChip = customScale ? AppFontHelper.GetEmojiFont(8f * fontScale, FontStyle.Regular) : fontChip;
            Font fChipBold = customScale ? AppFontHelper.GetEmojiFont(8f * fontScale, FontStyle.Bold) : fontChipBold;
            Font fBadge = customScale ? AppFontHelper.GetEmojiFont(7.2f * fontScale, FontStyle.Bold) : fontBadge;
            Font fEmpty = customScale ? AppFontHelper.GetEmojiFont(7.8f * fontScale, FontStyle.Italic) : fontEmpty;

            bool allDisabled = layout.Events.Count > 0 && layout.Events.All(e => !e.Event.IsEnabled);

            // 1. 노드 그림자
            var shadowRect = new RectangleF(nodeRect.X + 4, nodeRect.Y + 6, nodeRect.Width, nodeRect.Height);
            using (var shadowBrush = new SolidBrush(Color.FromArgb(IsDarkMode ? 60 : 25, 0, 0, 0)))
            {
                FillRoundedRectangle(g, shadowBrush, shadowRect, 10);
            }

            // 2. 노드 몸체 배경 (비활성화 상태 노드는 회색 타일 처리)
            Color bodyBg = allDisabled
                ? (IsDarkMode ? Color.FromArgb(22, 25, 32) : Color.FromArgb(235, 238, 242))
                : (IsDarkMode ? Color.FromArgb(20, 27, 38) : Color.White);

            using (var bodyBrush = new SolidBrush(bodyBg))
            {
                FillRoundedRectangle(g, bodyBrush, nodeRect, 10);
            }

            // 3. 노드 헤더
            Color headerColorStart, headerColorEnd;
            string stateBadge = "";

            if (allDisabled)
            {
                headerColorStart = IsDarkMode ? Color.FromArgb(50, 56, 66) : Color.FromArgb(175, 180, 190);
                headerColorEnd = IsDarkMode ? Color.FromArgb(40, 45, 54) : Color.FromArgb(155, 160, 170);
                stateBadge = "⏸️ ";
            }
            else if (MacroEventProfile.IsGlobalState(state))
            {
                headerColorStart = Color.FromArgb(108, 92, 231);
                headerColorEnd = Color.FromArgb(142, 68, 173);
                stateBadge = "🌐 ";
            }
            else if (state == _profile.InitialState)
            {
                headerColorStart = Color.FromArgb(0, 184, 148);
                headerColorEnd = Color.FromArgb(39, 174, 96);
                stateBadge = "⭐ ";
            }
            else
            {
                headerColorStart = Color.FromArgb(9, 132, 227);
                headerColorEnd = Color.FromArgb(41, 128, 185);
                stateBadge = "🏷️ ";
            }

            using (var headerBrush = new LinearGradientBrush(layout.HeaderBounds, headerColorStart, headerColorEnd, LinearGradientMode.Horizontal))
            {
                FillRoundedHeader(g, headerBrush, layout.HeaderBounds, 10);
            }

            string displayTitle = MacroEventProfile.FormatStateDisplay(state, Localization.IsKorean);
            string headerText = $"{stateBadge}{displayTitle}";
            if (state == _profile.InitialState) headerText += Localization.IsKorean ? " (시작)" : " (Initial)";
            var headerBounds = new RectangleF(nodeRect.X + 14 * fontScale, nodeRect.Y + 4 * fontScale, nodeRect.Width - 28 * fontScale, 24 * fontScale);
            AppFontHelper.DrawMixedString(g, headerText, 10.5f * fontScale, allDisabled ? (IsDarkMode ? Brushes.LightGray : Brushes.DimGray) : Brushes.White, headerBounds, StringAlignment.Near, StringAlignment.Center, FontStyle.Bold);

            // 4. 이벤트 카드 및 인라인 조건/결과 칩 렌더링
            foreach (var el in layout.Events)
            {
                var evt = el.Event;
                bool isEvtSelected = (SelectedEvent == evt);

                // 이벤트 카드 배경 (비활성화 이벤트는 회색 타일 처리)
                Color evtBg = isEvtSelected
                    ? (IsDarkMode ? Color.FromArgb(32, 45, 66) : Color.FromArgb(215, 232, 252))
                    : (evt.IsEnabled ? (IsDarkMode ? Color.FromArgb(24, 33, 48) : Color.FromArgb(246, 248, 252)) : (IsDarkMode ? Color.FromArgb(28, 32, 40) : Color.FromArgb(226, 230, 236)));

                using (var evtBgBrush = new SolidBrush(evtBg))
                {
                    FillRoundedRectangle(g, evtBgBrush, el.Bounds, 6);
                }

                Color evtBorderColor = isEvtSelected
                    ? (IsDarkMode ? Color.FromArgb(100, 150, 220) : Color.FromArgb(41, 128, 185))
                    : (evt.IsEnabled ? (IsDarkMode ? Color.FromArgb(40, 52, 72) : Color.FromArgb(200, 210, 225)) : (IsDarkMode ? Color.FromArgb(44, 50, 60) : Color.FromArgb(195, 202, 212)));

                using (var borderPen = new Pen(evtBorderColor, 1f))
                {
                    DrawRoundedRectangle(g, borderPen, el.Bounds, 6);
                }

                // 이벤트 헤더: 이름 & 병렬 뱃지 (비활성화 시 회색 처리)
                Color evtHeaderBg;
                if (!evt.IsEnabled)
                {
                    evtHeaderBg = IsDarkMode ? Color.FromArgb(36, 40, 50) : Color.FromArgb(212, 218, 226);
                }
                else if (isEvtSelected)
                {
                    evtHeaderBg = IsDarkMode ? Color.FromArgb(42, 68, 105) : Color.FromArgb(200, 222, 248);
                }
                else if (evt.IsParallel)
                {
                    evtHeaderBg = IsDarkMode ? Color.FromArgb(32, 50, 78) : Color.FromArgb(215, 230, 248);
                }
                else
                {
                    evtHeaderBg = IsDarkMode ? Color.FromArgb(36, 44, 58) : Color.FromArgb(226, 232, 242);
                }

                using (var headerBrush = new SolidBrush(evtHeaderBg))
                {
                    FillRoundedHeader(g, headerBrush, el.HeaderBounds, 6);
                }

                using (var sepPen = new Pen(IsDarkMode ? Color.FromArgb(48, 62, 85) : Color.FromArgb(200, 212, 228), 1f))
                {
                    g.DrawLine(sepPen, el.HeaderBounds.Left, el.HeaderBounds.Bottom, el.HeaderBounds.Right, el.HeaderBounds.Bottom);
                }

                string icon = evt.IsParallel ? "⚡" : "➡️";
                string displayEventName = MacroEventProfile.FormatEventDisplay(evt, Localization.IsKorean);
                string title = $"{icon} {displayEventName}";
                if (evt.IsParallel) title += Localization.IsKorean ? " [병렬]" : " [Parallel]";
                Brush evtTitleBrush = evt.IsEnabled ? (IsDarkMode ? Brushes.White : Brushes.Black) : (IsDarkMode ? Brushes.Gray : Brushes.DarkGray);
                var evtHeaderRect = new RectangleF(el.HeaderBounds.X + 8 * fontScale, el.HeaderBounds.Y + 2 * fontScale, el.HeaderBounds.Width - 16 * fontScale, el.HeaderBounds.Height - 4 * fontScale);
                AppFontHelper.DrawMixedString(g, title, 9f * fontScale, evtTitleBrush, evtHeaderRect, StringAlignment.Near, StringAlignment.Center, FontStyle.Bold);

                // 조건 섹션 헤더
                var trgSecRect = new RectangleF(el.TriggerSectionBounds.X + 4 * fontScale, el.TriggerSectionBounds.Y + 1, el.TriggerSectionBounds.Width, 18);
                AppFontHelper.DrawMixedString(g, Localization.IsKorean ? "🔹 발동 조건" : "🔹 Triggers", 8f * fontScale, evt.IsEnabled ? (IsDarkMode ? Brushes.LightSkyBlue : Brushes.DarkBlue) : Brushes.Gray, trgSecRect, StringAlignment.Near, StringAlignment.Center, FontStyle.Bold);

                // 조건 칩들
                if (el.TriggerChips.Count == 0)
                {
                    g.DrawString("💡 항상 발동 (조건 없음)", fEmpty, IsDarkMode ? Brushes.Gray : Brushes.DarkGray, el.TriggerSectionBounds.X + 8 * fontScale, el.TriggerSectionBounds.Bottom + 2 * fontScale);
                }
                else
                {
                    foreach (var tc in el.TriggerChips)
                    {
                        var trg = tc.Trigger;
                        Color chipBgColor = evt.IsEnabled
                            ? (IsDarkMode ? Color.FromArgb(26, 36, 52) : Color.FromArgb(235, 240, 248))
                            : (IsDarkMode ? Color.FromArgb(30, 34, 42) : Color.FromArgb(218, 222, 228));
                        using var chipBrush = new SolidBrush(chipBgColor);
                        FillRoundedRectangle(g, chipBrush, tc.ChipBounds, 5);

                        Color chipBorderColor = evt.IsEnabled
                            ? (IsDarkMode ? Color.FromArgb(48, 64, 90) : Color.FromArgb(190, 205, 225))
                            : (IsDarkMode ? Color.FromArgb(42, 48, 58) : Color.FromArgb(185, 192, 202));
                        using var chipBorder = new Pen(chipBorderColor, 1f);
                        DrawRoundedRectangle(g, chipBorder, tc.ChipBounds, 5);

                        // 조건 감지 발동 시 1초 강조 후 3초에 걸쳐 서서히 페이드아웃
                        int trgIdx = el.TriggerChips.IndexOf(tc);
                        if (_firedTriggers.TryGetValue((evt.Id, trgIdx), out var firedAt))
                        {
                            double elapsed = (DateTime.Now - firedAt).TotalSeconds;
                            if (elapsed <= 4.0)
                            {
                                float fade = elapsed <= 1.0 ? 1.0f : (1.0f - (float)((elapsed - 1.0) / 3.0));
                                int alpha = Math.Clamp((int)(fade * 255), 0, 255);
                                using var firedBg = new SolidBrush(Color.FromArgb((int)(alpha * 0.25f), 46, 204, 113));
                                FillRoundedRectangle(g, firedBg, tc.ChipBounds, 5);
                                using var firedPen = new Pen(Color.FromArgb(alpha, 46, 204, 113), 2.2f);
                                DrawRoundedRectangle(g, firedPen, tc.ChipBounds, 5);
                            }
                        }

                        float contentX = tc.ChipBounds.X + 6;
                        float centerY = tc.ChipBounds.Y + tc.ChipBounds.Height / 2;

                        if (trg.Type == TriggerType.GaugeBar)
                        {
                            // 1. 대형 색상 스와치 (Color Swatch)
                            Color color = Color.FromArgb(231, 76, 60);
                            try
                            {
                                if (!string.IsNullOrEmpty(trg.CustomColorHex))
                                    color = ColorTranslator.FromHtml(trg.CustomColorHex);
                            }
                            catch { }

                            using (var swatchBrush = new SolidBrush(color))
                            {
                                FillRoundedRectangle(g, swatchBrush, tc.SwatchBounds, 5);
                            }
                            using (var borderPen = new Pen(Color.WhiteSmoke, 1f))
                            {
                                DrawRoundedRectangle(g, borderPen, tc.SwatchBounds, 5);
                            }

                            // 2. 상단: 뱃지 및 연산자
                            var badgeRect = new RectangleF(tc.ChipBounds.X + 52, tc.ChipBounds.Y + 5, 32, 15);
                            using (var badgeBrush = new SolidBrush(Color.FromArgb(30, 80, 110)))
                            {
                                FillRoundedRectangle(g, badgeBrush, badgeRect, 3);
                            }
                            g.DrawString("밀도", fontBadge, Brushes.LightCyan, badgeRect.X + 4, badgeRect.Y + 1);

                            string opStr = trg.ComparisonOp switch
                            {
                                ComparisonOperator.LessThan => "<",
                                ComparisonOperator.GreaterThan => ">",
                                ComparisonOperator.GreaterThanOrEqual => ">=",
                                _ => "<="
                            };
                            g.DrawString(opStr, fontChipBold, IsDarkMode ? Brushes.LightGreen : Brushes.DarkGreen, tc.ComparisonOpBounds.X + 36, tc.ComparisonOpBounds.Y);

                            // 3. 중단: 색상 정확도 프로그레스 바
                            if (tc.HasGauge)
                            {
                                using (var trackBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(16, 20, 28) : Color.FromArgb(205, 215, 225)))
                                {
                                    FillRoundedRectangle(g, trackBrush, tc.GaugeTrackBounds, 3);
                                }
                                float fillW = Math.Clamp((float)(tc.GaugeTrackBounds.Width * (trg.ColorAccuracyPercent / 100.0)), 0, tc.GaugeTrackBounds.Width);
                                if (fillW > 0)
                                {
                                    var fillRect = new RectangleF(tc.GaugeTrackBounds.X, tc.GaugeTrackBounds.Y, fillW, tc.GaugeTrackBounds.Height);
                                    using var fillBrush = new SolidBrush(Color.FromArgb(52, 152, 219));
                                    FillRoundedRectangle(g, fillBrush, fillRect, 3);
                                }
                                using (var trackBorder = new Pen(IsDarkMode ? Color.FromArgb(50, 60, 75) : Color.FromArgb(170, 185, 205), 1f))
                                {
                                    DrawRoundedRectangle(g, trackBorder, tc.GaugeTrackBounds, 3);
                                }
                                g.DrawString($"정확 {(int)trg.ColorAccuracyPercent}%", fontBadge, IsDarkMode ? Brushes.LightSkyBlue : Brushes.DarkBlue, tc.GaugeTrackBounds.Right + 4, tc.GaugeTrackBounds.Y - 2);
                            }

                            // 4. 하단: 기준 잔여량 프로그레스 바
                            if (tc.HasGauge2)
                            {
                                using (var trackBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(16, 20, 28) : Color.FromArgb(205, 215, 225)))
                                {
                                    FillRoundedRectangle(g, trackBrush, tc.GaugeTrackBounds2, 3);
                                }
                                float fillW = Math.Clamp((float)(tc.GaugeTrackBounds2.Width * (trg.GaugeThresholdPercent / 100.0)), 0, tc.GaugeTrackBounds2.Width);
                                if (fillW > 0)
                                {
                                    var fillRect = new RectangleF(tc.GaugeTrackBounds2.X, tc.GaugeTrackBounds2.Y, fillW, tc.GaugeTrackBounds2.Height);
                                    using var fillBrush = new SolidBrush(Color.FromArgb(46, 204, 113));
                                    FillRoundedRectangle(g, fillBrush, fillRect, 3);
                                }
                                using (var trackBorder = new Pen(IsDarkMode ? Color.FromArgb(50, 60, 75) : Color.FromArgb(170, 185, 205), 1f))
                                {
                                    DrawRoundedRectangle(g, trackBorder, tc.GaugeTrackBounds2, 3);
                                }
                                g.DrawString($"잔여 {(int)trg.GaugeThresholdPercent}% {opStr}", fontBadge, IsDarkMode ? Brushes.LightGreen : Brushes.DarkGreen, tc.GaugeTrackBounds2.Right + 4, tc.GaugeTrackBounds2.Y - 2);
                            }
                        }
                        else if (trg.Type == TriggerType.ImageDetection)
                        {
                            // 1. 원본 비율 레터박스 템플릿 이미지 대형 썸네일
                            string fn = string.IsNullOrEmpty(trg.ImagePath) ? "템플릿" : (System.IO.Path.GetFileNameWithoutExtension(trg.ImagePath) ?? "템플릿");
                            var img = GetThumbnail(trg.ImageBase64, trg.ImagePath);
                            DrawLetterboxedImage(g, img, tc.FileLetterboxBounds, fn);

                            // 2. 상단: [IMG] 뱃지 및 파일명
                            var badgeRect = new RectangleF(tc.ChipBounds.X + 54, tc.ChipBounds.Y + 6, 32, 16);
                            using (var badgeBrush = new SolidBrush(Color.FromArgb(108, 92, 231)))
                            {
                                FillRoundedRectangle(g, badgeBrush, badgeRect, 3);
                            }
                            g.DrawString("IMG", fontBadge, Brushes.White, badgeRect.X + 4, badgeRect.Y + 1);

                            string shortFn = fn.Length > 8 ? fn[..8] + ".." : fn;
                            g.DrawString(shortFn, fontChip, IsDarkMode ? Brushes.WhiteSmoke : Brushes.Black, badgeRect.Right + 4, badgeRect.Y);

                            // 3. 하단: 정확도 단순 프로그레스 게이지 바
                            if (tc.HasGauge)
                            {
                                using (var trackBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(16, 20, 28) : Color.FromArgb(205, 215, 225)))
                                {
                                    FillRoundedRectangle(g, trackBrush, tc.GaugeTrackBounds, 3);
                                }
                                float fillW = Math.Clamp((float)(tc.GaugeTrackBounds.Width * (trg.ImageAccuracyPercent / 100.0)), 0, tc.GaugeTrackBounds.Width);
                                if (fillW > 0)
                                {
                                    var fillRect = new RectangleF(tc.GaugeTrackBounds.X, tc.GaugeTrackBounds.Y, fillW, tc.GaugeTrackBounds.Height);
                                    using var fillBrush = new SolidBrush(Color.FromArgb(52, 152, 219));
                                    FillRoundedRectangle(g, fillBrush, fillRect, 3);
                                }
                                using (var trackBorder = new Pen(IsDarkMode ? Color.FromArgb(50, 60, 75) : Color.FromArgb(170, 185, 205), 1f))
                                {
                                    DrawRoundedRectangle(g, trackBorder, tc.GaugeTrackBounds, 3);
                                }
                                g.DrawString($"{(int)trg.ImageAccuracyPercent}%", fontChipBold, IsDarkMode ? Brushes.LightSkyBlue : Brushes.DarkBlue, tc.GaugeTrackBounds.Right + 5, tc.GaugeTrackBounds.Y - 3);
                            }
                        }
                        else if (trg.Type == TriggerType.VisionDetection)
                        {
                            if (trg.VisionTarget == VisionTargetType.Color)
                            {
                                // 1. 대형 색상 스와치
                                Color color = Color.FromArgb(231, 76, 60);
                                try
                                {
                                    if (!string.IsNullOrEmpty(trg.CustomColorHex))
                                        color = ColorTranslator.FromHtml(trg.CustomColorHex);
                                }
                                catch { }
                                using (var swatchBrush = new SolidBrush(color))
                                {
                                    FillRoundedRectangle(g, swatchBrush, tc.SwatchBounds, 5);
                                }
                                using (var borderPen = new Pen(Color.WhiteSmoke, 1f))
                                {
                                    DrawRoundedRectangle(g, borderPen, tc.SwatchBounds, 5);
                                }

                                // 2. 상단: [색상] [일치/불일치] 뱃지
                                var badgeRect = new RectangleF(tc.ChipBounds.X + 52, tc.ChipBounds.Y + 5, 28, 15);
                                using (var badgeBrush = new SolidBrush(Color.FromArgb(30, 80, 110)))
                                {
                                    FillRoundedRectangle(g, badgeBrush, badgeRect, 3);
                                }
                                g.DrawString("색상", fontBadge, Brushes.LightCyan, badgeRect.X + 3, badgeRect.Y + 1);

                                bool isMatch = (trg.VisionMatchMode == VisionMatchMode.Match);
                                string matchText = isMatch ? "일치" : "불일치";
                                var matchTextSize = g.MeasureString(matchText, fontBadge);
                                float matchW = Math.Max(32f, matchTextSize.Width + 6f);
                                var matchRect = new RectangleF(badgeRect.Right + 3, tc.ChipBounds.Y + 5, matchW, 15);
                                using (var matchBrush = new SolidBrush(isMatch ? Color.FromArgb(39, 174, 96) : Color.FromArgb(192, 57, 43)))
                                {
                                    FillRoundedRectangle(g, matchBrush, matchRect, 3);
                                }
                                g.DrawString(matchText, fontBadge, Brushes.White, matchRect.X + (matchW - matchTextSize.Width) / 2, matchRect.Y + 1);

                                string hexStr = trg.CustomColorHex ?? "#E74C3C";
                                g.DrawString(hexStr, fontChip, IsDarkMode ? Brushes.LightGray : Brushes.DarkSlateGray, matchRect.Right + 4, matchRect.Y);

                                // 3. 중단: 색상 정확도 프로그레스 바
                                if (tc.HasGauge)
                                {
                                    using (var trackBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(16, 20, 28) : Color.FromArgb(205, 215, 225)))
                                    {
                                        FillRoundedRectangle(g, trackBrush, tc.GaugeTrackBounds, 3);
                                    }
                                    float fillW = Math.Clamp((float)(tc.GaugeTrackBounds.Width * (trg.ColorAccuracyPercent / 100.0)), 0, tc.GaugeTrackBounds.Width);
                                    if (fillW > 0)
                                    {
                                        var fillRect = new RectangleF(tc.GaugeTrackBounds.X, tc.GaugeTrackBounds.Y, fillW, tc.GaugeTrackBounds.Height);
                                        using var fillBrush = new SolidBrush(Color.FromArgb(52, 152, 219));
                                        FillRoundedRectangle(g, fillBrush, fillRect, 3);
                                    }
                                    using (var trackBorder = new Pen(IsDarkMode ? Color.FromArgb(50, 60, 75) : Color.FromArgb(170, 185, 205), 1f))
                                    {
                                        DrawRoundedRectangle(g, trackBorder, tc.GaugeTrackBounds, 3);
                                    }
                                    g.DrawString($"정확 {(int)trg.ColorAccuracyPercent}%", fontBadge, IsDarkMode ? Brushes.LightSkyBlue : Brushes.DarkBlue, tc.GaugeTrackBounds.Right + 4, tc.GaugeTrackBounds.Y - 2);
                                }

                                // 4. 하단: 기준 잔여량 프로그레스 바
                                if (tc.HasGauge2)
                                {
                                    using (var trackBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(16, 20, 28) : Color.FromArgb(205, 215, 225)))
                                    {
                                        FillRoundedRectangle(g, trackBrush, tc.GaugeTrackBounds2, 3);
                                    }
                                    float fillW = Math.Clamp((float)(tc.GaugeTrackBounds2.Width * (trg.GaugeThresholdPercent / 100.0)), 0, tc.GaugeTrackBounds2.Width);
                                    if (fillW > 0)
                                    {
                                        var fillRect = new RectangleF(tc.GaugeTrackBounds2.X, tc.GaugeTrackBounds2.Y, fillW, tc.GaugeTrackBounds2.Height);
                                        using var fillBrush = new SolidBrush(Color.FromArgb(46, 204, 113));
                                        FillRoundedRectangle(g, fillBrush, fillRect, 3);
                                    }
                                    using (var trackBorder = new Pen(IsDarkMode ? Color.FromArgb(50, 60, 75) : Color.FromArgb(170, 185, 205), 1f))
                                    {
                                        DrawRoundedRectangle(g, trackBorder, tc.GaugeTrackBounds2, 3);
                                    }

                                    string opStr = trg.ComparisonOp switch
                                    {
                                        ComparisonOperator.LessThan => "<",
                                        ComparisonOperator.GreaterThan => ">",
                                        ComparisonOperator.GreaterThanOrEqual => ">=",
                                        _ => "<="
                                    };
                                    g.DrawString($"잔여 {(int)trg.GaugeThresholdPercent}% {opStr}", fontBadge, IsDarkMode ? Brushes.LightGreen : Brushes.DarkGreen, tc.GaugeTrackBounds2.Right + 4, tc.GaugeTrackBounds2.Y - 2);
                                }
                            }
                            else
                            {
                                // 1. 원본 비율 레터박스 템플릿 이미지 대형 썸네일
                                string fn = string.IsNullOrEmpty(trg.ImagePath) ? "템플릿" : (System.IO.Path.GetFileNameWithoutExtension(trg.ImagePath) ?? "템플릿");
                                var img = GetThumbnail(trg.ImageBase64, trg.ImagePath);
                                DrawLetterboxedImage(g, img, tc.FileLetterboxBounds, fn);

                                // 2. 상단: [이미지] [일치/불일치] 뱃지
                                var badgeRect = new RectangleF(tc.ChipBounds.X + 54, tc.ChipBounds.Y + 6, 32, 16);
                                using (var badgeBrush = new SolidBrush(Color.FromArgb(108, 92, 231)))
                                {
                                    FillRoundedRectangle(g, badgeBrush, badgeRect, 3);
                                }
                                g.DrawString("이미지", fontBadge, Brushes.White, badgeRect.X + 2, badgeRect.Y + 1);

                                bool isMatch = (trg.VisionMatchMode == VisionMatchMode.Match);
                                string matchText = isMatch ? "일치" : "불일치";
                                var matchTextSize = g.MeasureString(matchText, fontBadge);
                                float matchW = Math.Max(32f, matchTextSize.Width + 6f);
                                var matchRect = new RectangleF(badgeRect.Right + 4, tc.ChipBounds.Y + 6, matchW, 16);
                                using (var matchBrush = new SolidBrush(isMatch ? Color.FromArgb(39, 174, 96) : Color.FromArgb(192, 57, 43)))
                                {
                                    FillRoundedRectangle(g, matchBrush, matchRect, 3);
                                }
                                g.DrawString(matchText, fontBadge, Brushes.White, matchRect.X + (matchW - matchTextSize.Width) / 2, matchRect.Y + 1);

                                if (trg.AllowFlipHorizontal)
                                {
                                    var flipRect = new RectangleF(matchRect.Right + 3, tc.ChipBounds.Y + 6, 18, 16);
                                    using var flipBrush = new SolidBrush(Color.FromArgb(41, 128, 185));
                                    FillRoundedRectangle(g, flipBrush, flipRect, 3);
                                    g.DrawString("⇄", fontBadge, Brushes.White, flipRect.X + 3, flipRect.Y + 1);
                                }

                                string shortFn = fn.Length > 6 ? fn[..6] + ".." : fn;
                                float labelX = trg.AllowFlipHorizontal ? matchRect.Right + 24 : matchRect.Right + 4;
                                g.DrawString(shortFn, fontChip, IsDarkMode ? Brushes.WhiteSmoke : Brushes.Black, labelX, matchRect.Y);

                                // 3. 하단: 정확도 프로그레스 게이지
                                if (tc.HasGauge)
                                {
                                    using (var trackBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(16, 20, 28) : Color.FromArgb(205, 215, 225)))
                                    {
                                        FillRoundedRectangle(g, trackBrush, tc.GaugeTrackBounds, 3);
                                    }
                                    float fillW = Math.Clamp((float)(tc.GaugeTrackBounds.Width * (trg.ImageAccuracyPercent / 100.0)), 0, tc.GaugeTrackBounds.Width);
                                    if (fillW > 0)
                                    {
                                        var fillRect = new RectangleF(tc.GaugeTrackBounds.X, tc.GaugeTrackBounds.Y, fillW, tc.GaugeTrackBounds.Height);
                                        using var fillBrush = new SolidBrush(Color.FromArgb(52, 152, 219));
                                        FillRoundedRectangle(g, fillBrush, fillRect, 3);
                                    }
                                    using (var trackBorder = new Pen(IsDarkMode ? Color.FromArgb(50, 60, 75) : Color.FromArgb(170, 185, 205), 1f))
                                    {
                                        DrawRoundedRectangle(g, trackBorder, tc.GaugeTrackBounds, 3);
                                    }
                                    g.DrawString($"{(int)trg.ImageAccuracyPercent}%", fontChipBold, IsDarkMode ? Brushes.LightSkyBlue : Brushes.DarkBlue, tc.GaugeTrackBounds.Right + 5, tc.GaugeTrackBounds.Y - 3);
                                }
                            }
                        }
                        else if (trg.Type == TriggerType.TimerInterval)
                        {
                            // 1. [타이머] 뱃지
                            var badgeRect = new RectangleF(contentX, centerY - 9, 44, 18);
                            using (var badgeBrush = new SolidBrush(Color.FromArgb(142, 68, 173)))
                            {
                                FillRoundedRectangle(g, badgeBrush, badgeRect, 3);
                            }
                            g.DrawString("⏱ 주기", fontBadge, Brushes.White, badgeRect.X + 4, badgeRect.Y + 2);
                            contentX += 50;

                            // 2. 주기 시간
                            g.DrawString($"{trg.IntervalMs} ms ({trg.IntervalMs / 1000.0:F1}s)", fontChipBold, IsDarkMode ? Brushes.Orange : Brushes.DarkOrange, contentX, centerY - 7);
                        }
                        else if (trg.Type == TriggerType.VariableCheck)
                        {
                            // 1. [변수] 뱃지
                            var badgeRect = new RectangleF(contentX, centerY - 9, 36, 18);
                            using (var badgeBrush = new SolidBrush(Color.FromArgb(211, 84, 0)))
                            {
                                FillRoundedRectangle(g, badgeBrush, badgeRect, 3);
                            }
                            g.DrawString("변수", fontBadge, Brushes.White, badgeRect.X + 6, badgeRect.Y + 2);
                            contentX += 42;

                            // 2. 수식 레터박스
                            string formula = $"{trg.VariableName} {trg.VarCompareOp} {trg.VarTargetValue}";
                            g.DrawString(formula, fontChipBold, IsDarkMode ? Brushes.LightYellow : Brushes.DarkGoldenrod, contentX, centerY - 7);
                        }
                        else
                        {
                            string summary = trg.GetSummary();
                            if (summary.Length > 28) summary = summary[..28] + "...";
                            g.DrawString($"• {summary}", fontChip, IsDarkMode ? Brushes.WhiteSmoke : Brushes.Black, contentX, centerY - 7);
                        }

                    }
                }

                // 행동 섹션 헤더
                using (var actHeaderBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(243, 156, 18) : Color.FromArgb(180, 85, 0)))
                {
                    var actSecRect = new RectangleF(el.ActionSectionBounds.X, el.ActionSectionBounds.Y + 1, el.ActionSectionBounds.Width, 18);
                    AppFontHelper.DrawMixedString(g, Localization.IsKorean ? "🔸 실행 결과" : "🔸 Action Commands", 8f * fontScale, actHeaderBrush, actSecRect, StringAlignment.Near, StringAlignment.Center, FontStyle.Bold);
                }

                // 행동 칩들 (ComfyUI / Unity 스타일 그래픽 렌더링)
                if (el.ActionChips.Count == 0)
                {
                    g.DrawString(Localization.IsKorean ? "⚠️ 행동 미등록" : "⚠️ No actions registered", fontEmpty, IsDarkMode ? Brushes.DarkGray : Brushes.Gray, el.ActionSectionBounds.X + 8, el.ActionSectionBounds.Bottom + 2);
                }
                else
                {
                    foreach (var ac in el.ActionChips)
                    {
                        var act = ac.Action;
                        using var chipBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(30, 34, 52) : Color.FromArgb(242, 238, 248));
                        FillRoundedRectangle(g, chipBrush, ac.ChipBounds, 5);

                        int actIdx = el.ActionChips.IndexOf(ac);
                        bool isExecuting = (evt.Id == ActiveActionEvtId && actIdx == ActiveActionIndex);

                        if (isExecuting)
                        {
                            using var execBg = new SolidBrush(Color.FromArgb(IsDarkMode ? 60 : 40, 243, 156, 18));
                            FillRoundedRectangle(g, execBg, ac.ChipBounds, 5);
                            using var execBorder = new Pen(Color.FromArgb(243, 156, 18), 2.2f);
                            DrawRoundedRectangle(g, execBorder, ac.ChipBounds, 5);
                        }
                        else
                        {
                            using var chipBorder = new Pen(IsDarkMode ? Color.FromArgb(54, 60, 92) : Color.FromArgb(210, 195, 225), 1f);
                            DrawRoundedRectangle(g, chipBorder, ac.ChipBounds, 5);
                        }

                        float contentX = ac.ChipBounds.X + 6;
                        float centerY = ac.ChipBounds.Y + ac.ChipBounds.Height / 2;

                        if (act.Type == ActionType.KeyPress)
                        {
                            // 1. 3D 키캡 (Keycap) 위젯
                            string keyStr = AppKeyHelper.FormatKey(act.Key);

                            var keycapSize = g.MeasureString(keyStr, fontKeycap);
                            float capWidth = Math.Max(26f, keycapSize.Width + 10f);
                            float capHeight = 18f;

                            var capShadow = new RectangleF(contentX, centerY - capHeight / 2 + 1, capWidth, capHeight);
                            using (var shadowBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(15, 20, 30) : Color.FromArgb(180, 185, 195)))
                            {
                                FillRoundedRectangle(g, shadowBrush, capShadow, 4);
                            }

                            var capFace = new RectangleF(contentX, centerY - capHeight / 2 - 1, capWidth, capHeight);
                            Color capTop = IsDarkMode ? Color.FromArgb(48, 60, 82) : Color.FromArgb(240, 242, 245);
                            Color capBottom = IsDarkMode ? Color.FromArgb(34, 44, 62) : Color.FromArgb(215, 220, 228);
                            using (var faceBrush = new LinearGradientBrush(capFace, capTop, capBottom, LinearGradientMode.Vertical))
                            {
                                FillRoundedRectangle(g, faceBrush, capFace, 4);
                            }
                            using (var capBorder = new Pen(IsDarkMode ? Color.FromArgb(120, 135, 160) : Color.FromArgb(170, 175, 185), 1f))
                            {
                                DrawRoundedRectangle(g, capBorder, capFace, 4);
                            }

                            g.DrawString(keyStr, fontKeycap, IsDarkMode ? Brushes.White : Brushes.Black, capFace.X + (capWidth - keycapSize.Width) / 2, capFace.Y + 2);
                            contentX += capWidth + 8;

                            // 2. 키 홀드 및 지연 표시
                            g.DrawString($"⌨ 클릭", fontBadge, IsDarkMode ? Brushes.White : Brushes.DimGray, contentX, centerY - 6);
                            contentX += 45;

                            if (act.PostDelayMs > 0)
                            {
                                g.DrawString($"+{act.PostDelayMs}ms", fontBadge, IsDarkMode ? Brushes.Khaki : Brushes.DarkGoldenrod, contentX, centerY - 6);
                            }
                        }
                        else if (act.Type == ActionType.MouseClick)
                        {
                            // 1. 마우스 조작 가변 뱃지
                            string btnText = AppKeyHelper.FormatMouseButtonShort(act.MouseButton, Localization.IsKorean);
                            string mouseBadgeText = $"🖱 {btnText}";
                            var btnTextSize = g.MeasureString(mouseBadgeText, fontBadge);
                            float btnWidth = Math.Max(46f, btnTextSize.Width + 8f);

                            var btnRect = new RectangleF(contentX, centerY - 9, btnWidth, 18);
                            using (var btnBg = new SolidBrush(Color.FromArgb(39, 110, 144)))
                            {
                                FillRoundedRectangle(g, btnBg, btnRect, 3);
                            }
                            g.DrawString(mouseBadgeText, fontBadge, Brushes.White, btnRect.X + 4, btnRect.Y + 2);
                            contentX += btnWidth + 6;

                            // 2. 타깃 모드별 레터박스
                            if (act.TargetMode == MouseTargetMode.Coordinates)
                            {
                                string coord = $"({act.CustomX}, {act.CustomY})";
                                g.DrawString(coord, fontChipBold, IsDarkMode ? Brushes.LightSkyBlue : Brushes.DarkBlue, contentX, centerY - 7);
                            }
                            else if (act.TargetMode == MouseTargetMode.TargetImage)
                            {
                                g.DrawString("[IMG 찾기클릭]", fontBadge, IsDarkMode ? Brushes.LightGreen : Brushes.DarkGreen, contentX, centerY - 6);
                            }
                            else
                            {
                                g.DrawString("[감지위치 클릭]", fontBadge, IsDarkMode ? Brushes.Khaki : Brushes.DarkGoldenrod, contentX, centerY - 6);
                            }
                        }
                        else if (act.Type == ActionType.ChangeState)
                        {
                            // 네온 상태 전환 뱃지
                            var badgeRect = new RectangleF(contentX, centerY - 9, ac.ChipBounds.Width - (contentX - ac.ChipBounds.X) - 26, 18);
                            using (var neonBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(35, 70, 60) : Color.FromArgb(220, 248, 235)))
                            {
                                FillRoundedRectangle(g, neonBrush, badgeRect, 4);
                            }
                            using (var neonBorder = new Pen(Color.FromArgb(46, 204, 113), 1f))
                            {
                                DrawRoundedRectangle(g, neonBorder, badgeRect, 4);
                            }
                            g.DrawString($"➔ 전이: [{act.TargetState}]", fontChipBold, IsDarkMode ? Brushes.Aquamarine : Brushes.DarkGreen, badgeRect.X + 6, badgeRect.Y + 2);
                        }
                        else if (act.Type == ActionType.Wait)
                        {
                            var badgeRect = new RectangleF(contentX, centerY - 9, 36, 18);
                            using (var delayBrush = new SolidBrush(Color.FromArgb(100, 80, 40)))
                            {
                                FillRoundedRectangle(g, delayBrush, badgeRect, 3);
                            }
                            g.DrawString("대기", fontBadge, Brushes.Gold, badgeRect.X + 6, badgeRect.Y + 2);
                            contentX += 42;

                            // 미니 슬라이더 바 (0~3000ms)
                            float trackWidth = 75f;
                            float trackHeight = 10f;
                            var trackRect = new RectangleF(contentX, centerY - trackHeight / 2, trackWidth, trackHeight);
                            using (var trackBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(15, 20, 30) : Color.FromArgb(205, 215, 225)))
                            {
                                FillRoundedRectangle(g, trackBrush, trackRect, 3);
                            }
                            float fillWidth = Math.Clamp((float)(trackWidth * (Math.Clamp(act.WaitDurationMs, 0, 3000) / 3000.0)), 0, trackWidth);
                            if (fillWidth > 0)
                            {
                                var fillRect = new RectangleF(trackRect.X, trackRect.Y, fillWidth, trackHeight);
                                using var fillBrush = new SolidBrush(Color.FromArgb(241, 196, 15));
                                FillRoundedRectangle(g, fillBrush, fillRect, 3);
                            }
                            using (var trackBorder = new Pen(IsDarkMode ? Color.FromArgb(60, 60, 50) : Color.FromArgb(170, 185, 205), 1f))
                            {
                                DrawRoundedRectangle(g, trackBorder, trackRect, 3);
                            }
                            contentX += trackWidth + 6;

                            g.DrawString($"{act.WaitDurationMs}ms", fontChipBold, IsDarkMode ? Brushes.Khaki : Brushes.DarkGoldenrod, contentX, centerY - 7);
                        }
                        else if (act.Type == ActionType.VariableControl)
                        {
                            var badgeRect = new RectangleF(contentX, centerY - 9, 36, 18);
                            using (var varBrush = new SolidBrush(Color.FromArgb(140, 70, 40)))
                            {
                                FillRoundedRectangle(g, varBrush, badgeRect, 3);
                            }
                            g.DrawString("변수", fontBadge, Brushes.White, badgeRect.X + 6, badgeRect.Y + 2);
                            contentX += 42;

                            string opStr = act.VariableOp switch
                            {
                                VariableOperator.Add => "+=",
                                VariableOperator.Subtract => "-=",
                                _ => "="
                            };
                            g.DrawString($"{act.VariableName} {opStr} {act.VariableValue}", fontChipBold, IsDarkMode ? Brushes.Orange : Brushes.DarkOrange, contentX, centerY - 7);
                        }
                        else
                        {
                            string summary = ac.Action.Description;
                            if (string.IsNullOrWhiteSpace(summary)) summary = $"{ac.Action.Type}";
                            if (summary.Length > 28) summary = summary[..28] + "...";
                            g.DrawString($"▶ {summary}", fontChip, IsDarkMode ? Brushes.WhiteSmoke : Brushes.Black, contentX, centerY - 7);
                        }

                    }
                }

                // 우측 연결 포트 핀 (Pin)
                float pinSize = 11f;
                var pinPt = el.PinPt;
                bool hasTransition = evt.Actions.Any(a => a.Type == ActionType.ChangeState);
                Color pinColor = hasTransition ? Color.FromArgb(241, 196, 15) : Color.FromArgb(149, 165, 166);

                using (var pinBrush = new SolidBrush(pinColor))
                {
                    g.FillEllipse(pinBrush, pinPt.X - pinSize / 2, pinPt.Y - pinSize / 2, pinSize, pinSize);
                }
                g.DrawEllipse(Pens.Black, pinPt.X - pinSize / 2, pinPt.Y - pinSize / 2, pinSize, pinSize);
            }

            // 이벤트가 없을 경우 빈 상태 안내
            if (layout.Events.Count == 0)
            {
                g.DrawString(Localization.IsKorean ? "💡 등록된 이벤트 없음 (더블클릭하여 추가)" : "💡 No events (Double-click to add)", fontEmpty, IsDarkMode ? Brushes.Gray : Brushes.DarkGray, nodeRect.X + 14, nodeRect.Y + 46);
            }

            // 6. 좌측 유입 포트 (Input Port)
            if (!MacroEventProfile.IsGlobalState(state))
            {
                float inPinSize = 12f;
                var inPinPt = new PointF(nodeRect.Left, nodeRect.Y + 19f);
                using var inPinBrush = new SolidBrush(Color.FromArgb(52, 152, 219));
                g.FillEllipse(inPinBrush, inPinPt.X - inPinSize / 2, inPinPt.Y - inPinSize / 2, inPinSize, inPinSize);
                g.DrawEllipse(Pens.White, inPinPt.X - inPinSize / 2, inPinPt.Y - inPinSize / 2, inPinSize, inPinSize);
            }

            // 7. 노드 외곽 테두리 (활성 펄스 - 부드러운 강도, 선택 하이라이트)
            if (isActive)
            {
                float glowWidth = 1.5f + (float)Math.Sin(_pulsePhase) * 0.4f;
                using var glowPen = new Pen(Color.FromArgb(170, 46, 204, 113), glowWidth);
                DrawRoundedRectangle(g, glowPen, nodeRect, 10);
            }
            else if (isHoveredTarget)
            {
                using var hoverPen = new Pen(Color.FromArgb(241, 196, 15), 3f) { DashStyle = DashStyle.Dash };
                DrawRoundedRectangle(g, hoverPen, nodeRect, 10);
            }
            else if (isSelected || SelectedStates.Contains(state))
            {
                using var selectPen = new Pen(IsDarkMode ? Color.FromArgb(255, 234, 167) : Color.FromArgb(52, 152, 219), 2.5f);
                DrawRoundedRectangle(g, selectPen, nodeRect, 10);
            }
            else
            {
                using var borderPen = new Pen(IsDarkMode ? Color.FromArgb(44, 60, 84) : Color.FromArgb(195, 205, 220), 1.2f);
                DrawRoundedRectangle(g, borderPen, nodeRect, 10);
            }

            // 8. 우측 하단 리사이즈 그립 표시 (점 6개)
            using (var gripBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(120, 140, 165) : Color.FromArgb(160, 175, 195)))
            {
                float gx = nodeRect.Right - 5;
                float gy = nodeRect.Bottom - 5;
                g.FillRectangle(gripBrush, gx - 2, gy - 2, 2, 2);
                g.FillRectangle(gripBrush, gx - 6, gy - 2, 2, 2);
                g.FillRectangle(gripBrush, gx - 10, gy - 2, 2, 2);
                g.FillRectangle(gripBrush, gx - 2, gy - 6, 2, 2);
                g.FillRectangle(gripBrush, gx - 6, gy - 6, 2, 2);
                g.FillRectangle(gripBrush, gx - 2, gy - 10, 2, 2);
            }

            if (customScale)
            {
                fHeader.Dispose();
                fEvtTitle.Dispose();
                fSecTitle.Dispose();
                fChip.Dispose();
                fChipBold.Dispose();
                fBadge.Dispose();
                fEmpty.Dispose();
            }
        }
    }

    private void DrawTransitions(Graphics g)
    {
        foreach (var evt in _profile.Events)
        {
            var changeStateActions = evt.Actions.Where(a => a.Type == ActionType.ChangeState && !string.IsNullOrEmpty(a.TargetState)).ToList();
            if (changeStateActions.Count == 0) continue;

            if (!_profile.NodePositions.TryGetValue(evt.StateTag, out var srcPos)) continue;

            var srcLayout = CalculateStateLayout(evt.StateTag, srcPos);
            var evLayout = srcLayout.Events.FirstOrDefault(e => e.Event == evt);
            if (evLayout == null) continue;

            var startPt = evLayout.PinPt;

            foreach (var action in changeStateActions)
            {
                string targetState = action.TargetState;
                if (!_profile.NodePositions.TryGetValue(targetState, out var dstPos)) continue;

                var dstLayout = CalculateStateLayout(targetState, dstPos);
                var endPt = new PointF(dstLayout.Bounds.Left, dstLayout.Bounds.Y + 19f);

                bool isHighlighted = (SelectedEvent == evt || SelectedState == evt.StateTag);
                bool isActive = (_activeState == evt.StateTag);

                Color edgeColor = isActive
                    ? Color.FromArgb(46, 204, 113)
                    : (isHighlighted ? Color.FromArgb(241, 196, 15) : Color.FromArgb(120, 150, 180));

                float lineWidth = (isHighlighted || isActive) ? 2.5f : 1.6f;
                DrawBezierCurveWithArrow(g, startPt, endPt, edgeColor, lineWidth, evt.Name);
            }
        }
    }

    private void DrawConnectingCurve(Graphics g)
    {
        Color curveColor = Color.FromArgb(241, 196, 15);
        using var dashedPen = new Pen(curveColor, 2f) { DashStyle = DashStyle.Dash };

        float dx = Math.Abs(_connectingCurrentWorldPt.X - _connectingStartPt.X) * 0.5f;
        dx = Math.Max(40f, dx);

        var cp1 = new PointF(_connectingStartPt.X + dx, _connectingStartPt.Y);
        var cp2 = new PointF(_connectingCurrentWorldPt.X - dx, _connectingCurrentWorldPt.Y);

        g.DrawBezier(dashedPen, _connectingStartPt, cp1, cp2, _connectingCurrentWorldPt);
        g.FillEllipse(Brushes.Gold, _connectingCurrentWorldPt.X - 5, _connectingCurrentWorldPt.Y - 5, 10, 10);
    }

    private void DrawBezierCurveWithArrow(Graphics g, PointF start, PointF end, Color color, float width, string label)
    {
        float dx = Math.Abs(end.X - start.X) * 0.5f;
        dx = Math.Max(50f, dx);

        var cp1 = new PointF(start.X + dx, start.Y);
        var cp2 = new PointF(end.X - dx, end.Y);

        using var pen = new Pen(color, width);
        g.DrawBezier(pen, start, cp1, cp2, end);
        DrawArrowhead(g, cp2, end, color, 8f);

        PointF midPt = CalculateBezierPoint(0.5f, start, cp1, cp2, end);
        using var labelFont = AppFontHelper.GetEmojiFont(7.5f, FontStyle.Bold);
        var textSz = g.MeasureString(label, labelFont);
        var pillRect = new RectangleF(midPt.X - textSz.Width / 2 - 4, midPt.Y - textSz.Height / 2 - 2, textSz.Width + 8, textSz.Height + 4);

        using var pillBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(220, 15, 20, 28) : Color.FromArgb(220, 255, 255, 255));
        using var pillBorder = new Pen(color, 1f);
        FillRoundedRectangle(g, pillBrush, pillRect, 4);
        DrawRoundedRectangle(g, pillBorder, pillRect, 4);
        g.DrawString(label, labelFont, IsDarkMode ? Brushes.White : Brushes.Black, pillRect.X + 4, pillRect.Y + 2);
    }

    private void DrawArrowhead(Graphics g, PointF from, PointF to, Color color, float size)
    {
        float angle = (float)Math.Atan2(to.Y - from.Y, to.X - from.X);
        var pt1 = new PointF(to.X - size * (float)Math.Cos(angle - Math.PI / 6), to.Y - size * (float)Math.Sin(angle - Math.PI / 6));
        var pt2 = new PointF(to.X - size * (float)Math.Cos(angle + Math.PI / 6), to.Y - size * (float)Math.Sin(angle + Math.PI / 6));

        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, new PointF[] { to, pt1, pt2 });
    }

    private PointF CalculateBezierPoint(float t, PointF p0, PointF p1, PointF p2, PointF p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        return new PointF(
            uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X,
            uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y
        );
    }

    private void DrawHudOverlay(Graphics g)
    {
        string hudText = Localization.IsKorean
            ? $"Zoom: {(Zoom < 0.1f ? (Zoom * 100).ToString("0.0") : ((int)(Zoom * 100)).ToString())}% | [칩 더블클릭]: 상세 편집 | [휠 드래그]: 이동 | [휠]: 줌"
            : $"Zoom: {(Zoom < 0.1f ? (Zoom * 100).ToString("0.0") : ((int)(Zoom * 100)).ToString())}% | [Double-click Chip]: Edit | [Wheel Drag]: Pan | [Wheel]: Zoom";
        using var font = AppFontHelper.GetDefaultFont(9f, FontStyle.Regular);
        var sz = g.MeasureString(hudText, font);
        var rect = new RectangleF(Width - sz.Width - 24, 12, sz.Width + 16, 26);

        using var bg = new SolidBrush(IsDarkMode ? Color.FromArgb(210, 15, 20, 28) : Color.FromArgb(210, 255, 255, 255));
        using var border = new Pen(IsDarkMode ? Color.FromArgb(44, 60, 84) : Color.FromArgb(180, 195, 215));
        FillRoundedRectangle(g, bg, rect, 6);
        DrawRoundedRectangle(g, border, rect, 6);
        g.DrawString(hudText, font, IsDarkMode ? Brushes.White : Brushes.Black, rect.X + 8, rect.Y + 5);
    }


    private Image? GetThumbnail(string? base64, string? filePath)
    {
        string? key = !string.IsNullOrEmpty(base64) ? base64 : filePath;
        if (string.IsNullOrEmpty(key)) return null;

        if (_thumbnailCache.TryGetValue(key, out var cached)) return cached;

        try
        {
            if (!string.IsNullOrEmpty(base64))
            {
                byte[] bytes = Convert.FromBase64String(base64);
                using var ms = new System.IO.MemoryStream(bytes);
                var img = Image.FromStream(ms);
                _thumbnailCache[key] = img;
                return img;
            }
            if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
            {
                var img = Image.FromFile(filePath);
                _thumbnailCache[key] = img;
                return img;
            }
        }
        catch { }
        return null;
    }

    private void DrawLetterboxedImage(Graphics g, Image? img, RectangleF bounds, string fallbackText = "")
    {
        using var bgBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(14, 18, 26) : Color.FromArgb(220, 228, 238));
        FillRoundedRectangle(g, bgBrush, bounds, 4);
        using var borderPen = new Pen(IsDarkMode ? Color.FromArgb(48, 64, 90) : Color.FromArgb(185, 200, 220), 1f);
        DrawRoundedRectangle(g, borderPen, bounds, 4);

        if (img != null && img.Width > 0 && img.Height > 0)
        {
            float maxW = bounds.Width - 4;
            float maxH = bounds.Height - 4;
            float scale = Math.Min(maxW / img.Width, maxH / img.Height);
            float drawW = img.Width * scale;
            float drawH = img.Height * scale;
            float drawX = bounds.X + (bounds.Width - drawW) / 2f;
            float drawY = bounds.Y + (bounds.Height - drawH) / 2f;

            var oldInterpolation = g.InterpolationMode;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            g.DrawImage(img, new RectangleF(drawX, drawY, drawW, drawH));
            g.InterpolationMode = oldInterpolation;
        }
        else
        {
            using var fontMini = AppFontHelper.GetDefaultFont(7.5f);
            using var textBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(140, 160, 185) : Color.FromArgb(90, 110, 135));
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(string.IsNullOrEmpty(fallbackText) ? "IMG" : fallbackText, fontMini, textBrush, bounds, sf);
        }
    }

}

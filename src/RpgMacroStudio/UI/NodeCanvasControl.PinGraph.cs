using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

public partial class NodeCanvasControl
{
    private GraphNode? _draggingGraphNode;
    private PointF _dragNodeMouseOffset;
    private PointF _dragNodeStartPt;
    private readonly Dictionary<string, PointF> _multiDragGraphNodeOffsets = new();
    private GraphNode? _resizingGraphNode;
    private SizeF _resizeStartGraphSize;
    private PointF _resizeStartGraphMousePt;

    /// <summary>
    /// 첫 실행 시에만 시작 노드의 Flow-Out 핀을 하이라이트로 표시할지 여부
    /// </summary>
    public bool HighlightStartPinOnFirstRun { get; set; } = false;

    public PointF GetPinWorldPosition(GraphNode node, GraphPin pin)
    {
        float baseW = (node.NodeType == GraphNodeType.Start) ? 200f : (node.NodeType == GraphNodeType.Condition ? 250f : 220f);
        float nodeScale = Math.Clamp(node.Width / baseW, 0.7f, 3.0f);
        float fontScale = Math.Clamp((float)Math.Pow(nodeScale, 0.65), 0.85f, 2.2f);
        float headerH = Math.Max(28f, 28f * fontScale);
        float topOffset = headerH + (string.IsNullOrEmpty(node.Description) ? 6f * fontScale : 22f * fontScale);
        float bodyHeight = Math.Max(28f * fontScale, node.Height - topOffset - 6f * fontScale);

        if (pin.Direction == GraphPinDirection.Input)
        {
            int index = node.InputPins.IndexOf(pin);
            if (index < 0) index = 0;
            int count = Math.Max(1, node.InputPins.Count);
            float step = bodyHeight / count;
            float y = node.Y + topOffset + (index + 0.5f) * step;
            return new PointF(node.X, y);
        }
        else
        {
            int index = node.OutputPins.IndexOf(pin);
            if (index < 0) index = 0;
            int count = Math.Max(1, node.OutputPins.Count);
            float step = bodyHeight / count;
            float y = node.Y + topOffset + (index + 0.5f) * step;
            return new PointF(node.X + node.Width, y);
        }
    }

    public bool HitTestPinGraphResizeGrip(PointF worldPt, out GraphNode? foundNode)
    {
        foundNode = null;
        for (int i = PinGraph.Nodes.Count - 1; i >= 0; i--)
        {
            var node = PinGraph.Nodes[i];
            var gripRect = new RectangleF(node.X + node.Width - 14, node.Y + node.Height - 14, 14, 14);
            if (gripRect.Contains(worldPt))
            {
                foundNode = node;
                return true;
            }
        }
        return false;
    }

    public GraphPin? HitTestPin(PointF worldPt, out GraphNode? foundNode)
    {
        foundNode = null;
        float hitRadius = 14f; // 핀 클릭 감지 반경

        foreach (var node in PinGraph.Nodes)
        {
            foreach (var pin in node.InputPins.Concat(node.OutputPins))
            {
                var pinPos = GetPinWorldPosition(node, pin);
                float dx = worldPt.X - pinPos.X;
                float dy = worldPt.Y - pinPos.Y;
                if (dx * dx + dy * dy <= hitRadius * hitRadius)
                {
                    foundNode = node;
                    return pin;
                }
            }
        }

        return null;
    }

    public GraphNode? HitTestNode(PointF worldPt)
    {
        for (int i = PinGraph.Nodes.Count - 1; i >= 0; i--)
        {
            var node = PinGraph.Nodes[i];
            if (worldPt.X >= node.X && worldPt.X <= node.X + node.Width &&
                worldPt.Y >= node.Y && worldPt.Y <= node.Y + node.Height)
            {
                return node;
            }
        }
        return null;
    }

    public void DrawPinGraphWires(Graphics g)
    {
        foreach (var conn in PinGraph.Connections)
        {
            var fromNode = PinGraph.FindNode(conn.FromNodeId);
            var toNode = PinGraph.FindNode(conn.ToNodeId);
            if (fromNode == null || toNode == null) continue;

            var fromPin = fromNode.FindPin(conn.FromPinId);
            var toPin = toNode.FindPin(conn.ToPinId);
            if (fromPin == null || toPin == null) continue;

            var p1 = GetPinWorldPosition(fromNode, fromPin);
            var p2 = GetPinWorldPosition(toNode, toPin);

            // 베지어 제어점 계산
            float dx = Math.Max(45f, Math.Abs(p2.X - p1.X) * 0.5f);
            var cp1 = new PointF(p1.X + dx, p1.Y);
            var cp2 = new PointF(p2.X - dx, p2.Y);

            // 핀 종류에 따른 색상 결정
            Color wireColor;
            if (fromPin.IsBranchPin)
            {
                wireColor = fromPin.BranchCondition
                    ? Color.FromArgb(46, 204, 113)  // True / 성공 (생생한 에메랄드)
                    : Color.FromArgb(231, 76, 60);  // False / Else (비비드 코랄 레드)
            }
            else if (fromPin.Kind == GraphPinKind.Data)
            {
                wireColor = Color.FromArgb(52, 152, 219); // Data 전달선 (스카이 블루)
            }
            else
            {
                wireColor = IsDarkMode ? Color.FromArgb(220, 225, 235) : Color.FromArgb(80, 95, 115);
            }

            // 그림자 / 외곽 글로우 효과
            using (var glowPen = new Pen(Color.FromArgb(IsDarkMode ? 40 : 25, wireColor), 5.5f))
            {
                g.DrawBezier(glowPen, p1, cp1, cp2, p2);
            }

            // 본체 와이어
            using (var wirePen = new Pen(wireColor, 2.5f))
            {
                g.DrawBezier(wirePen, p1, cp1, cp2, p2);
            }

            // 실행 중 펄스 효과 (살아 움직이는 데이터 파티클)
            if (!string.IsNullOrEmpty(_activeState) && fromNode.StateTag == _activeState)
            {
                float t = (_pulsePhase / (float)(Math.PI * 2));
                // 베지어 곡선 위의 점 계산
                float u = 1f - t;
                float tt = t * t;
                float uu = u * u;
                float uuu = uu * u;
                float ttt = tt * t;

                float px = uuu * p1.X + 3 * uu * t * cp1.X + 3 * u * tt * cp2.X + ttt * p2.X;
                float py = uuu * p1.Y + 3 * uu * t * cp1.Y + 3 * u * tt * cp2.Y + ttt * p2.Y;

                using var pulseBrush = new SolidBrush(Color.White);
                g.FillEllipse(pulseBrush, px - 4, py - 4, 8, 8);
            }
        }
    }

    public void DrawPinDraggingCurve(Graphics g)
    {
        if (DraggingSourceNode == null || DraggingSourcePin == null) return;
        var p1 = GetPinWorldPosition(DraggingSourceNode, DraggingSourcePin);
        var p2 = DraggingCurrentWorldPt;

        // 마그네틱 자석 스냅 효과: 호환 가능한 핀 위에 마우스가 올라간 경우 해당 핀의 중심점으로 끝점 자동 흡착
        bool isSnapped = false;
        if (HoveredPin != null)
        {
            var hNode = PinGraph.Nodes.FirstOrDefault(n => n.FindPin(HoveredPin.Id) != null);
            if (hNode != null && PinGraph.CanConnect(DraggingSourceNode.Id, DraggingSourcePin.Id, hNode.Id, HoveredPin.Id))
            {
                p2 = GetPinWorldPosition(hNode, HoveredPin);
                isSnapped = true;
            }
        }

        float dx = Math.Max(40f, Math.Abs(p2.X - p1.X) * 0.45f);
        var cp1 = new PointF(p1.X + (DraggingSourcePin.Direction == GraphPinDirection.Output ? dx : -dx), p1.Y);
        var cp2 = new PointF(p2.X + (DraggingSourcePin.Direction == GraphPinDirection.Output ? -dx : dx), p2.Y);

        Color dragColor = DraggingSourcePin.IsBranchPin
            ? (DraggingSourcePin.BranchCondition ? Color.FromArgb(46, 204, 113) : Color.FromArgb(231, 76, 60))
            : (DraggingSourcePin.Kind == GraphPinKind.Data ? Color.FromArgb(52, 152, 219) : Color.FromArgb(241, 196, 15));

        // 자석 흡착 시 와이어 외부 발광 효과
        if (isSnapped)
        {
            using var glowPen = new Pen(Color.FromArgb(90, dragColor), 6f);
            g.DrawBezier(glowPen, p1, cp1, cp2, p2);
        }

        using var dragPen = new Pen(dragColor, isSnapped ? 3.2f : 2.5f) { DashStyle = isSnapped ? DashStyle.Solid : DashStyle.Dash };
        g.DrawBezier(dragPen, p1, cp1, cp2, p2);
    }

    public void DrawPinGraphNodes(Graphics g)
    {
        using var fontHeader = AppFontHelper.GetEmojiFont(9.5f, FontStyle.Bold);
        using var fontBody = AppFontHelper.GetEmojiFont(8.5f, FontStyle.Regular);
        using var fontPin = AppFontHelper.GetEmojiFont(8f, FontStyle.Bold);

        foreach (var node in PinGraph.Nodes)
        {
            var nodeRect = new RectangleF(node.X, node.Y, node.Width, node.Height);
            bool isSelected = (node.Id == SelectedGraphNodeId || SelectedGraphNodeIds.Contains(node.Id));
            bool isEnabled = node.IsEnabled;

            // 노드 크기에 따른 일관된 폰트 스케일링 (FSM 모드와 완전 동일한 공식: Pow(scale, 0.65))
            float baseW = (node.NodeType == GraphNodeType.Start) ? 200f : (node.NodeType == GraphNodeType.Condition ? 250f : 220f);
            float nodeScale = Math.Clamp(node.Width / baseW, 0.7f, 3.0f);
            float fontScale = Math.Clamp((float)Math.Pow(nodeScale, 0.65), 0.85f, 2.2f);
            bool customScale = Math.Abs(fontScale - 1.0f) > 0.03f;

            float headerH = Math.Max(28f, 28f * fontScale);
            var headerRect = new RectangleF(node.X, node.Y, node.Width, headerH);

            Font fH = customScale ? AppFontHelper.GetEmojiFont(9.5f * fontScale, FontStyle.Bold) : fontHeader;
            Font fB = customScale ? AppFontHelper.GetEmojiFont(8.5f * fontScale, FontStyle.Regular) : fontBody;
            Font fP = customScale ? AppFontHelper.GetEmojiFont(8f * fontScale, FontStyle.Bold) : fontPin;
            Font fBadge = customScale ? AppFontHelper.GetEmojiFont(7f * fontScale, FontStyle.Bold) : fontPin;

            // 1. 그림자
            var shadowRect = new RectangleF(nodeRect.X + 3, nodeRect.Y + 5, nodeRect.Width, nodeRect.Height);
            using (var shadowBrush = new SolidBrush(Color.FromArgb(IsDarkMode ? 70 : 30, 0, 0, 0)))
            {
                FillRoundedRectangle(g, shadowBrush, shadowRect, 8);
            }

            // 2. 바디 배경 (비활성화 시 회색 타일)
            Color bodyBg = !isEnabled
                ? (IsDarkMode ? Color.FromArgb(28, 32, 40) : Color.FromArgb(226, 230, 236))
                : (isSelected
                    ? (IsDarkMode ? Color.FromArgb(25, 35, 52) : Color.FromArgb(235, 245, 255))
                    : (IsDarkMode ? Color.FromArgb(18, 24, 34) : Color.White));

            using (var bodyBrush = new SolidBrush(bodyBg))
            {
                FillRoundedRectangle(g, bodyBrush, nodeRect, 8);
            }

            // 3. 헤더 (비활성화 시 회색 헤더)
            Color hColor1, hColor2;
            if (!isEnabled)
            {
                hColor1 = IsDarkMode ? Color.FromArgb(50, 56, 66) : Color.FromArgb(175, 180, 190);
                hColor2 = IsDarkMode ? Color.FromArgb(40, 45, 54) : Color.FromArgb(155, 160, 170);
            }
            else
            {
                switch (node.NodeType)
                {
                    case GraphNodeType.Start:
                        hColor1 = Color.FromArgb(0, 184, 148);
                        hColor2 = Color.FromArgb(39, 174, 96);
                        break;
                    case GraphNodeType.Condition:
                        hColor1 = Color.FromArgb(108, 92, 231);
                        hColor2 = Color.FromArgb(142, 68, 173);
                        break;
                    case GraphNodeType.ChangeState:
                        hColor1 = Color.FromArgb(230, 126, 34);
                        hColor2 = Color.FromArgb(211, 84, 0);
                        break;
                    case GraphNodeType.VariableControl:
                        hColor1 = Color.FromArgb(0, 206, 201);
                        hColor2 = Color.FromArgb(9, 132, 227);
                        break;
                    case GraphNodeType.KeySequence:
                        hColor1 = Color.FromArgb(70, 80, 160);
                        hColor2 = Color.FromArgb(45, 52, 110);
                        break;
                    default:
                        hColor1 = Color.FromArgb(9, 132, 227);
                        hColor2 = Color.FromArgb(41, 128, 185);
                        break;
                }
            }

            using (var headerBrush = new LinearGradientBrush(headerRect, hColor1, hColor2, LinearGradientMode.Horizontal))
            {
                FillRoundedHeader(g, headerBrush, headerRect, 8);
            }

            // KeySequence 노드 헤더 접기/펼치기 버튼 렌더링
            if (node.NodeType == GraphNodeType.KeySequence)
            {
                float btnW = 58f * fontScale;
                float btnH = 18f * fontScale;
                var foldBtnRect = new RectangleF(headerRect.Right - btnW - 8f * fontScale, headerRect.Y + (headerRect.Height - btnH) / 2f, btnW, btnH);
                using var btnBg = new SolidBrush(Color.FromArgb(130, 0, 0, 0));
                FillRoundedRectangle(g, btnBg, foldBtnRect, 3);
                using var btnBorder = new Pen(Color.FromArgb(180, 255, 255, 255), 1f);
                DrawRoundedRectangle(g, btnBorder, foldBtnRect, 3);
                string btnTxt = node.IsCollapsed ? "▶ 펼치기" : "▼ 접기";
                AppFontHelper.DrawMixedString(g, btnTxt, 7.5f * fontScale, Brushes.White, foldBtnRect, StringAlignment.Center, StringAlignment.Center, FontStyle.Bold);
            }

            // 헤더 텍스트 (이모지는 Segoe UI Emoji Regular, 텍스트는 Segoe UI Bold로 선명하게 합성 렌더링)
            float textPadRight = (node.NodeType == GraphNodeType.KeySequence) ? 72f * fontScale : 24f * fontScale;
            var textRect = new RectangleF(headerRect.X + 12 * fontScale, headerRect.Y, headerRect.Width - textPadRight, headerRect.Height);
            Brush hTextBrush = !isEnabled ? (IsDarkMode ? Brushes.LightGray : Brushes.DimGray) : Brushes.White;
            AppFontHelper.DrawMixedString(g, node.Title, 10f * fontScale, hTextBrush, textRect, StringAlignment.Near, StringAlignment.Center, FontStyle.Bold);

            // 본문 좌/상단 안전 여백 (헤더 및 테두리와 겹치지 않도록 충분한 간격 확보)
            float bodyPadLeft = 14f * fontScale;
            float bodyPadTop = headerRect.Bottom + 5f * fontScale;

            // 바디 텍스트 / 설명 (이모지 및 특수기호 안전 분리 렌더링)
            if (!string.IsNullOrEmpty(node.Description))
            {
                Color descCol = !isEnabled
                    ? (IsDarkMode ? Color.FromArgb(120, 125, 135) : Color.FromArgb(140, 145, 155))
                    : (IsDarkMode ? Color.FromArgb(170, 185, 205) : Color.FromArgb(100, 110, 125));
                using var descBrush = new SolidBrush(descCol);
                var descRect = new RectangleF(node.X + bodyPadLeft, bodyPadTop, node.Width - bodyPadLeft - 10f, 18f * fontScale);
                AppFontHelper.DrawMixedString(g, node.Description, 8.5f * fontScale, descBrush, descRect, StringAlignment.Near, StringAlignment.Center, FontStyle.Regular);
            }

            float graphicTop = bodyPadTop + (string.IsNullOrEmpty(node.Description) ? 0 : 20f * fontScale);

            // 조건 노드 / 액션 노드 상세 시각화 그래픽
            if (node.NodeType == GraphNodeType.Condition && node.TriggerData != null)
            {
                var trg = node.TriggerData;
                if (trg.Type == TriggerType.VisionDetection || trg.Type == TriggerType.GaugeBar)
                {
                    if (trg.VisionTarget == VisionTargetType.Color || trg.Type == TriggerType.GaugeBar)
                    {
                        Color swatchCol = Color.FromArgb(231, 76, 60);
                        try { if (!string.IsNullOrEmpty(trg.CustomColorHex)) swatchCol = ColorTranslator.FromHtml(trg.CustomColorHex); } catch { }
                        var swatchRect = new RectangleF(node.X + bodyPadLeft, graphicTop, 22f * fontScale, 22f * fontScale);
                        using var sb = new SolidBrush(swatchCol);
                        FillRoundedRectangle(g, sb, swatchRect, 4);
                        using var sp = new Pen(Color.WhiteSmoke, 1f);
                        DrawRoundedRectangle(g, sp, swatchRect, 4);

                        // 듀얼 게이지 바 (우측 핀 텍스트 라벨과 겹치지 않도록 안전 여백 확보)
                        float barX = swatchRect.Right + 6f * fontScale;
                        float barW = Math.Clamp(node.Width - 145f, 40f, 120f) * fontScale;
                        var g1 = new RectangleF(barX, graphicTop + 1f, barW, 6f * fontScale);
                        using var tb = new SolidBrush(IsDarkMode ? Color.FromArgb(16, 22, 32) : Color.FromArgb(210, 220, 230));
                        FillRoundedRectangle(g, tb, g1, 2);
                        float fw1 = Math.Clamp((float)(barW * (trg.ColorAccuracyPercent / 100.0)), 0, barW);
                        if (fw1 > 0)
                        {
                            using var fb1 = new SolidBrush(Color.FromArgb(52, 152, 219));
                            FillRoundedRectangle(g, fb1, new RectangleF(barX, graphicTop + 1f, fw1, 6f * fontScale), 2);
                        }
                        g.DrawString($"정확 {(int)trg.ColorAccuracyPercent}%", fBadge, IsDarkMode ? Brushes.LightSkyBlue : Brushes.DarkBlue, g1.Right + 3, g1.Y - 2);

                        var g2 = new RectangleF(barX, graphicTop + 12f * fontScale, barW, 6f * fontScale);
                        FillRoundedRectangle(g, tb, g2, 2);
                        float fw2 = Math.Clamp((float)(barW * (trg.GaugeThresholdPercent / 100.0)), 0, barW);
                        if (fw2 > 0)
                        {
                            using var fb2 = new SolidBrush(Color.FromArgb(46, 204, 113));
                            FillRoundedRectangle(g, fb2, new RectangleF(barX, graphicTop + 12f * fontScale, fw2, 6f * fontScale), 2);
                        }
                        string opStr = trg.ComparisonOp switch
                        {
                            ComparisonOperator.LessThan => "<",
                            ComparisonOperator.GreaterThan => ">",
                            ComparisonOperator.GreaterThanOrEqual => ">=",
                            _ => "<="
                        };
                        g.DrawString($"잔여 {(int)trg.GaugeThresholdPercent}%{opStr}", fBadge, IsDarkMode ? Brushes.LightGreen : Brushes.DarkGreen, g2.Right + 3, g2.Y - 2);
                    }
                }
            }
            else if (node.NodeType == GraphNodeType.Action && node.ActionData != null)
            {
                var act = node.ActionData;
                if (act.Type == ActionType.KeyPress)
                {
                    // 좌측 핀 라벨(In ▶)과 겹치지 않도록 node.X + 44 최소 오프셋 및 스케일링 여백 확보
                    float keycapX = Math.Max(node.X + 44, node.X + bodyPadLeft + 24f * fontScale);
                    var keycapRect = new RectangleF(keycapX, graphicTop, 44f * fontScale, 20f * fontScale);
                    using var kcb = new SolidBrush(IsDarkMode ? Color.FromArgb(45, 55, 72) : Color.FromArgb(220, 228, 238));
                    FillRoundedRectangle(g, kcb, keycapRect, 4);
                    using var kcp = new Pen(IsDarkMode ? Color.FromArgb(80, 95, 120) : Color.FromArgb(170, 185, 200), 1.2f);
                    DrawRoundedRectangle(g, kcp, keycapRect, 4);
                    using var sfK = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(act.Key, fH, IsDarkMode ? Brushes.White : Brushes.Black, keycapRect, sfK);
                }
                else if (act.Type == ActionType.MouseClick)
                {
                    float mouseBadgeX = Math.Max(node.X + 44, node.X + bodyPadLeft + 24f * fontScale);
                    var mouseBadgeRect = new RectangleF(mouseBadgeX, graphicTop, 50f * fontScale, 20f * fontScale);
                    using var mb = new SolidBrush(Color.FromArgb(30, 80, 110));
                    FillRoundedRectangle(g, mb, mouseBadgeRect, 4);
                    using var sfM = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(act.MouseButton.ToString(), fBadge, Brushes.White, mouseBadgeRect, sfM);
                }
            }
            else if (node.NodeType == GraphNodeType.KeySequence)
            {
                if (!node.IsCollapsed && node.SequenceActions != null)
                {
                    float rowY = graphicTop;
                    for (int sIdx = 0; sIdx < node.SequenceActions.Count; sIdx++)
                    {
                        var sAct = node.SequenceActions[sIdx];
                        var rowRect = new RectangleF(node.X + bodyPadLeft + 4f * fontScale, rowY, node.Width - bodyPadLeft - 18f * fontScale, 18f * fontScale);
                        
                        using var rowBg = new SolidBrush(IsDarkMode ? Color.FromArgb(32, 40, 56) : Color.FromArgb(235, 240, 248));
                        FillRoundedRectangle(g, rowBg, rowRect, 3);

                        string rowIcon = sAct.Type == ActionType.Wait 
                            ? "⏳" 
                            : (sAct.KeyMode == KeyStrokeMode.KeyDown ? "⬇️" : (sAct.KeyMode == KeyStrokeMode.KeyUp ? "⬆️" : "⌨️"));
                        string rowText = sAct.Type == ActionType.Wait 
                            ? $"대기 {sAct.WaitDurationMs}ms" 
                            : $"{sAct.Key} ({(sAct.KeyMode == KeyStrokeMode.KeyDownUp ? "누르고 뗌" : (sAct.KeyMode == KeyStrokeMode.KeyDown ? "누름" : "손 뗌"))})";

                        AppFontHelper.DrawMixedString(g, $"{rowIcon} {rowText}", 8f * fontScale, IsDarkMode ? Brushes.WhiteSmoke : Brushes.DarkSlateGray, rowRect, StringAlignment.Near, StringAlignment.Center, FontStyle.Regular);
                        rowY += 21f * fontScale;
                    }
                }
            }

            if (customScale)
            {
                fH.Dispose();
                fB.Dispose();
                fP.Dispose();
                fBadge.Dispose();
            }

            // 4. 입력 핀 (좌측)
            foreach (var pin in node.InputPins)
            {
                var pinPos = GetPinWorldPosition(node, pin);
                DrawPinShape(g, pinPos, pin, true, fontPin, node);
            }

            // 5. 출력 핀 (우측)
            foreach (var pin in node.OutputPins)
            {
                var pinPos = GetPinWorldPosition(node, pin);
                DrawPinShape(g, pinPos, pin, false, fontPin, node);
            }

            // 6. 선택 시 테두리 하이라이트
            Color borderCol = isSelected
                ? Color.FromArgb(52, 152, 219)
                : (IsDarkMode ? Color.FromArgb(45, 58, 78) : Color.FromArgb(205, 215, 228));
            float borderWidth = isSelected ? 2.2f : 1f;

            using (var borderPen = new Pen(borderCol, borderWidth))
            {
                DrawRoundedRectangle(g, borderPen, nodeRect, 8);
            }

            // 7. 우측 하단 리사이즈 그립 표시 (점 6개)
            using (var gripBrush = new SolidBrush(IsDarkMode ? Color.FromArgb(120, 140, 165) : Color.FromArgb(160, 175, 195)))
            {
                float gx = nodeRect.Right - 4;
                float gy = nodeRect.Bottom - 4;
                g.FillRectangle(gripBrush, gx - 2, gy - 2, 2, 2);
                g.FillRectangle(gripBrush, gx - 6, gy - 2, 2, 2);
                g.FillRectangle(gripBrush, gx - 10, gy - 2, 2, 2);
                g.FillRectangle(gripBrush, gx - 2, gy - 6, 2, 2);
                g.FillRectangle(gripBrush, gx - 6, gy - 6, 2, 2);
                g.FillRectangle(gripBrush, gx - 2, gy - 10, 2, 2);
            }
        }
    }

    private void DrawPinShape(Graphics g, PointF pinPos, GraphPin pin, bool isInput, Font font, GraphNode node)
    {
        float r = 5.5f;
        Color pinColor;

        if (pin.IsBranchPin)
        {
            pinColor = pin.BranchCondition ? Color.FromArgb(46, 204, 113) : Color.FromArgb(231, 76, 60);
        }
        else if (pin.Kind == GraphPinKind.Data)
        {
            pinColor = Color.FromArgb(52, 152, 219);
        }
        else
        {
            pinColor = Color.White;
        }

        bool isStartPinHighlight = HighlightStartPinOnFirstRun && node.NodeType == GraphNodeType.Start && pin.Direction == GraphPinDirection.Output;

        // 와이어 드래그 중인 핀과의 호환성 검사 및 테두리 펄스/네온 하이라이트
        bool isCompatible = false;
        if (IsPinDragging && DraggingSourceNode != null && DraggingSourcePin != null)
        {
            isCompatible = PinGraph.CanConnect(DraggingSourceNode.Id, DraggingSourcePin.Id, node.Id, pin.Id);
        }

        bool isHovered = (HoveredPin == pin && isCompatible);

        if (isCompatible || isStartPinHighlight)
        {
            float pulseVal = (float)(Math.Sin(_pulsePhase) * 0.5f + 0.5f);
            Color neonColor = isStartPinHighlight
                ? Color.FromArgb(241, 196, 15) // 첫 실행 시 시작 노드 골드 네온 펄스 아우라
                : (pin.IsBranchPin
                    ? (pin.BranchCondition ? Color.FromArgb(46, 235, 120) : Color.FromArgb(255, 80, 80))
                    : (pin.Kind == GraphPinKind.Data ? Color.FromArgb(60, 180, 255) : Color.FromArgb(255, 215, 60)));

            float auraRadius = r + (isStartPinHighlight ? 7.5f : (isHovered ? 7.5f : 4.5f)) + pulseVal * (isHovered ? 4.0f : 2.5f);
            int auraAlpha = isStartPinHighlight ? (140 + (int)(pulseVal * 80)) : (isHovered ? 130 : (45 + (int)(pulseVal * 45)));

            // 1. 은은한 네온 컬러 외부 발광 아우라 (Glow Aura)
            using (var auraBrush = new SolidBrush(Color.FromArgb(auraAlpha, neonColor)))
            {
                g.FillEllipse(auraBrush, pinPos.X - auraRadius, pinPos.Y - auraRadius, auraRadius * 2, auraRadius * 2);
            }

            // 2. 테두리 펄스 네온 링 (Pulse Ring)
            float ringRadius = r + (isStartPinHighlight ? 4.5f : (isHovered ? 4.5f : 2.5f)) + pulseVal * 1.0f;
            int ringAlpha = isStartPinHighlight ? 255 : (isHovered ? 255 : (150 + (int)(pulseVal * 85)));
            using (var ringPen = new Pen(Color.FromArgb(ringAlpha, neonColor), (isHovered || isStartPinHighlight) ? 2.5f : 1.6f))
            {
                g.DrawEllipse(ringPen, pinPos.X - ringRadius, pinPos.Y - ringRadius, ringRadius * 2, ringRadius * 2);
            }

            // 3. 마그네틱 자석 스냅 십자선 가이드
            if (isHovered)
            {
                using var snapPen = new Pen(Color.White, 1.5f);
                g.DrawLine(snapPen, pinPos.X - r - 4, pinPos.Y, pinPos.X - r - 1, pinPos.Y);
                g.DrawLine(snapPen, pinPos.X + r + 1, pinPos.Y, pinPos.X + r + 4, pinPos.Y);
                r = 6.5f; // 자석 흡착 시 핀 크기 살짝 확대
            }
        }

        // 핀 몸체 그리기
        if (pin.Kind == GraphPinKind.ExecutionFlow)
        {
            // Flow: 세련된 오른쪽 화살표/오각형 핀
            PointF[] pts = isInput
                ? new PointF[] { new(pinPos.X - r, pinPos.Y - r), new(pinPos.X + 2, pinPos.Y), new(pinPos.X - r, pinPos.Y + r) }
                : new PointF[] { new(pinPos.X - 2, pinPos.Y - r), new(pinPos.X + r, pinPos.Y), new(pinPos.X - 2, pinPos.Y + r) };

            using var brush = new SolidBrush(isHovered ? Color.White : pinColor);
            g.FillPolygon(brush, pts);
            using var pen = new Pen(isCompatible ? (isHovered ? Color.White : Color.FromArgb(200, 255, 255, 255)) : Color.FromArgb(40, 40, 40), isCompatible ? 1.5f : 1f);
            g.DrawPolygon(pen, pts);
        }
        else
        {
            // Data / Branch: 원형 핀
            using var brush = new SolidBrush(isHovered ? Color.White : pinColor);
            g.FillEllipse(brush, pinPos.X - r, pinPos.Y - r, r * 2, r * 2);
            using var pen = new Pen(isCompatible ? (isHovered ? Color.White : Color.FromArgb(200, 255, 255, 255)) : Color.FromArgb(40, 40, 40), isCompatible ? 1.5f : 1f);
            g.DrawEllipse(pen, pinPos.X - r, pinPos.Y - r, r * 2, r * 2);
        }

        // 핀 텍스트 라벨 (호환 핀은 텍스트도 밝게 하이라이트, 이모지/심볼 안전 합성 분리 렌더링)
        Color labelColor = isHovered
            ? Color.White
            : (isCompatible
                ? (IsDarkMode ? Color.FromArgb(255, 240, 180) : Color.FromArgb(180, 110, 0))
                : (IsDarkMode ? Color.FromArgb(215, 225, 238) : Color.FromArgb(50, 60, 75)));

        using var textBrush = new SolidBrush(labelColor);
        string displayName = pin.Name;
        RectangleF labelRect = isInput
            ? new RectangleF(pinPos.X + 8, pinPos.Y - 10, 58, 20)
            : new RectangleF(pinPos.X - 66, pinPos.Y - 10, 58, 20);
        AppFontHelper.DrawMixedString(g, displayName, font?.Size ?? 8f, textBrush, labelRect, isInput ? StringAlignment.Near : StringAlignment.Far, StringAlignment.Center, FontStyle.Bold);
    }

    public bool HandlePinGraphMouseDown(MouseEventArgs e, PointF worldPt)
    {
        if (e.Button == MouseButtons.Left)
        {
            // 0. 리사이즈 핸들 클릭 검사
            if (HitTestPinGraphResizeGrip(worldPt, out var rNode) && rNode != null)
            {
                _resizingGraphNode = rNode;
                _resizeStartGraphSize = new SizeF(rNode.Width, rNode.Height);
                _resizeStartGraphMousePt = worldPt;
                Cursor = Cursors.SizeNWSE;
                Invalidate();
                return true;
            }

            // 1. 핀 클릭 검사 (와이어 드래그 시작)
            var hitPin = HitTestPin(worldPt, out var foundNode);
            if (hitPin != null && foundNode != null)
            {
                IsPinDragging = true;
                DraggingSourcePin = hitPin;
                DraggingSourceNode = foundNode;
                DraggingCurrentWorldPt = worldPt;
                Invalidate();
                return true;
            }

            // 2. 노드 헤더/몸체 클릭 검사 (노드 선택 및 드래그 시작)
            var hitNode = HitTestNode(worldPt);
            if (hitNode != null)
            {
                // KeySequence 노드 접기/펼치기 버튼 클릭 검사
                if (hitNode.NodeType == GraphNodeType.KeySequence)
                {
                    float baseW = 240f;
                    float nodeScale = Math.Clamp(hitNode.Width / baseW, 0.7f, 3.0f);
                    float fontScale = Math.Clamp((float)Math.Pow(nodeScale, 0.65), 0.85f, 2.2f);
                    float headerH = Math.Max(28f, 28f * fontScale);
                    float btnW = 58f * fontScale;
                    float btnH = 18f * fontScale;
                    var foldBtnRect = new RectangleF(hitNode.X + hitNode.Width - btnW - 8f * fontScale, hitNode.Y + (headerH - btnH) / 2f, btnW, btnH);
                    if (foldBtnRect.Contains(worldPt))
                    {
                        hitNode.IsCollapsed = !hitNode.IsCollapsed;
                        _profile.PinGraphCollapsedNodes[hitNode.Id] = hitNode.IsCollapsed;
                        hitNode.Height = hitNode.IsCollapsed 
                            ? 75f 
                            : Math.Max(90f, 36f + (hitNode.SequenceActions?.Count ?? 0) * 22f);
                        Invalidate();
                        return true;
                    }
                }

                SelectedGraphNode = hitNode;
                SelectedGraphNodeId = hitNode.Id;
                if (!SelectedGraphNodeIds.Contains(hitNode.Id))
                {
                    SelectedGraphNodeIds.Clear();
                    SelectedGraphNodeIds.Add(hitNode.Id);
                }

                _draggingGraphNode = hitNode;
                _dragNodeStartPt = new PointF(hitNode.X, hitNode.Y);
                _dragNodeMouseOffset = new PointF(worldPt.X - hitNode.X, worldPt.Y - hitNode.Y);

                _multiDragGraphNodeOffsets.Clear();
                if (SelectedGraphNodeIds.Count > 1)
                {
                    foreach (var id in SelectedGraphNodeIds)
                    {
                        var n = PinGraph.FindNode(id);
                        if (n != null)
                        {
                            _multiDragGraphNodeOffsets[id] = new PointF(n.X - hitNode.X, n.Y - hitNode.Y);
                        }
                    }
                }

                GraphNodeSelected?.Invoke(hitNode);

                // FSM 상태 및 인스펙터 연동 동기화
                if (!string.IsNullOrEmpty(hitNode.StateTag))
                {
                    SelectState(hitNode.StateTag);
                }

                Invalidate();
                return true;
            }

            // 빈 공간 클릭: 마키(Rubber-band) 다중 드래그 선택 시작
            _isMarqueeSelecting = true;
            _marqueeStartWorldPt = worldPt;
            _marqueeCurrentWorldPt = worldPt;
            SelectedGraphNode = null;
            SelectedGraphNodeId = null;
            SelectedGraphNodeIds.Clear();
            GraphNodeSelected?.Invoke(null);
            Invalidate();
            return true;
        }
        else if (e.Button == MouseButtons.Right)
        {
            // 우클릭 다운: 키다운 시 팝업하지 않고 OnMouseUp에서 단일 팝업 라우팅하도록 허용
            return false;
        }

        return false;
    }

    public bool HandlePinGraphMouseMove(MouseEventArgs e, PointF worldPt)
    {
        // 0. 리사이징 조작 중
        if (_resizingGraphNode != null && e.Button == MouseButtons.Left)
        {
            float nw = Math.Max(160f, _resizeStartGraphSize.Width + (worldPt.X - _resizeStartGraphMousePt.X));
            float nh = Math.Max(60f, _resizeStartGraphSize.Height + (worldPt.Y - _resizeStartGraphMousePt.Y));
            _resizingGraphNode.Width = nw;
            _resizingGraphNode.Height = nh;
            _profile.PinGraphNodeSizes[_resizingGraphNode.Id] = new NodeCanvasSize(nw, nh);
            Cursor = Cursors.SizeNWSE;
            Invalidate();
            return true;
        }

        if (_isMarqueeSelecting)
        {
            _marqueeCurrentWorldPt = worldPt;
            Invalidate();
            return true;
        }

        if (IsPinDragging)
        {
            DraggingCurrentWorldPt = worldPt;
            HoveredPin = HitTestPin(worldPt, out _);
            Invalidate();
            return true;
        }

        if (_draggingGraphNode != null && e.Button == MouseButtons.Left)
        {
            float newX = worldPt.X - _dragNodeMouseOffset.X;
            float newY = worldPt.Y - _dragNodeMouseOffset.Y;
            _draggingGraphNode.X = newX;
            _draggingGraphNode.Y = newY;
            _profile.PinGraphNodePositions[_draggingGraphNode.Id] = new NodeCanvasPosition(newX, newY);

            if (_multiDragGraphNodeOffsets.Count > 0)
            {
                foreach (var kvp in _multiDragGraphNodeOffsets)
                {
                    if (kvp.Key == _draggingGraphNode.Id) continue;
                    var other = PinGraph.FindNode(kvp.Key);
                    if (other != null)
                    {
                        other.X = newX + kvp.Value.X;
                        other.Y = newY + kvp.Value.Y;
                        _profile.PinGraphNodePositions[other.Id] = new NodeCanvasPosition(other.X, other.Y);
                    }
                }
            }

            Invalidate();
            return true;
        }

        // 호버 커서 제어
        if (HitTestPinGraphResizeGrip(worldPt, out _))
        {
            Cursor = Cursors.SizeNWSE;
        }
        else if (HitTestPin(worldPt, out _) != null)
        {
            Cursor = Cursors.Hand;
        }
        else if (HitTestNode(worldPt) != null)
        {
            Cursor = Cursors.SizeAll;
        }
        else
        {
            Cursor = Cursors.Default;
        }

        return false;
    }

    public bool HandlePinGraphMouseUp(MouseEventArgs e, PointF worldPt)
    {
        if (_resizingGraphNode != null)
        {
            _profile.PinGraphNodeSizes[_resizingGraphNode.Id] = new NodeCanvasSize(_resizingGraphNode.Width, _resizingGraphNode.Height);
            _resizingGraphNode = null;
            Cursor = Cursors.Default;
            ProfileModified?.Invoke();
            Invalidate();
            return true;
        }

        if (_isMarqueeSelecting)
        {
            _isMarqueeSelecting = false;
            float mx = Math.Min(_marqueeStartWorldPt.X, _marqueeCurrentWorldPt.X);
            float my = Math.Min(_marqueeStartWorldPt.Y, _marqueeCurrentWorldPt.Y);
            float mw = Math.Abs(_marqueeCurrentWorldPt.X - _marqueeStartWorldPt.X);
            float mh = Math.Abs(_marqueeCurrentWorldPt.Y - _marqueeStartWorldPt.Y);
            var marqueeRect = new RectangleF(mx, my, mw, mh);

            SelectedGraphNodeIds.Clear();
            if (mw > 4 || mh > 4)
            {
                foreach (var node in PinGraph.Nodes)
                {
                    var nRect = new RectangleF(node.X, node.Y, node.Width, node.Height);
                    if (nRect.IntersectsWith(marqueeRect))
                    {
                        SelectedGraphNodeIds.Add(node.Id);
                    }
                }
            }

            if (SelectedGraphNodeIds.Count >= 1)
            {
                SelectedGraphNodeId = SelectedGraphNodeIds.First();
                SelectedGraphNode = PinGraph.FindNode(SelectedGraphNodeId);
                GraphNodeSelected?.Invoke(SelectedGraphNode);
            }
            else
            {
                SelectedGraphNode = null;
                SelectedGraphNodeId = null;
                GraphNodeSelected?.Invoke(null);
            }

            Invalidate();
            return true;
        }

        if (IsPinDragging)
        {
            var targetPin = HitTestPin(worldPt, out var targetNode);
            if (targetPin != null && targetNode != null && DraggingSourceNode != null && DraggingSourcePin != null)
            {
                int beforeCount = PinGraph.Connections.Count;
                PinGraph.AddConnection(DraggingSourceNode.Id, DraggingSourcePin.Id, targetNode.Id, targetPin.Id);
                if (PinGraph.Connections.Count != beforeCount)
                {
                    // 상태 전이 노드 연결 시 TargetState 지능적 동기화
                    if (DraggingSourceNode.NodeType == GraphNodeType.ChangeState && DraggingSourceNode.ActionData != null && targetNode.NodeType == GraphNodeType.Start)
                    {
                        DraggingSourceNode.ActionData.TargetState = targetNode.StateTag;
                    }
                    else if (targetNode.NodeType == GraphNodeType.ChangeState && targetNode.ActionData != null && DraggingSourceNode.NodeType == GraphNodeType.Start)
                    {
                        targetNode.ActionData.TargetState = DraggingSourceNode.StateTag;
                    }
                    RecordUndoRequested?.Invoke(Localization.IsKorean ? "핀 연결" : "Connect Pin");
                    ProfileModified?.Invoke();
                }
            }
            else if (targetPin == null && DraggingSourceNode != null && DraggingSourcePin != null)
            {
                var savedSrcNode = DraggingSourceNode;
                var savedSrcPin = DraggingSourcePin;
                IsPinDragging = false;
                DraggingSourcePin = null;
                DraggingSourceNode = null;
                Invalidate();
                ShowQuickSpawnPinMenu(savedSrcNode, savedSrcPin, worldPt, e.Location);
                return true;
            }

            IsPinDragging = false;
            DraggingSourcePin = null;
            DraggingSourceNode = null;
            Invalidate();
            return true;
        }

        if (_draggingGraphNode != null)
        {
            float dx = _draggingGraphNode.X - _dragNodeStartPt.X;
            float dy = _draggingGraphNode.Y - _dragNodeStartPt.Y;
            _profile.PinGraphNodePositions[_draggingGraphNode.Id] = new NodeCanvasPosition(_draggingGraphNode.X, _draggingGraphNode.Y);
            _draggingGraphNode = null;
            _multiDragGraphNodeOffsets.Clear();
            Cursor = Cursors.Default;
            if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3)
            {
                ProfileModified?.Invoke();
            }
            Invalidate();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 완전 자유형 핀 그래프 노드들을 보기 좋게 실행 흐름 순서대로 자동 정렬합니다.
    /// </summary>
    public void AutoLayoutPinGraph()
    {
        _profile.PinGraphNodePositions.Clear();
        // 기본 변환기로부터 충돌 없는 최적 배치 위치를 다시 계산
        _pinGraph = PinGraphConverter.FromProfile(_profile, ignoreSavedPositions: true);
        foreach (var node in _pinGraph.Nodes)
        {
            _profile.PinGraphNodePositions[node.Id] = new NodeCanvasPosition(node.X, node.Y);
        }
        ResetView();
        Invalidate();
        ProfileModified?.Invoke();
    }

    public void AddPinGraphConditionNode(string? preferredState, PointF worldPt, GraphNode? srcNode = null, GraphPin? srcPin = null)
    {
        RecordUndoRequested?.Invoke(Localization.IsKorean ? "핀 조건 노드 추가" : "Add Condition Pin Node");
        string targetState = preferredState ?? SelectedState ?? _profile.InitialState ?? "기본 상태";
        bool ko = Localization.IsKorean;

        var newTrig = new MacroTrigger
        {
            Type = TriggerType.Always,
            CustomColorHex = "#2ECC71"
        };

        // 기존 조건 노드에서 드래그하여 연결하는 경우에만 동일 이벤트의 추가 AND 조건으로 결합
        var targetEvt = (srcNode != null && srcNode.NodeType == GraphNodeType.Condition)
            ? _profile.Events.FirstOrDefault(e => srcNode.Id.Contains(e.Id))
            : _profile.Events.FirstOrDefault(e => e.StateTag == targetState && e.Triggers.Count == 0);

        if (targetEvt == null)
        {
            targetEvt = new MacroEvent
            {
                Name = ko ? $"조건 이벤트 {_profile.Events.Count + 1}" : $"Condition Event {_profile.Events.Count + 1}",
                StateTag = targetState,
                Triggers = new List<MacroTrigger>() // 기본 생성자의 기본 트리거 1개 중복 방지: 빈 목록으로 초기화
            };
            _profile.Events.Add(targetEvt);
        }

        targetEvt.Triggers.Add(newTrig);
        int tIdx = targetEvt.Triggers.Count - 1;
        string nodeId = $"trig_{targetEvt.Id}_{tIdx}";
        _profile.PinGraphNodePositions[nodeId] = new NodeCanvasPosition(worldPt.X, worldPt.Y);

        _pinGraph = PinGraphConverter.FromProfile(_profile);
        Invalidate();
        ProfileModified?.Invoke();
    }

    public void AddPinGraphActionNode(ActionType type, string? preferredState, PointF worldPt, GraphNode? srcNode = null, GraphPin? srcPin = null)
    {
        RecordUndoRequested?.Invoke(Localization.IsKorean ? $"핀 행동 노드 추가: {type}" : $"Add Action Pin Node: {type}");
        string targetState = preferredState ?? SelectedState ?? _profile.InitialState ?? "기본 상태";
        bool ko = Localization.IsKorean;

        var newAct = new MacroAction { Type = type };
        switch (type)
        {
            case ActionType.MouseClick:
                newAct.Description = ko ? "좌표 클릭" : "Click Coordinates";
                newAct.MouseButton = MouseButtonType.Left;
                newAct.TargetMode = MouseTargetMode.Coordinates;
                newAct.CustomX = (int)Math.Max(0, worldPt.X);
                newAct.CustomY = (int)Math.Max(0, worldPt.Y);
                break;
            case ActionType.KeyPress:
                newAct.Description = ko ? "키 누름: [1]" : "Key Press: [1]";
                newAct.Key = "D1";
                break;
            case ActionType.ChangeState:
                newAct.Description = ko ? "상태 전이" : "Change State";
                newAct.TargetState = _profile.States.FirstOrDefault(s => s != targetState && !MacroEventProfile.IsGlobalState(s)) ?? targetState;
                break;
            default:
                newAct.Description = ko ? "지연 대기" : "Wait";
                newAct.WaitDurationMs = 500;
                break;
        }

        var targetEvt = _profile.Events.FirstOrDefault(e => (srcNode != null && srcNode.Id.Contains(e.Id)))
                        ?? _profile.Events.FirstOrDefault(e => e.StateTag == targetState);

        if (targetEvt == null)
        {
            targetEvt = new MacroEvent
            {
                Name = ko ? $"동작 이벤트 {_profile.Events.Count + 1}" : $"Action Event {_profile.Events.Count + 1}",
                StateTag = targetState,
                Triggers = new List<MacroTrigger>() // 빈 목록으로 초기화
            };
            _profile.Events.Add(targetEvt);
        }

        targetEvt.Actions.Add(newAct);
        int aIdx = targetEvt.Actions.Count - 1;
        string nodeId = $"act_{targetEvt.Id}_{aIdx}";
        _profile.PinGraphNodePositions[nodeId] = new NodeCanvasPosition(worldPt.X, worldPt.Y);

        _pinGraph = PinGraphConverter.FromProfile(_profile);
        Invalidate();
        ProfileModified?.Invoke();
    }

    private void ShowQuickSpawnPinMenu(GraphNode srcNode, GraphPin srcPin, PointF worldPt, Point screenPt)
    {
        bool ko = Localization.IsKorean;
        var cms = new ContextMenuStrip();

        var titleItem = new ToolStripMenuItem(ko ? "⚡ 연결할 새 노드 생성:" : "⚡ Create Node to Connect:") { Enabled = false };
        titleItem.Font = AppFontHelper.GetEmojiFont(9f, FontStyle.Bold);
        cms.Items.Add(titleItem);
        cms.Items.Add(new ToolStripSeparator());

        // 1. 조건 감지 노드
        cms.Items.Add(ko ? "➕ 새 조건 감지 노드 (True/Else)" : "➕ Condition Node (True/Else)", null, (s, ev) =>
        {
            AddPinGraphConditionNode(srcNode.StateTag, worldPt, srcNode, srcPin);
        });

        // 2. 마우스 클릭 노드
        cms.Items.Add(ko ? "🖱️ 새 마우스 클릭 노드" : "🖱️ Mouse Click Node", null, (s, ev) =>
        {
            AddPinGraphActionNode(ActionType.MouseClick, srcNode.StateTag, worldPt, srcNode, srcPin);
        });

        // 3. 키보드 입력 노드
        cms.Items.Add(ko ? "⌨️ 새 키보드 입력 노드" : "⌨️ Key Press Node", null, (s, ev) =>
        {
            AddPinGraphActionNode(ActionType.KeyPress, srcNode.StateTag, worldPt, srcNode, srcPin);
        });

        // 4. 상태 전환 노드
        cms.Items.Add(ko ? "🔄 새 상태 전환 노드" : "🔄 Change State Node", null, (s, ev) =>
        {
            AddPinGraphActionNode(ActionType.ChangeState, srcNode.StateTag, worldPt, srcNode, srcPin);
        });

        cms.Show(this, screenPt);
    }
}

using System.Drawing;
using System.Drawing.Drawing2D;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

public partial class NodeCanvasControl
{
    // -------------------------------------------------------------
    // 내부 레이아웃 캐시 데이터 구조체
    // -------------------------------------------------------------
    private class TriggerChipLayout
    {
        public RectangleF ChipBounds;
        public RectangleF DeleteBounds;
        public RectangleF GaugeTrackBounds;
        public RectangleF GaugeTrackBounds2;
        public RectangleF SwatchBounds;
        public RectangleF MatchModeBounds;
        public RectangleF FileLetterboxBounds;
        public RectangleF ComparisonOpBounds;
        public bool HasGauge;
        public bool HasGauge2;
        public MacroTrigger Trigger = null!;
    }

    private class ActionChipLayout
    {
        public RectangleF ChipBounds;
        public RectangleF DeleteBounds;
        public RectangleF GaugeTrackBounds;
        public RectangleF ButtonModeBounds;
        public RectangleF CoordLetterboxBounds;
        public RectangleF FileLetterboxBounds;
        public RectangleF TargetStateBadgeBounds;
        public bool HasGauge;
        public MacroAction Action = null!;
    }

    private class EventNodeLayout
    {
        public MacroEvent Event = null!;
        public RectangleF Bounds;
        public RectangleF HeaderBounds;
        public RectangleF DeleteEventBtnBounds;

        public RectangleF TriggerSectionBounds;
        public RectangleF AddTriggerBtnBounds;
        public List<TriggerChipLayout> TriggerChips = new();

        public RectangleF ActionSectionBounds;
        public RectangleF AddActionBtnBounds;
        public RectangleF RecordBtnBounds;
        public List<ActionChipLayout> ActionChips = new();

        public PointF PinPt;
    }

    private class StateNodeLayout
    {
        public string State = "";
        public RectangleF Bounds;
        public RectangleF HeaderBounds;
        public List<EventNodeLayout> Events = new();
        public RectangleF AddEventBtnBounds;
    }

    /// <summary>
    /// 등록된 모든 상태 노드가 유효한 캔버스 좌표를 갖도록 보장하고 자동 배치합니다.
    /// 동적 노드 높이(GetNodeBounds)와 FSM 전이(Transition) 위상을 반영하여 어떠한 상태 노드도 겹치지 않도록 보장합니다.
    /// </summary>
    public void EnsureNodePositions()
    {
        bool allPresent = _profile.States.All(s => _profile.NodePositions.ContainsKey(s));
        if (allPresent) return;

        ComputeSmartLayout();
    }

    /// <summary>
    /// 모든 상태 노드를 보기 좋게 FSM 전이 흐름 및 동적 노드 높이 기반으로 완전 자동 재정렬합니다.
    /// </summary>
    public void AutoLayoutNodes()
    {
        _profile.NodePositions.Clear();
        ComputeSmartLayout();
        ResetView();
        Invalidate();
        ProfileModified?.Invoke();
    }

    private void ComputeSmartLayout()
    {
        MacroLayoutEngine.ComputeSmartLayout(
            _profile,
            st => GetNodeBounds(st, new NodeCanvasPosition(0, 0)).Height
        );
    }

    public void ResetView()
    {
        PanOffset = new PointF(60, 60);
        Zoom = 1.0f;
        Invalidate();
    }

    public void FocusState(string state)
    {
        if (_profile.NodePositions.TryGetValue(state, out var pos))
        {
            PanOffset = new PointF(Width / 2f - (pos.X + 170) * Zoom, Height / 2f - (pos.Y + 80) * Zoom);
            Invalidate();
        }
    }

    // 좌표계 변환 함수
    public PointF WorldToScreen(PointF worldPt) =>
        new(worldPt.X * Zoom + PanOffset.X, worldPt.Y * Zoom + PanOffset.Y);

    public PointF ScreenToWorld(Point screenPt) =>
        new((screenPt.X - PanOffset.X) / Zoom, (screenPt.Y - PanOffset.Y) / Zoom);

    private StateNodeLayout CalculateStateLayout(string state, NodeCanvasPosition pos)
    {
        float width = 340f;
        if (_profile.NodeSizes.TryGetValue(state, out var customSize) && customSize.Width >= 220f)
        {
            width = customSize.Width;
        }

        float nodeScale = Math.Clamp(width / 340f, 0.7f, 3.0f);
        float fontScale = Math.Clamp((float)Math.Pow(nodeScale, 0.65), 0.85f, 2.2f);

        float headerHeight = 38f * fontScale;
        float bodyPadLeft = 10f * fontScale;
        float bodyPadTop = 10f * fontScale;
        float curY = pos.Y + headerHeight + bodyPadTop;

        var events = _profile.Events.Where(e => e.StateTag == state).ToList();
        var evLayouts = new List<EventNodeLayout>();

        using var fontSub = AppFontHelper.GetDefaultFont(8f * fontScale);

        foreach (var evt in events)
        {
            var el = new EventNodeLayout { Event = evt };
            float startEvtY = curY;

            // 1. 이벤트 헤더 (26px * fontScale)
            float evtHeaderH = 26f * fontScale;
            el.HeaderBounds = new RectangleF(pos.X + bodyPadLeft, curY, width - (bodyPadLeft * 2), evtHeaderH);
            el.DeleteEventBtnBounds = RectangleF.Empty;
            curY += evtHeaderH + 4f * fontScale;

            // 2. 조건 섹션 헤더
            float trigSecH = 20f * fontScale;
            el.TriggerSectionBounds = new RectangleF(pos.X + bodyPadLeft + 4f * fontScale, curY, width - (bodyPadLeft * 2) - 8f * fontScale, trigSecH);
            el.AddTriggerBtnBounds = RectangleF.Empty;
            curY += trigSecH + 3f * fontScale;

            // 조건 칩들
            if (evt.Triggers.Count == 0)
            {
                curY += 18f * fontScale; // 빈 상태 라벨용
            }
            else
            {
                foreach (var trg in evt.Triggers)
                {
                    float chipLeft = pos.X + bodyPadLeft + 6f * fontScale;
                    float chipW = width - (bodyPadLeft * 2) - 12f * fontScale;

                    bool isVisualTrigger = (trg.Type == TriggerType.VisionDetection || trg.Type == TriggerType.GaugeBar || trg.Type == TriggerType.ImageDetection);
                    float chipHeight = isVisualTrigger ? (54f * fontScale) : (28f * fontScale);

                    var tc = new TriggerChipLayout
                    {
                        Trigger = trg,
                        ChipBounds = new RectangleF(chipLeft, curY, chipW, chipHeight),
                        DeleteBounds = RectangleF.Empty
                    };

                    if (trg.Type == TriggerType.GaugeBar)
                    {
                        tc.SwatchBounds = new RectangleF(chipLeft + 6 * fontScale, curY + 7 * fontScale, 40 * fontScale, 40 * fontScale);
                        tc.ComparisonOpBounds = new RectangleF(chipLeft + 52 * fontScale, curY + 5 * fontScale, 45 * fontScale, 15 * fontScale);
                        tc.GaugeTrackBounds = new RectangleF(chipLeft + 52 * fontScale, curY + 23 * fontScale, Math.Max(70 * fontScale, chipW - 60 * fontScale), 8 * fontScale);
                        tc.GaugeTrackBounds2 = new RectangleF(chipLeft + 52 * fontScale, curY + 37 * fontScale, Math.Max(70 * fontScale, chipW - 60 * fontScale), 8 * fontScale);
                        tc.HasGauge = true;
                        tc.HasGauge2 = true;
                    }
                    else if (trg.Type == TriggerType.ImageDetection)
                    {
                        tc.FileLetterboxBounds = new RectangleF(chipLeft + 6 * fontScale, curY + 7 * fontScale, 46 * fontScale, 40 * fontScale);
                        tc.GaugeTrackBounds = new RectangleF(chipLeft + 56 * fontScale, curY + 28 * fontScale, Math.Max(75 * fontScale, chipW - 65 * fontScale), 8 * fontScale);
                        tc.HasGauge = true;
                    }
                    else if (trg.Type == TriggerType.VisionDetection)
                    {
                        if (trg.VisionTarget == VisionTargetType.Color)
                        {
                            tc.SwatchBounds = new RectangleF(chipLeft + 6 * fontScale, curY + 7 * fontScale, 40 * fontScale, 40 * fontScale);
                            tc.MatchModeBounds = new RectangleF(chipLeft + 52 * fontScale, curY + 5 * fontScale, 34 * fontScale, 15 * fontScale);
                            tc.GaugeTrackBounds = new RectangleF(chipLeft + 52 * fontScale, curY + 23 * fontScale, Math.Max(70 * fontScale, chipW - 60 * fontScale), 8 * fontScale);
                            tc.GaugeTrackBounds2 = new RectangleF(chipLeft + 52 * fontScale, curY + 37 * fontScale, Math.Max(70 * fontScale, chipW - 60 * fontScale), 8 * fontScale);
                            tc.HasGauge = true;
                            tc.HasGauge2 = true;
                        }
                        else
                        {
                            tc.FileLetterboxBounds = new RectangleF(chipLeft + 6 * fontScale, curY + 7 * fontScale, 46 * fontScale, 40 * fontScale);
                            tc.MatchModeBounds = new RectangleF(chipLeft + 56 * fontScale, curY + 6 * fontScale, 34 * fontScale, 16 * fontScale);
                            tc.GaugeTrackBounds = new RectangleF(chipLeft + 56 * fontScale, curY + 28 * fontScale, Math.Max(75 * fontScale, chipW - 65 * fontScale), 8 * fontScale);
                            tc.HasGauge = true;
                        }
                    }

                    el.TriggerChips.Add(tc);
                    curY += chipHeight + 4f * fontScale;
                }
            }

            // 3. 행동 섹션 헤더
            curY += 3f * fontScale;
            float actSecH = 20f * fontScale;
            el.ActionSectionBounds = new RectangleF(pos.X + bodyPadLeft + 4f * fontScale, curY, width - (bodyPadLeft * 2) - 8f * fontScale, actSecH);
            el.RecordBtnBounds = RectangleF.Empty;
            el.AddActionBtnBounds = RectangleF.Empty;
            curY += actSecH + 3f * fontScale;

            // 행동 칩들
            if (evt.Actions.Count == 0)
            {
                curY += 18f * fontScale;
            }
            else
            {
                foreach (var act in evt.Actions)
                {
                    float chipLeft = pos.X + bodyPadLeft + 6f * fontScale;
                    float chipW = width - (bodyPadLeft * 2) - 12f * fontScale;
                    float actChipH = 28f * fontScale;
                    float trackY = curY + 9f * fontScale;

                    var ac = new ActionChipLayout
                    {
                        Action = act,
                        ChipBounds = new RectangleF(chipLeft, curY, chipW, actChipH),
                        DeleteBounds = RectangleF.Empty
                    };

                    if (act.Type == ActionType.Wait)
                    {
                        ac.GaugeTrackBounds = new RectangleF(chipLeft + 88 * fontScale, trackY + 1, 75 * fontScale, 8 * fontScale);
                        ac.HasGauge = true;
                    }
                    else if (act.Type == ActionType.MouseClick)
                    {
                        ac.ButtonModeBounds = new RectangleF(chipLeft + 4 * fontScale, trackY - 9 * fontScale, 44 * fontScale, 18 * fontScale);
                        if (act.TargetMode == MouseTargetMode.Coordinates)
                        {
                            ac.CoordLetterboxBounds = new RectangleF(chipLeft + 52 * fontScale, trackY - 9 * fontScale, 65 * fontScale, 18 * fontScale);
                        }
                        else if (act.TargetMode == MouseTargetMode.TargetImage)
                        {
                            ac.FileLetterboxBounds = new RectangleF(chipLeft + 52 * fontScale, trackY - 9 * fontScale, 85 * fontScale, 18 * fontScale);
                            ac.GaugeTrackBounds = new RectangleF(chipLeft + 145 * fontScale, trackY + 1, 50 * fontScale, 8 * fontScale);
                            ac.HasGauge = true;
                        }
                    }
                    else if (act.Type == ActionType.ChangeState)
                    {
                        ac.TargetStateBadgeBounds = new RectangleF(chipLeft + 6 * fontScale, trackY - 9 * fontScale, chipW - 20 * fontScale, 18 * fontScale);
                    }

                    el.ActionChips.Add(ac);
                    curY += actChipH + 4f * fontScale;
                }
            }

            curY += 6f * fontScale; // 이벤트 하단 여백
            el.Bounds = new RectangleF(pos.X + bodyPadLeft * 0.7f, startEvtY, width - (bodyPadLeft * 1.4f), curY - startEvtY);
            el.PinPt = new PointF(el.Bounds.Right, startEvtY + (curY - startEvtY) / 2);
            evLayouts.Add(el);

            curY += 6f * fontScale; // 다음 이벤트와의 간격
        }

        if (events.Count == 0)
        {
            curY += 28f * fontScale; // 빈 상태 안내 문구 여백
        }

        curY += 8f * fontScale; // 노드 하단 여백
        float totalHeight = curY - pos.Y;
        if (_profile.NodeSizes.TryGetValue(state, out var customSizeH) && customSizeH.Height > totalHeight)
        {
            totalHeight = customSizeH.Height;
        }
        return new StateNodeLayout
        {
            State = state,
            Bounds = new RectangleF(pos.X, pos.Y, width, totalHeight),
            HeaderBounds = new RectangleF(pos.X, pos.Y, width, headerHeight),
            Events = evLayouts,
            AddEventBtnBounds = RectangleF.Empty
        };
    }

    // 그래픽 유틸리티 함수
    public RectangleF GetNodeBounds(string state, NodeCanvasPosition pos)
    {
        return CalculateStateLayout(state, pos).Bounds;
    }

    private void FillRoundedRectangle(Graphics g, Brush brush, RectangleF rect, float r)
    {
        using var path = CreateRoundedPath(rect, r);
        g.FillPath(brush, path);
    }

    private void DrawRoundedRectangle(Graphics g, Pen pen, RectangleF rect, float r)
    {
        using var path = CreateRoundedPath(rect, r);
        g.DrawPath(pen, path);
    }

    private void FillRoundedHeader(Graphics g, Brush brush, RectangleF rect, float r)
    {
        using var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
        path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
        path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    private GraphicsPath CreateRoundedPath(RectangleF rect, float r)
    {
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
        path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
        path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }
}

using System.Drawing;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

public partial class NodeCanvasControl
{
    private string? _resizingState;
    private RectangleF _resizeStartStateBounds;
    private PointF _resizeStartStateMousePt;

    public bool HitTestStateResizeGrip(PointF worldPt, out string? foundState, out RectangleF nodeBounds)
    {
        foundState = null;
        nodeBounds = RectangleF.Empty;
        foreach (var state in _profile.States.AsEnumerable().Reverse())
        {
            if (!_profile.NodePositions.TryGetValue(state, out var pos)) continue;
            var layout = CalculateStateLayout(state, pos);
            var gripRect = new RectangleF(layout.Bounds.Right - 16, layout.Bounds.Bottom - 16, 16, 16);
            if (gripRect.Contains(worldPt))
            {
                foundState = state;
                nodeBounds = layout.Bounds;
                return true;
            }
        }
        return false;
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _resizingState = null;
        _isMarqueeSelecting = false;
        _isPanning = false;
        _isConnecting = false;
        _draggingState = null;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        _lastMousePos = e.Location;
        var worldPt = ScreenToWorld(e.Location);

        // 이전 상호작용 잔여 상태 안전 초기화 (대화상자 호출 등으로 인한 키업/마우스업 유실 방지)
        _isMarqueeSelecting = false;
        _isConnecting = false;
        _draggingState = null;

        // 휠 클릭 시 화면 이동(Pan)
        if (e.Button == MouseButtons.Middle)
        {
            _isPanning = true;
            Cursor = Cursors.SizeAll;
            return;
        }

        if (IsPinGraphMode)
        {
            if (HandlePinGraphMouseDown(e, worldPt)) return;
        }

        // 우클릭 다운: 대상 하이라이트 선택 처리 (메뉴는 마우스 업 시에만 팝업)
        if (e.Button == MouseButtons.Right)
        {
            _isRightDown = true;
            _rightMouseDownScreenPt = e.Location;
            _rightMouseDownWorldPt = worldPt;

            bool hitFound = false;
            foreach (var state in _profile.States.AsEnumerable().Reverse())
            {
                if (!_profile.NodePositions.TryGetValue(state, out var pos)) continue;
                var layout = CalculateStateLayout(state, pos);

                if (layout.Bounds.Contains(worldPt))
                {
                    hitFound = true;
                    if (!SelectedStates.Contains(state))
                    {
                        SelectedState = state;
                        SelectedStates.Clear();
                        SelectedStates.Add(state);
                    }

                    SelectedEvent = null;
                    SelectedTriggerChip = null;
                    SelectedActionChip = null;

                    foreach (var el in layout.Events)
                    {
                        if (el.Bounds.Contains(worldPt))
                        {
                            SelectedEvent = el.Event;
                            foreach (var tc in el.TriggerChips)
                            {
                                if (tc.ChipBounds.Contains(worldPt))
                                {
                                    SelectedTriggerChip = (el.Event, tc.Trigger);
                                    break;
                                }
                            }
                            foreach (var ac in el.ActionChips)
                            {
                                if (ac.ChipBounds.Contains(worldPt))
                                {
                                    SelectedActionChip = (el.Event, ac.Action);
                                    break;
                                }
                            }
                            break;
                        }
                    }

                    SelectionChanged?.Invoke(SelectedState, SelectedEvent);
                    Invalidate();
                    break;
                }
            }

            if (!hitFound)
            {
                SelectedState = null;
                SelectedStates.Clear();
                SelectedEvent = null;
                SelectedTriggerChip = null;
                SelectedActionChip = null;
                SelectionChanged?.Invoke(null, null);
                Invalidate();
            }

            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            // 0. FSM 카드 노드 우측 하단 리사이즈 핸들 클릭 검사
            if (!IsPinGraphMode && HitTestStateResizeGrip(worldPt, out var resizeState, out var gripBounds) && resizeState != null)
            {
                _resizingState = resizeState;
                _resizeStartStateBounds = gripBounds;
                _resizeStartStateMousePt = worldPt;
                Cursor = Cursors.SizeNWSE;
                Invalidate();
                return;
            }

            // 1. 이벤트의 우측 연결 핀 클릭 검사 (드래그하여 전이선 연결)
            foreach (var state in _profile.States)
            {
                if (!_profile.NodePositions.TryGetValue(state, out var pos)) continue;
                var layout = CalculateStateLayout(state, pos);

                foreach (var el in layout.Events)
                {
                    if (Math.Abs(worldPt.X - el.PinPt.X) <= 12 && Math.Abs(worldPt.Y - el.PinPt.Y) <= 12)
                    {
                        _isConnecting = true;
                        _connectingEvent = el.Event;
                        _connectingStartPt = el.PinPt;
                        _connectingCurrentWorldPt = worldPt;
                        _connectingTargetHoverState = null;
                        Invalidate();
                        return;
                    }
                }
            }

            // 2. 상태 노드 및 내부 요소 선택 검사 (그래픽 노드 내 직접 조작은 방지하고 오직 선택 및 드래그만 수행)
            foreach (var state in _profile.States.AsEnumerable().Reverse())
            {
                if (!_profile.NodePositions.TryGetValue(state, out var pos)) continue;
                var layout = CalculateStateLayout(state, pos);

                if (layout.Bounds.Contains(worldPt))
                {
                    SelectedState = state;
                    if (!SelectedStates.Contains(state))
                    {
                        SelectedStates.Clear();
                        SelectedStates.Add(state);
                    }

                    SelectedEvent = null;
                    SelectedTriggerChip = null;
                    SelectedActionChip = null;

                    // 이벤트 및 칩 선택 검사 (오직 선택만 처리하여 실수에 의한 프로필 변경 및 팝업 방지)
                    foreach (var el in layout.Events)
                    {
                        if (el.Bounds.Contains(worldPt))
                        {
                            SelectedEvent = el.Event;

                            // 조건 칩 선택 검사
                            foreach (var tc in el.TriggerChips)
                            {
                                if (tc.ChipBounds.Contains(worldPt))
                                {
                                    SelectedTriggerChip = (el.Event, tc.Trigger);
                                    SelectedActionChip = null;
                                    break;
                                }
                            }

                            // 행동 칩 선택 검사
                            if (SelectedTriggerChip == null)
                            {
                                foreach (var ac in el.ActionChips)
                                {
                                    if (ac.ChipBounds.Contains(worldPt))
                                    {
                                        SelectedActionChip = (el.Event, ac.Action);
                                        SelectedTriggerChip = null;
                                        break;
                                    }
                                }
                            }

                            break;
                        }
                    }

                    // 헤더 또는 노드 빈 공간 클릭 시 노드 드래그 시작
                    _draggingState = state;
                    _dragNodeOffset = new PointF(worldPt.X - pos.X, worldPt.Y - pos.Y);
                    _hasRecordedDragUndo = false;
                    _multiDragInitialOffsets.Clear();
                    if (SelectedStates.Count > 1 && SelectedStates.Contains(state))
                    {
                        foreach (var st in SelectedStates)
                        {
                            if (_profile.NodePositions.TryGetValue(st, out var stPos))
                            {
                                _multiDragInitialOffsets[st] = new PointF(stPos.X - pos.X, stPos.Y - pos.Y);
                            }
                        }
                    }
                    SelectionChanged?.Invoke(SelectedState, SelectedEvent);
                    Invalidate();
                    return;
                }
            }

            // 3. 노드 바깥 빈 공간 클릭: 마키(Rubber-band) 다중 드래그 선택 시작
            _isMarqueeSelecting = true;
            _marqueeStartWorldPt = worldPt;
            _marqueeCurrentWorldPt = worldPt;
            SelectedStates.Clear();
            SelectedState = null;
            SelectedEvent = null;
            SelectedTriggerChip = null;
            SelectedActionChip = null;
            SelectionChanged?.Invoke(null, null);
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var worldPt = ScreenToWorld(e.Location);

        if (_isPanning)
        {
            PanOffset = new PointF(PanOffset.X + (e.X - _lastMousePos.X), PanOffset.Y + (e.Y - _lastMousePos.Y));
            _lastMousePos = e.Location;
            Invalidate();
            return;
        }

        if (IsPinGraphMode)
        {
            if (HandlePinGraphMouseMove(e, worldPt)) return;
        }

        // FSM 카드 노드 리사이징 중
        if (!IsPinGraphMode && _resizingState != null && e.Button == MouseButtons.Left)
        {
            float newW = Math.Max(220f, _resizeStartStateBounds.Width + (worldPt.X - _resizeStartStateMousePt.X));
            float newH = Math.Max(100f, _resizeStartStateBounds.Height + (worldPt.Y - _resizeStartStateMousePt.Y));
            _profile.NodeSizes[_resizingState] = new NodeCanvasSize(newW, newH);
            Cursor = Cursors.SizeNWSE;
            Invalidate();
            return;
        }

        // 마키 드래그 영역 갱신
        if (_isMarqueeSelecting)
        {
            _marqueeCurrentWorldPt = worldPt;
            Invalidate();
            return;
        }

        if (_isConnecting)
        {
            _connectingCurrentWorldPt = worldPt;

            _connectingTargetHoverState = null;
            foreach (var state in _profile.States)
            {
                if (state == _connectingEvent?.StateTag) continue;
                if (_profile.NodePositions.TryGetValue(state, out var pos))
                {
                    if (CalculateStateLayout(state, pos).Bounds.Contains(worldPt))
                    {
                        _connectingTargetHoverState = state;
                        break;
                    }
                }
            }

            Invalidate();
            return;
        }

        if (_draggingState != null && e.Button == MouseButtons.Left)
        {
            if (_profile.NodePositions.TryGetValue(_draggingState, out var pos))
            {
                if (!_hasRecordedDragUndo)
                {
                    RecordUndoRequested?.Invoke(Localization.IsKorean ? $"상태 노드 이동: [{_draggingState}]" : $"Move State Node: [{_draggingState}]");
                    _hasRecordedDragUndo = true;
                }
                float newX = worldPt.X - _dragNodeOffset.X;
                float newY = worldPt.Y - _dragNodeOffset.Y;
                pos.X = newX;
                pos.Y = newY;

                if (_multiDragInitialOffsets.Count > 0)
                {
                    foreach (var kvp in _multiDragInitialOffsets)
                    {
                        if (kvp.Key == _draggingState) continue;
                        if (_profile.NodePositions.TryGetValue(kvp.Key, out var otherPos))
                        {
                            otherPos.X = newX + kvp.Value.X;
                            otherPos.Y = newY + kvp.Value.Y;
                        }
                    }
                }

                Invalidate();
            }
            return;
        }

        // 호버 커서 제어
        if (!IsPinGraphMode && _resizingState == null && _draggingState == null && !_isPanning && !_isConnecting && !_isMarqueeSelecting)
        {
            if (HitTestStateResizeGrip(worldPt, out _, out _))
            {
                Cursor = Cursors.SizeNWSE;
            }
            else
            {
                Cursor = Cursors.Default;
            }
        }

        _lastMousePos = e.Location;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var worldPt = ScreenToWorld(e.Location);

        if (_resizingState != null)
        {
            _resizingState = null;
            Cursor = Cursors.Default;
            ProfileModified?.Invoke();
            Invalidate();
            return;
        }

        if (_isPanning)
        {
            _isPanning = false;
            Cursor = Cursors.Default;
        }

        if (IsPinGraphMode)
        {
            if (HandlePinGraphMouseUp(e, ScreenToWorld(e.Location))) return;
        }

        // 우클릭 업: 제자리 탭/클릭이었으면 컨텍스트 메뉴 팝업!
        if (e.Button == MouseButtons.Right)
        {
            if (_isRightDown)
            {
                _isRightDown = false;
                float dx = e.X - _rightMouseDownScreenPt.X;
                float dy = e.Y - _rightMouseDownScreenPt.Y;
                if (Math.Abs(dx) <= 6 && Math.Abs(dy) <= 6)
                {
                    ShowContextMenuAt(e.Location, ScreenToWorld(e.Location));
                }
            }
            return;
        }


        if (_isMarqueeSelecting)
        {
            _isMarqueeSelecting = false;
            float mx = Math.Min(_marqueeStartWorldPt.X, _marqueeCurrentWorldPt.X);
            float my = Math.Min(_marqueeStartWorldPt.Y, _marqueeCurrentWorldPt.Y);
            float mw = Math.Abs(_marqueeCurrentWorldPt.X - _marqueeStartWorldPt.X);
            float mh = Math.Abs(_marqueeCurrentWorldPt.Y - _marqueeStartWorldPt.Y);
            var marqueeRect = new RectangleF(mx, my, mw, mh);

            SelectedStates.Clear();
            if (mw > 4 || mh > 4)
            {
                foreach (var state in _profile.States)
                {
                    if (_profile.NodePositions.TryGetValue(state, out var pos))
                    {
                        var layout = CalculateStateLayout(state, pos);
                        if (layout.Bounds.IntersectsWith(marqueeRect))
                        {
                            SelectedStates.Add(state);
                        }
                    }
                }
            }

            if (SelectedStates.Count == 1)
            {
                SelectedState = SelectedStates.First();
                SelectionChanged?.Invoke(SelectedState, null);
            }
            else
            {
                SelectedState = null;
                SelectedEvent = null;
                SelectionChanged?.Invoke(null, null);
            }
            Invalidate();
        }

        if (_draggingState != null)
        {
            _draggingState = null;
            _multiDragInitialOffsets.Clear();
            if (_hasRecordedDragUndo)
            {
                ProfileModified?.Invoke();
            }
        }

        if (_isConnecting)
        {
            if (_connectingEvent != null && !string.IsNullOrEmpty(_connectingTargetHoverState))
            {
                bool alreadyHasTransition = _connectingEvent.Actions.Any(a => a.Type == ActionType.ChangeState && a.TargetState == _connectingTargetHoverState);
                if (!alreadyHasTransition)
                {
                    RecordUndoRequested?.Invoke(Localization.IsKorean ? $"전이선 연결: [{_connectingEvent.Name}] ➔ [{_connectingTargetHoverState}]" : $"Connect Transition Wire: [{_connectingEvent.Name}] ➔ [{_connectingTargetHoverState}]");
                    _connectingEvent.Actions.Add(new MacroAction
                    {
                        Type = ActionType.ChangeState,
                        TargetState = _connectingTargetHoverState,
                        Description = Localization.IsKorean ? $"상태 전환 ➔ [{_connectingTargetHoverState}]" : $"Change State ➔ [{_connectingTargetHoverState}]"
                    });
                    ProfileModified?.Invoke();
                }
            }
            else if (_connectingEvent != null && string.IsNullOrEmpty(_connectingTargetHoverState))
            {
                float dx = worldPt.X - _connectingStartPt.X;
                float dy = worldPt.Y - _connectingStartPt.Y;
                if (Math.Abs(dx) > 15 || Math.Abs(dy) > 15)
                {
                    var savedEvt = _connectingEvent;
                    _isConnecting = false;
                    _connectingEvent = null;
                    _connectingTargetHoverState = null;
                    Invalidate();
                    ShowQuickSpawnStateMenu(savedEvt, worldPt, e.Location);
                    return;
                }
            }

            _isConnecting = false;
            _connectingEvent = null;
            _connectingTargetHoverState = null;
            Invalidate();
        }
    }

    /// <summary>
    /// 마우스 우클릭 위치에 따라 칩, 이벤트, 상태 노드, 빈 캔버스 전용 컨텍스트 메뉴를 분기 출력합니다.
    /// </summary>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        float oldZoom = Zoom;
        float steps = e.Delta / 120.0f;
        float newZoom = Math.Clamp(Zoom + steps * 0.025f, 0.10f, 3.0f);
        newZoom = (float)(Math.Round(newZoom / 0.025) * 0.025);
        Zoom = Math.Clamp(newZoom, 0.10f, 3.0f);

        Point mouseScreen = e.Location;
        PanOffset = new PointF(
            mouseScreen.X - (mouseScreen.X - PanOffset.X) * (Zoom / oldZoom),
            mouseScreen.Y - (mouseScreen.Y - PanOffset.Y) * (Zoom / oldZoom)
        );

        Invalidate();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var worldPt = ScreenToWorld(e.Location);

        if (IsPinGraphMode)
        {
            var hitGraphNode = HitTestNode(worldPt);
            if (hitGraphNode != null)
            {
                PinGraphNodeEditRequested?.Invoke(hitGraphNode);
                return;
            }
        }

        foreach (var state in _profile.States)
        {
            if (!_profile.NodePositions.TryGetValue(state, out var pos)) continue;
            var layout = CalculateStateLayout(state, pos);

            if (layout.Bounds.Contains(worldPt))
            {
                // 1. 비어있는 그래픽 노드인 경우 -> 더블클릭 시 즉시 새 이벤트 생성!
                if (layout.Events.Count == 0)
                {
                    AddEventToStateRequested?.Invoke(state);
                    return;
                }

                // 2. 이벤트가 있는 노드의 헤더 더블클릭 -> 상태 이름 변경
                if (layout.HeaderBounds.Contains(worldPt))
                {
                    StateDoubleClicked?.Invoke(state);
                    return;
                }

                // 3. 이벤트 내부 칩 더블클릭 -> 해당 조건 또는 행동 편집 대화상자 호출
                foreach (var el in layout.Events)
                {
                    if (el.Bounds.Contains(worldPt))
                    {
                        foreach (var tc in el.TriggerChips)
                        {
                            if (tc.ChipBounds.Contains(worldPt))
                            {
                                TriggerEditRequested?.Invoke(el.Event, tc.Trigger);
                                return;
                            }
                        }

                        foreach (var ac in el.ActionChips)
                        {
                            if (ac.ChipBounds.Contains(worldPt))
                            {
                                ActionEditRequested?.Invoke(el.Event, ac.Action);
                                return;
                            }
                        }

                        // 행동이 미등록된 실행 결과 영역 더블클릭 -> 우클릭과 동일한 컨텍스트 메뉴(행동 추가 등) 표시
                        if (el.Event.Actions.Count == 0 && worldPt.Y >= el.ActionSectionBounds.Top)
                        {
                            ShowContextMenuAt(e.Location, worldPt);
                            return;
                        }

                        // 조건이 미등록된 발동 조건 영역 더블클릭 -> 우클릭과 동일한 컨텍스트 메뉴(조건 추가 등) 표시
                        if (el.Event.Triggers.Count == 0 && worldPt.Y >= el.TriggerSectionBounds.Top && worldPt.Y < el.ActionSectionBounds.Top)
                        {
                            ShowContextMenuAt(e.Location, worldPt);
                            return;
                        }

                        // 칩 영역 외 이벤트 카드 본체 더블클릭 -> 이벤트 설정 팝업 호출!
                        EventEditRequested?.Invoke(el.Event);
                        return;
                    }
                }

                // 4. 이벤트 카드 바깥의 노드 빈 여백 더블클릭 시 새 이벤트 추가
                AddEventToStateRequested?.Invoke(state);
                return;
            }
        }
    }

    private void ShowQuickSpawnStateMenu(MacroEvent srcEvt, PointF worldPt, Point screenPt)
    {
        bool ko = Localization.IsKorean;
        var cms = new ContextMenuStrip();

        var titleItem = new ToolStripMenuItem(ko ? "⚡ 새 상태 노드를 생성하여 전이선 연결:" : "⚡ Create State Node & Connect:") { Enabled = false };
        titleItem.Font = AppFontHelper.GetEmojiFont(9f, FontStyle.Bold);
        cms.Items.Add(titleItem);
        cms.Items.Add(new ToolStripSeparator());

        cms.Items.Add(ko ? "➕ 여기에 새 상태 노드 추가 및 연결" : "➕ Add State Node Here & Connect", null, (s, ev) =>
        {
            string defaultName = _profile.GenerateNextStateName(ko);
            string? newState = PromptTextInput(
                Localization.Get("PromptAddStateTitle"),
                Localization.Get("PromptAddStateDesc"),
                defaultName);

            if (!string.IsNullOrWhiteSpace(newState))
            {
                newState = _profile.ResolveUniqueStateName(newState, ko);
                RecordUndoRequested?.Invoke(Localization.IsKorean ? $"새 연결 상태 생성: [{newState}]" : $"Create Connected State: [{newState}]");
                _profile.States.Add(newState);
                _profile.NodePositions[newState] = new NodeCanvasPosition(worldPt.X, worldPt.Y);

                srcEvt.Actions.Add(new MacroAction
                {
                    Type = ActionType.ChangeState,
                    TargetState = newState,
                    Description = $"상태 전이: [{newState}]"
                });

                SelectedState = newState;
                ProfileModified?.Invoke();
                Invalidate();
            }
        });

        cms.Show(this, screenPt);
    }

    private string? PromptTextInput(string title, string prompt, string defaultVal)
    {
        using var promptForm = new Form
        {
            Width = 440,
            Height = 160,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lbl = new Label { Left = 20, Top = 18, Text = prompt, Width = 390 };
        var txt = new TextBox { Left = 20, Top = 45, Width = 380, Text = defaultVal };
        var btnOk = new Button { Text = Localization.Get("BtnCommonAdd"), Left = 210, Width = 90, Top = 80, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = Localization.Get("BtnCommonCancel"), Left = 310, Width = 90, Top = 80, DialogResult = DialogResult.Cancel };
        promptForm.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
        promptForm.AcceptButton = btnOk;
        promptForm.CancelButton = btnCancel;
        AppThemeHelper.ApplyTheme(promptForm, AppSettings.Current.IsDarkMode);

        if (promptForm.ShowDialog(FindForm()) == DialogResult.OK)
        {
            return txt.Text;
        }
        return null;
    }

    // 그래픽 유틸리티 함수
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Delete)
        {
            if (DeleteCurrentSelection())
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
        else if (e.KeyCode == Keys.F2 || e.KeyCode == Keys.Enter)
        {
            if (EditCurrentSelection())
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
        else if (e.KeyCode == Keys.Space)
        {
            if (ToggleCurrentSelectionEnabled())
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
        else if (e.KeyCode == Keys.Escape)
        {
            DeselectAll();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }
}
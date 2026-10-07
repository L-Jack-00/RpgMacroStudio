using System.Drawing;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

public partial class NodeCanvasControl
{
    public event Action<GraphNode>? PinGraphNodeEditRequested;

    private void ShowContextMenuAt(Point screenPt, PointF worldPt)
    {
        if (IsPinGraphMode)
        {
            ShowPinGraphContextMenu(screenPt, worldPt);
            return;
        }

        bool ko = Localization.IsKorean;

        // 1. 상태 노드 및 내부 요소 히트 검사
        foreach (var state in _profile.States.AsEnumerable().Reverse())
        {
            if (!_profile.NodePositions.TryGetValue(state, out var pos)) continue;
            var layout = CalculateStateLayout(state, pos);

            if (layout.Bounds.Contains(worldPt))
            {
                if (!SelectedStates.Contains(state))
                {
                    SelectedState = state;
                    SelectedStates.Clear();
                    SelectedStates.Add(state);
                }

                // A. 이벤트 내부 요소 검사
                foreach (var el in layout.Events)
                {
                    if (el.Bounds.Contains(worldPt))
                    {
                        SelectedEvent = el.Event;

                        // (1) 조건 칩 위 우클릭
                        foreach (var tc in el.TriggerChips)
                        {
                            if (tc.ChipBounds.Contains(worldPt))
                            {
                                SelectedTriggerChip = (el.Event, tc.Trigger);
                                SelectedActionChip = null;
                                var chipMenu = new ContextMenuStrip();
                                chipMenu.Items.Add(ko ? "✏️ 조건 수정" : "✏️ Edit Trigger", null, (s, e) => TriggerEditRequested?.Invoke(el.Event, tc.Trigger));
                                chipMenu.Items.Add(ko ? "🗑️ 조건 삭제" : "🗑️ Delete Trigger", null, (s, e) => TriggerDeleteRequested?.Invoke(el.Event, tc.Trigger));
                                chipMenu.Show(this, screenPt);
                                return;
                            }
                        }

                        // (2) 행동 칩 위 우클릭
                        foreach (var ac in el.ActionChips)
                        {
                            if (ac.ChipBounds.Contains(worldPt))
                            {
                                SelectedActionChip = (el.Event, ac.Action);
                                SelectedTriggerChip = null;
                                var chipMenu = new ContextMenuStrip();
                                chipMenu.Items.Add(ko ? "✏️ 행동 수정" : "✏️ Edit Action", null, (s, e) => ActionEditRequested?.Invoke(el.Event, ac.Action));
                                chipMenu.Items.Add(ko ? "🗑️ 행동 삭제" : "🗑️ Delete Action", null, (s, e) => ActionDeleteRequested?.Invoke(el.Event, ac.Action));
                                chipMenu.Show(this, screenPt);
                                return;
                            }
                        }

                        // (3) 이벤트 카드 본체 위 우클릭
                        var evtMenu = new ContextMenuStrip();
                        evtMenu.Items.Add(ko ? "➕ 조건 추가" : "➕ Add Trigger", null, (s, e) => TriggerAddRequested?.Invoke(el.Event));
                        evtMenu.Items.Add(ko ? "➕ 행동 추가" : "➕ Add Action", null, (s, e) => ActionAddRequested?.Invoke(el.Event));
                        evtMenu.Items.Add(ko ? "🔴 매크로 녹화" : "🔴 Record Macro", null, (s, e) => RecordMacroRequested?.Invoke(el.Event));
                        evtMenu.Items.Add(new ToolStripSeparator());
                        evtMenu.Items.Add(ko ? "🗑️ 이벤트 삭제" : "🗑️ Delete Event", null, (s, e) => EventDeleteRequested?.Invoke(state, el.Event));
                        evtMenu.Show(this, screenPt);
                        return;
                    }
                }

                // (4) 상태 노드 헤더/몸체 위 우클릭
                var stateMenu = new ContextMenuStrip();
                bool isFixed = MacroEventProfile.IsGlobalState(state) || MacroEventProfile.IsDefaultState(state);

                // 다중 노드 선택 상태인 경우 일괄 삭제 메뉴 제공
                var multiDeletable = SelectedStates
                    .Where(s => !MacroEventProfile.IsGlobalState(s) && !MacroEventProfile.IsDefaultState(s))
                    .ToList();

                if (multiDeletable.Count > 1)
                {
                    stateMenu.Items.Add(ko ? $"🗑️ 선택된 {multiDeletable.Count}개 상태 노드 일괄 삭제" : $"🗑️ Delete {multiDeletable.Count} Selected States", null, (s, e) => DeleteMultipleStatesRequested?.Invoke(multiDeletable));
                    stateMenu.Items.Add(new ToolStripSeparator());
                }

                var itemAddEvt = stateMenu.Items.Add(ko ? "➕ 새 이벤트 추가" : "➕ Add Event", null, (s, e) => AddEventToStateRequested?.Invoke(state));
                stateMenu.Items.Add(new ToolStripSeparator());

                var itemInit = stateMenu.Items.Add(ko ? "⭐ 시작 상태로 지정" : "⭐ Set as Initial State", null, (s, e) => SetInitialStateRequested?.Invoke(state));
                itemInit.Enabled = (!MacroEventProfile.IsGlobalState(state) && state != _profile.InitialState);

                var itemRename = stateMenu.Items.Add(ko ? "✏️ 상태 이름 변경" : "✏️ Rename State", null, (s, e) => RenameStateRequested?.Invoke(state));
                itemRename.Enabled = !isFixed;

                stateMenu.Items.Add(new ToolStripSeparator());

                var itemDelete = stateMenu.Items.Add(ko ? "🗑️ 상태 노드 삭제" : "🗑️ Delete State", null, (s, e) => DeleteStateRequested?.Invoke(state));
                itemDelete.Enabled = !isFixed;

                stateMenu.Show(this, screenPt);
                return;
            }
        }

        // 2. 캔버스 빈 공간 위 우클릭
        var canvasMenu = new ContextMenuStrip();
        canvasMenu.Items.Add(Localization.Get("MenuAddState"), null, (s, e) => AddNewStateRequested?.Invoke());
        canvasMenu.Items.Add(Localization.Get("MenuAddEvent"), null, (s, e) => 
        {
            string targetState = SelectedState ?? _profile.InitialState;
            AddEventToStateRequested?.Invoke(targetState);
        });
        canvasMenu.Items.Add(new ToolStripSeparator());
        canvasMenu.Items.Add(Localization.Get("MenuAutoLayout"), null, (s, e) => 
        {
            RecordUndoRequested?.Invoke(Localization.IsKorean ? "노드 자동 정렬" : "Auto Layout Nodes");
            AutoLayoutNodes();
        });
        canvasMenu.Items.Add(Localization.Get("MenuResetView"), null, (s, e) => ResetView());
        canvasMenu.Items.Add(new ToolStripSeparator());

        bool isExpanded = IsInspectorExpandedFunc?.Invoke() ?? true;
        var inspectorItem = new ToolStripMenuItem(ko ? "인스펙터" : "Inspector")
        {
            Checked = isExpanded,
            CheckOnClick = true
        };
        inspectorItem.Click += (s, e) => ToggleInspectorRequested?.Invoke();
        canvasMenu.Items.Add(inspectorItem);

        canvasMenu.Show(this, screenPt);
    }

    /// <summary>
    /// Delete 키 입력 시 현재 선택된 칩, 이벤트, 상태 노드(또는 핀그래프 노드)를 순차적으로 삭제합니다.
    /// </summary>
    public bool DeleteCurrentSelection()
    {
        if (IsPinGraphMode)
        {
            var target = SelectedGraphNode ?? (SelectedGraphNodeId != null ? _pinGraph.Nodes.FirstOrDefault(n => n.Id == SelectedGraphNodeId) : null);
            if (target != null)
            {
                DeletePinGraphNode(target);
                return true;
            }
            if (SelectedGraphNodeIds.Count > 0)
            {
                var id = SelectedGraphNodeIds.First();
                var node = _pinGraph.Nodes.FirstOrDefault(n => n.Id == id);
                if (node != null)
                {
                    DeletePinGraphNode(node);
                    return true;
                }
            }
            return false;
        }

        if (SelectedTriggerChip != null)
        {
            var target = SelectedTriggerChip.Value;
            SelectedTriggerChip = null;
            TriggerDeleteRequested?.Invoke(target.Event, target.Trigger);
            return true;
        }

        if (SelectedActionChip != null)
        {
            var target = SelectedActionChip.Value;
            SelectedActionChip = null;
            ActionDeleteRequested?.Invoke(target.Event, target.Action);
            return true;
        }

        if (SelectedEvent != null)
        {
            var evt = SelectedEvent;
            string state = SelectedState ?? evt.StateTag;
            SelectedEvent = null;
            EventDeleteRequested?.Invoke(state, evt);
            return true;
        }

        // 다중 노드 선택 상태인 경우 일괄 삭제
        var deletableStates = SelectedStates
            .Where(s => !MacroEventProfile.IsGlobalState(s) && !MacroEventProfile.IsDefaultState(s))
            .ToList();

        if (deletableStates.Count > 1)
        {
            DeleteMultipleStatesRequested?.Invoke(deletableStates);
            return true;
        }

        if (!string.IsNullOrEmpty(SelectedState) && !MacroEventProfile.IsGlobalState(SelectedState) && !MacroEventProfile.IsDefaultState(SelectedState))
        {
            string st = SelectedState;
            DeleteStateRequested?.Invoke(st);
            return true;
        }

        return false;
    }

    /// <summary>
    /// F2 또는 Enter 키 입력 시 현재 선택된 칩, 이벤트, 상태 노드(또는 핀그래프 노드)를 편집/이름변경합니다.
    /// </summary>
    public bool EditCurrentSelection()
    {
        if (IsPinGraphMode)
        {
            var target = SelectedGraphNode ?? (SelectedGraphNodeId != null ? _pinGraph.Nodes.FirstOrDefault(n => n.Id == SelectedGraphNodeId) : null);
            if (target != null)
            {
                PinGraphNodeEditRequested?.Invoke(target);
                return true;
            }
            return false;
        }

        if (SelectedTriggerChip != null)
        {
            var target = SelectedTriggerChip.Value;
            TriggerEditRequested?.Invoke(target.Event, target.Trigger);
            return true;
        }

        if (SelectedActionChip != null)
        {
            var target = SelectedActionChip.Value;
            ActionEditRequested?.Invoke(target.Event, target.Action);
            return true;
        }

        if (SelectedEvent != null)
        {
            EventEditRequested?.Invoke(SelectedEvent);
            return true;
        }

        if (!string.IsNullOrEmpty(SelectedState) && !MacroEventProfile.IsGlobalState(SelectedState))
        {
            RenameStateRequested?.Invoke(SelectedState);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Space 키 입력 시 현재 선택된 이벤트의 활성/비활성 여부를 토글합니다.
    /// </summary>
    public bool ToggleCurrentSelectionEnabled()
    {
        if (IsPinGraphMode)
        {
            var target = SelectedGraphNode ?? (SelectedGraphNodeId != null ? _pinGraph.Nodes.FirstOrDefault(n => n.Id == SelectedGraphNodeId) : null);
            if (target != null)
            {
                var evt = _profile.Events.FirstOrDefault(e => target.Id.Contains(e.Id));
                if (evt != null)
                {
                    evt.IsEnabled = !evt.IsEnabled;
                    ProfileModified?.Invoke();
                    _pinGraph = PinGraphConverter.FromProfile(_profile);
                    Invalidate();
                    return true;
                }
            }
            return false;
        }

        MacroEvent? targetEvt = null;
        if (SelectedTriggerChip != null)
        {
            targetEvt = SelectedTriggerChip.Value.Event;
        }
        else if (SelectedActionChip != null)
        {
            targetEvt = SelectedActionChip.Value.Event;
        }
        else if (SelectedEvent != null)
        {
            targetEvt = SelectedEvent;
        }

        if (targetEvt != null)
        {
            targetEvt.IsEnabled = !targetEvt.IsEnabled;
            ProfileModified?.Invoke();
            Invalidate();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Esc 키 입력 시 모든 선택(상태, 이벤트, 칩, 핀 노드 등)을 안전하게 해제합니다.
    /// </summary>
    public void DeselectAll()
    {
        SelectedState = null;
        SelectedStates.Clear();
        SelectedEvent = null;
        SelectedTriggerChip = null;
        SelectedActionChip = null;
        SelectedGraphNode = null;
        SelectedGraphNodeId = null;
        SelectedGraphNodeIds.Clear();
        GraphNodeSelected?.Invoke(null);
        Invalidate();
    }

    /// <summary>
    /// 핀 그래프의 특정 노드를 삭제하고 관련된 이벤트 데이터 및 뷰를 동기화합니다.
    /// </summary>
    public void DeletePinGraphNode(GraphNode hitNode)
    {
        RecordUndoRequested?.Invoke(Localization.IsKorean ? $"핀 노드 삭제: [{hitNode.Title}]" : $"Delete Pin Node: [{hitNode.Title}]");
        var evt = _profile.Events.FirstOrDefault(e => hitNode.Id.Contains(e.Id));
        if (evt != null)
        {
            if (hitNode.TriggerData != null)
            {
                evt.Triggers.Remove(hitNode.TriggerData);
            }
            else if (hitNode.ActionData != null)
            {
                evt.Actions.Remove(hitNode.ActionData);
            }
            if (evt.Triggers.Count == 0 && evt.Actions.Count == 0)
            {
                _profile.Events.Remove(evt);
            }
        }
        else if (hitNode.NodeType == GraphNodeType.Start && !string.IsNullOrEmpty(hitNode.StateTag) &&
                 !MacroEventProfile.IsGlobalState(hitNode.StateTag) && !MacroEventProfile.IsDefaultState(hitNode.StateTag))
        {
            _profile.States.Remove(hitNode.StateTag);
            _profile.Events.RemoveAll(e => e.StateTag == hitNode.StateTag);
        }

        PinGraph.RemoveNode(hitNode.Id);
        _profile.PinGraphNodePositions.Remove(hitNode.Id);
        _profile.PinGraphNodeSizes.Remove(hitNode.Id);
        SelectedGraphNode = null;
        SelectedGraphNodeId = null;
        SelectedGraphNodeIds.Clear();
        GraphNodeSelected?.Invoke(null);
        _pinGraph = PinGraphConverter.FromProfile(_profile);
        Invalidate();
        ProfileModified?.Invoke();
    }

    private void ShowPinGraphContextMenu(Point screenPt, PointF worldPt)
    {
        bool ko = Localization.IsKorean;
        var hitNode = HitTestNode(worldPt);
        var cms = new ContextMenuStrip();

        if (hitNode != null)
        {
            SelectedGraphNode = hitNode;
            SelectedGraphNodeId = hitNode.Id;
            GraphNodeSelected?.Invoke(hitNode);
            Invalidate();

            // 1. 노드 편집 (삭제 버튼 위에 추가)
            cms.Items.Add(ko ? $"✏️ 노드 [{hitNode.Title}] 속성/편집" : $"✏️ Edit Node [{hitNode.Title}]", null, (s, ev) =>
            {
                PinGraphNodeEditRequested?.Invoke(hitNode);
            });

            cms.Items.Add(new ToolStripSeparator());

            // 2. 노드 삭제
            cms.Items.Add(ko ? $"🗑️ 노드 [{hitNode.Title}] 삭제" : $"🗑️ Delete Node [{hitNode.Title}]", null, (s, ev) =>
            {
                DeletePinGraphNode(hitNode);
            });
        }
        else
        {
            cms.Items.Add(ko ? "➕ 새 조건 감지 노드 추가" : "➕ Add Condition Node", null, (s, ev) =>
            {
                AddPinGraphConditionNode(SelectedState, worldPt);
            });

            cms.Items.Add(ko ? "➕ 새 마우스 클릭 노드 추가" : "➕ Add Mouse Click Node", null, (s, ev) =>
            {
                AddPinGraphActionNode(ActionType.MouseClick, SelectedState, worldPt);
            });

            cms.Items.Add(ko ? "➕ 새 키보드 입력 노드 추가" : "➕ Add Key Press Node", null, (s, ev) =>
            {
                AddPinGraphActionNode(ActionType.KeyPress, SelectedState, worldPt);
            });

            cms.Items.Add(ko ? "🔄 새 상태 전환 노드 추가" : "🔄 Add Change State Node", null, (s, ev) =>
            {
                AddPinGraphActionNode(ActionType.ChangeState, SelectedState, worldPt);
            });

            cms.Items.Add(new ToolStripSeparator());

            cms.Items.Add(Localization.Get("MenuAutoLayout"), null, (s, ev) => AutoLayoutNodes());
            cms.Items.Add(Localization.Get("MenuResetView"), null, (s, ev) => ResetView());

            cms.Items.Add(new ToolStripSeparator());

            bool isExpanded = IsInspectorExpandedFunc?.Invoke() ?? true;
            var inspectorItem = new ToolStripMenuItem(ko ? "인스펙터" : "Inspector")
            {
                Checked = isExpanded,
                CheckOnClick = true
            };
            inspectorItem.Click += (s, ev) => ToggleInspectorRequested?.Invoke();
            cms.Items.Add(inspectorItem);
        }

        cms.Show(this, screenPt);
    }

}
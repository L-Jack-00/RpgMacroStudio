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
    private void InitCanvasContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Localization.Get("MenuAddState"), null, (s, e) => AddNewStateNode());
        menu.Items.Add(Localization.Get("MenuAddEvent"), null, (s, e) => AddNewEventToSelectedState());
        menu.Items.Add(new ToolStripSeparator());

        var copyItem = new ToolStripMenuItem(Localization.IsKorean ? "📋 노드/이벤트 복사 (Ctrl+C)" : "📋 Copy Selection (Ctrl+C)", null, (s, e) => CopyCurrentSelectionToClipboard());
        var pasteItem = new ToolStripMenuItem(Localization.IsKorean ? "📥 노드/이벤트 붙여넣기 (Ctrl+V)" : "📥 Paste (Ctrl+V)", null, (s, e) => PasteFromClipboard());
        menu.Items.Add(copyItem);
        menu.Items.Add(pasteItem);
        menu.Items.Add(new ToolStripSeparator());

        var toggleEnableItem = new ToolStripMenuItem(Localization.IsKorean ? "⚡ 이벤트 활성 / 비활성화 (Space)" : "⚡ Toggle Event Enabled (Space)");
        toggleEnableItem.Click += (s, e) =>
        {
            if (IsClassicMode ? _classicControl.ToggleCurrentSelectionEnabled() : _canvas.ToggleCurrentSelectionEnabled())
            {
                OnCanvasSelectionChanged(_canvas.SelectedState, _canvas.SelectedEvent);
            }
        };
        menu.Items.Add(toggleEnableItem);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(Localization.Get("MenuAutoLayout"), null, (s, e) => 
        {
            RecordUndo(Localization.IsKorean ? "노드 자동 정렬" : "Auto Layout");
            _canvas.AutoLayoutNodes();
        });
        menu.Items.Add(Localization.Get("MenuResetView"), null, (s, e) => _canvas.ResetView());
        menu.Items.Add(new ToolStripSeparator());

        var inspectorItem = new ToolStripMenuItem(Localization.IsKorean ? "인스펙터" : "Inspector")
        {
            Checked = !_isInspectorCollapsed,
            CheckOnClick = true
        };
        inspectorItem.Click += (s, e) => SetInspectorCollapsed(!inspectorItem.Checked);
        menu.Items.Add(inspectorItem);

        _canvas.ContextMenuStrip = menu;
    }

    private void OnCanvasSelectionChanged(string? stateName, MacroEvent? evt)
    {
        // 녹화 진행 중일 때는 캔버스 빈 공간 클릭 등으로 녹화 대상 이벤트 선택이 풀리는 것을 방지
        if (_macroRecorder.IsRecording && _recordingTargetEvent != null)
        {
            if (evt == null && string.IsNullOrEmpty(stateName))
            {
                return;
            }
        }

        _isUpdatingUi = true;
        try
        {
            if (!IsClassicMode)
            {
                _canvas.SelectedState = stateName;
                _canvas.SelectedEvent = evt;
            }

            bool hasState = !string.IsNullOrEmpty(stateName);
            bool isFixedState = !hasState || MacroEventProfile.IsGlobalState(stateName) || MacroEventProfile.IsDefaultState(stateName);

            _txtStateName.Text = hasState ? (isFixedState ? MacroEventProfile.FormatStateDisplay(stateName, Localization.IsKorean) : stateName) : "";
            _txtStateName.Enabled = hasState && !isFixedState;
            _btnDeleteState.Enabled = hasState && !isFixedState;
            _btnSetInitial.Enabled = hasState && !MacroEventProfile.IsGlobalState(stateName) && stateName != _profile.InitialState;

            bool hasEvt = (evt != null);
            _txtEventName.Enabled = hasEvt;
            _chkEventEnabled.Enabled = hasEvt;
            _chkParallel.Enabled = hasEvt;
            _sliderHumanizerDelay.Enabled = hasEvt;
            _btnAddTrigger.Enabled = hasEvt;
            _btnEditTrigger.Enabled = hasEvt;
            _btnDeleteTrigger.Enabled = hasEvt;
            _btnAddAction.Enabled = hasEvt;
            _btnEditAction.Enabled = hasEvt;
            _btnDeleteAction.Enabled = hasEvt;
            _btnMoveUpAction.Enabled = hasEvt;
            _btnMoveDownAction.Enabled = hasEvt;
            _btnRecordMacro.Enabled = (_macroRecorder.IsRecording || hasEvt) && !_isMacroRunning;

            if (!hasEvt)
            {
                _txtEventName.Text = "";
                _chkEventEnabled.Checked = true;
                _chkParallel.Checked = false;
                _sliderHumanizerDelay.Value = 27;
                _lblHumanizerDelay.Text = string.Format(Localization.Get("LblHumanizerDelay"), 27);
                _lvTriggers.Items.Clear();
                _lvActions.Items.Clear();
            }
            else
            {
                _txtEventName.Text = MacroEventProfile.FormatEventDisplay(evt, Localization.IsKorean);
                _chkEventEnabled.Checked = evt!.IsEnabled;
                _chkParallel.Checked = evt!.IsParallel;
                int jitterVal = Math.Clamp(evt.HumanizerJitterMs, 0, 54);
                _sliderHumanizerDelay.Value = jitterVal;
                _lblHumanizerDelay.Text = string.Format(Localization.Get("LblHumanizerDelay"), jitterVal);
                RefreshTriggerListView(evt, syncTree: false);
                RefreshActionListView(evt, syncTree: false);
            }
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void RefreshTriggerListView(MacroEvent evt, bool syncTree = true)
    {
        _lvTriggers.Items.Clear();
        for (int i = 0; i < evt.Triggers.Count; i++)
        {
            var tr = evt.Triggers[i];
            var item = new ListViewItem((i + 1).ToString());
            item.SubItems.Add(tr.Type.ToString());
            item.SubItems.Add(tr.GetSummary());
            item.Tag = tr;
            _lvTriggers.Items.Add(item);
        }
        if (syncTree) _classicControl.RefreshEventInTree(evt);
    }

    private void RefreshActionListView(MacroEvent evt, bool syncTree = true)
    {
        _lvActions.Items.Clear();
        for (int i = 0; i < evt.Actions.Count; i++)
        {
            var a = evt.Actions[i];
            var item = new ListViewItem((i + 1).ToString());
            item.SubItems.Add(a.Type.ToString());
            item.SubItems.Add(a.Description);
            item.Tag = a;
            _lvActions.Items.Add(item);
        }
        if (syncTree) _classicControl.RefreshEventInTree(evt);
    }

    private void OnPinGraphNodeSelected(GraphNode? node)
    {
        if (node == null)
        {
            OnCanvasSelectionChanged(null, null);
            return;
        }

        if (node.NodeType == GraphNodeType.Start)
        {
            OnCanvasSelectionChanged(node.StateTag, null);
            return;
        }

        if (node.NodeType == GraphNodeType.Condition && node.TriggerData != null)
        {
            var targetEvt = _profile.Events.FirstOrDefault(e => e.Triggers.Contains(node.TriggerData));
            if (targetEvt == null && node.Id.StartsWith("trig_"))
            {
                var parts = node.Id.Split('_');
                if (parts.Length >= 2)
                {
                    string evtId = parts[1];
                    targetEvt = _profile.Events.FirstOrDefault(e => e.Id == evtId);
                }
            }

            if (targetEvt != null)
            {
                OnCanvasSelectionChanged(targetEvt.StateTag, targetEvt);

                // 발동 조건 목록에서 해당 트리거 항목을 자동 선택 및 포커스
                for (int i = 0; i < _lvTriggers.Items.Count; i++)
                {
                    if (_lvTriggers.Items[i].Tag == node.TriggerData)
                    {
                        _lvTriggers.Items[i].Selected = true;
                        _lvTriggers.Items[i].Focused = true;
                        _lvTriggers.Items[i].EnsureVisible();
                        break;
                    }
                }
            }
            else
            {
                OnCanvasSelectionChanged(node.StateTag, null);
            }
            return;
        }

        if ((node.NodeType == GraphNodeType.Action || node.NodeType == GraphNodeType.ChangeState || node.NodeType == GraphNodeType.VariableControl) && node.ActionData != null)
        {
            var targetEvt = _profile.Events.FirstOrDefault(e => e.Actions.Contains(node.ActionData));
            if (targetEvt == null && node.Id.StartsWith("act_"))
            {
                var parts = node.Id.Split('_');
                if (parts.Length >= 2)
                {
                    string evtId = parts[1];
                    targetEvt = _profile.Events.FirstOrDefault(e => e.Id == evtId);
                }
            }

            if (targetEvt != null)
            {
                OnCanvasSelectionChanged(targetEvt.StateTag, targetEvt);

                // 실행 행동 목록에서 해당 액션 항목을 자동 선택 및 포커스
                for (int i = 0; i < _lvActions.Items.Count; i++)
                {
                    if (_lvActions.Items[i].Tag == node.ActionData)
                    {
                        _lvActions.Items[i].Selected = true;
                        _lvActions.Items[i].Focused = true;
                        _lvActions.Items[i].EnsureVisible();
                        break;
                    }
                }
            }
            else
            {
                OnCanvasSelectionChanged(node.StateTag, null);
            }
            return;
        }

        OnCanvasSelectionChanged(node.StateTag, null);
    }

    // -------------------------------------------------------------
    // ComfyUI / Shader Graph 스타일 노드 인라인 조작 핸들러들
    // -------------------------------------------------------------
    private void EditSpecificTrigger(MacroEvent evt, MacroTrigger trigger)
    {
        using var dlg = new TriggerEditDialog(trigger);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            RecordUndo(Localization.IsKorean ? $"조건 수정 ({dlg.TriggerData.Type})" : $"Edit Trigger ({dlg.TriggerData.Type})");
            _isDirty = true;
            RefreshTriggerListView(evt);
            SyncAndRefreshAllViews();
        }
    }

    private void AddSpecificTrigger(MacroEvent evt)
    {
        using var dlg = new TriggerEditDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            RecordUndo(Localization.IsKorean ? $"조건 추가 ({dlg.TriggerData.Type})" : $"Add Trigger ({dlg.TriggerData.Type})");
            evt.Triggers.Add(dlg.TriggerData);
            _isDirty = true;
            RefreshTriggerListView(evt);
            SyncAndRefreshAllViews();
        }
    }

    private void DeleteSpecificTrigger(MacroEvent evt, MacroTrigger trigger)
    {
        if (evt.Triggers.Count <= 1)
        {
            MessageBox.Show(Localization.Get("ErrMinTriggers"), Localization.Get("TitleNotice"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        RecordUndo(Localization.IsKorean ? $"조건 삭제 ({trigger.Type})" : $"Delete Trigger ({trigger.Type})");
        evt.Triggers.Remove(trigger);
        _isDirty = true;
        RefreshTriggerListView(evt);
        SyncAndRefreshAllViews();
    }

    private void EditSpecificAction(MacroEvent evt, MacroAction action)
    {
        using var dlg = new ActionEditDialog(action);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            RecordUndo(Localization.IsKorean ? $"행동 수정 ({dlg.ActionData.Type})" : $"Edit Action ({dlg.ActionData.Type})");
            _isDirty = true;
            RefreshActionListView(evt);
            SyncAndRefreshAllViews();
        }
    }

    private void AddSpecificAction(MacroEvent evt)
    {
        using var dlg = new ActionEditDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            RecordUndo(Localization.IsKorean ? $"행동 추가 ({dlg.ActionData.Type})" : $"Add Action ({dlg.ActionData.Type})");
            evt.Actions.Add(dlg.ActionData);
            _isDirty = true;
            RefreshActionListView(evt);
            SyncAndRefreshAllViews();
        }
    }

    private void DeleteSpecificAction(MacroEvent evt, MacroAction action)
    {
        RecordUndo(Localization.IsKorean ? $"행동 삭제 ({action.Type})" : $"Delete Action ({action.Type})");
        evt.Actions.Remove(action);
        _isDirty = true;
        RefreshActionListView(evt);
        SyncAndRefreshAllViews();
    }

    private void DeleteSpecificEvent(string state, MacroEvent evt)
    {
        string prompt = Localization.IsKorean ? $"'{evt.Name}' 이벤트를 정말 삭제하시겠습니까?" : $"Are you sure you want to delete event '{evt.Name}'?";
        if (MessageBox.Show(this, prompt, Localization.Get("TitleConfirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            RecordUndo(Localization.IsKorean ? $"이벤트 '{evt.Name}' 삭제" : $"Delete Event '{evt.Name}'");
            _profile.Events.Remove(evt);
            _isDirty = true;
            SyncAndRefreshAllViews();
            OnCanvasSelectionChanged(state, null);
            Log(Localization.IsKorean ? $"🗑️ 이벤트 삭제됨: [{evt.Name}]" : $"🗑️ Event deleted: [{evt.Name}]");
        }
    }

    private void AddEventToSpecificState(string state)
    {
        string stateDisplay = MacroEventProfile.FormatStateDisplay(state, Localization.IsKorean);
        RecordUndo(Localization.IsKorean ? $"'{stateDisplay}'에 이벤트 추가" : $"Add Event to '{stateDisplay}'");
        string stateTag = MacroEventProfile.IsGlobalState(state) ? "Global" : state;
        int evtCount = _profile.Events.Count(e => (MacroEventProfile.IsGlobalState(e.StateTag) && MacroEventProfile.IsGlobalState(state)) || e.StateTag == state) + 1;
        var newEvt = new MacroEvent
        {
            Name = Localization.IsKorean
                ? $"{stateDisplay} 이벤트 {evtCount}"
                : $"{stateDisplay} Event {evtCount}",
            IsEnabled = true,
            IsParallel = true,
            StateTag = stateTag,
            Triggers = new List<MacroTrigger> { new MacroTrigger { Type = TriggerType.Always } }
        };

        _profile.Events.Add(newEvt);
        _isDirty = true;
        SyncAndRefreshAllViews();
        OnCanvasSelectionChanged(state, newEvt);
        Log(Localization.IsKorean
            ? $"⚡ 새 이벤트 추가됨: [{newEvt.Name}] (소속: '{stateDisplay}')"
            : $"⚡ New event added: [{newEvt.Name}] (State: '{stateDisplay}')");
    }


    private void AddTrigger()
    {
        if (CurrentSelectedEvent == null) return;
        AddSpecificTrigger(CurrentSelectedEvent);
    }

    private void EditSelectedTrigger()
    {
        if (CurrentSelectedEvent == null || _lvTriggers.SelectedItems.Count == 0) return;
        var tr = _lvTriggers.SelectedItems[0].Tag as MacroTrigger;
        if (tr != null) EditSpecificTrigger(CurrentSelectedEvent, tr);
    }

    private void DeleteSelectedTrigger()
    {
        if (CurrentSelectedEvent == null || _lvTriggers.SelectedItems.Count == 0) return;
        var tr = _lvTriggers.SelectedItems[0].Tag as MacroTrigger;
        if (tr != null) DeleteSpecificTrigger(CurrentSelectedEvent, tr);
    }

    private void AddAction()
    {
        if (CurrentSelectedEvent == null) return;
        AddSpecificAction(CurrentSelectedEvent);
    }

    private void EditSelectedAction()
    {
        if (CurrentSelectedEvent == null || _lvActions.SelectedItems.Count == 0) return;
        var act = _lvActions.SelectedItems[0].Tag as MacroAction;
        if (act != null) EditSpecificAction(CurrentSelectedEvent, act);
    }

    private void DeleteSelectedAction()
    {
        if (CurrentSelectedEvent == null || _lvActions.SelectedItems.Count == 0) return;
        var act = _lvActions.SelectedItems[0].Tag as MacroAction;
        if (act != null) DeleteSpecificAction(CurrentSelectedEvent, act);
    }

    private void MoveAction(int delta)
    {
        if (CurrentSelectedEvent == null || _lvActions.SelectedItems.Count == 0) return;
        int idx = _lvActions.SelectedIndices[0];
        int targetIdx = idx + delta;
        if (targetIdx < 0 || targetIdx >= CurrentSelectedEvent.Actions.Count) return;

        RecordUndo(Localization.IsKorean ? "행동 순서 변경" : "Move Action");
        var temp = CurrentSelectedEvent.Actions[idx];
        CurrentSelectedEvent.Actions[idx] = CurrentSelectedEvent.Actions[targetIdx];
        CurrentSelectedEvent.Actions[targetIdx] = temp;

        _isDirty = true;
        RefreshActionListView(CurrentSelectedEvent);
        _lvActions.Items[targetIdx].Selected = true;
        SyncAndRefreshAllViews();
    }


    private void AddNewStateNode()
    {
        string prompt = Localization.Get("PromptAddStateDesc");
        using var promptForm = new Form
        {
            Width = 440,
            Height = 160,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = Localization.Get("PromptAddStateTitle"),
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lbl = new Label { Left = 20, Top = 18, Text = prompt, Width = 390 };
        string suggestedState = _profile.GenerateNextStateName(Localization.IsKorean);
        var txt = new TextBox { Left = 20, Top = 45, Width = 380, Text = suggestedState };
        var btnOk = new Button { Text = Localization.Get("BtnCommonAdd"), Left = 210, Width = 90, Top = 80, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = Localization.Get("BtnCommonCancel"), Left = 310, Width = 90, Top = 80, DialogResult = DialogResult.Cancel };
        promptForm.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
        promptForm.AcceptButton = btnOk;
        promptForm.CancelButton = btnCancel;
        AppThemeHelper.ApplyTheme(promptForm, AppSettings.Current.IsDarkMode);

        if (promptForm.ShowDialog(this) == DialogResult.OK)
        {
            string baseState = txt.Text.Trim();
            if (string.IsNullOrEmpty(baseState)) return;

            string newState = _profile.ResolveUniqueStateName(baseState, Localization.IsKorean);

            RecordUndo(Localization.IsKorean ? $"새 상태 노드 추가 [{newState}]" : $"Add State Node [{newState}]");
            _profile.States.Add(newState);
            // 기존 노드들과 겹치지 않는 안전한 스마트 위치 탐색
            var safePos = MacroLayoutEngine.FindSafeSpawnPosition(_profile);
            _profile.NodePositions[newState] = safePos;

            _isDirty = true;
            SyncAndRefreshAllViews();
            Log(Localization.IsKorean ? $"새 상태 노드 추가됨: [{newState}] (위치: {safePos.X:F0}, {safePos.Y:F0})" : $"New state node added: [{newState}]");
        }
    }

    private void AddNewEventToSelectedState()
    {
        // 현재 활성화된 뷰에서 선택된 상태를 대상으로 이벤트 추가
        string targetState;
        if (IsClassicMode)
        {
            targetState = _classicControl.SelectedState ?? _profile.InitialState;
        }
        else
        {
            targetState = _canvas.SelectedState ?? _profile.InitialState;
        }
        AddEventToSpecificState(targetState);
    }

    private void CommitStateRenameFromTextBox()
    {
        if (_isUpdatingUi) return;
        string? oldName = CurrentSelectedState;
        if (string.IsNullOrEmpty(oldName) || MacroEventProfile.IsGlobalState(oldName) || MacroEventProfile.IsDefaultState(oldName))
        {
            return;
        }

        string newName = _txtStateName.Text.Trim();
        if (string.IsNullOrEmpty(newName) || newName == oldName)
        {
            _txtStateName.Text = oldName;
            return;
        }

        if (_profile.States.Contains(newName))
        {
            MessageBox.Show(Localization.IsKorean ? $"이미 '{newName}' 상태가 존재합니다." : $"State '{newName}' already exists.", Localization.Get("TitleDuplicateError"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtStateName.Text = oldName;
            return;
        }

        ExecuteStateRename(oldName, newName);
    }

    private void RenameState(string? stateName)
    {
        if (string.IsNullOrEmpty(stateName) || MacroEventProfile.IsGlobalState(stateName) || MacroEventProfile.IsDefaultState(stateName)) return;

        using var promptForm = new Form
        {
            Width = 440,
            Height = 160,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = Localization.Get("PromptRenameTitle"),
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lbl = new Label { Left = 20, Top = 18, Text = string.Format(Localization.Get("PromptRenameDesc"), stateName), Width = 390 };
        var txt = new TextBox { Left = 20, Top = 45, Width = 380, Text = stateName };
        var btnOk = new Button { Text = Localization.Get("BtnCommonApply"), Left = 210, Width = 90, Top = 80, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = Localization.Get("BtnCommonCancel"), Left = 310, Width = 90, Top = 80, DialogResult = DialogResult.Cancel };
        promptForm.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
        promptForm.AcceptButton = btnOk;
        promptForm.CancelButton = btnCancel;
        AppThemeHelper.ApplyTheme(promptForm, AppSettings.Current.IsDarkMode);

        if (promptForm.ShowDialog(this) == DialogResult.OK)
        {
            string newName = txt.Text.Trim();
            if (string.IsNullOrEmpty(newName) || newName == stateName) return;

            if (_profile.States.Contains(newName))
            {
                MessageBox.Show(Localization.IsKorean ? $"이미 '{newName}' 상태가 존재합니다." : $"State '{newName}' already exists.", Localization.Get("TitleDuplicateError"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ExecuteStateRename(stateName, newName);
        }
    }

    private void ExecuteStateRename(string oldName, string newName)
    {
        RecordUndo(Localization.IsKorean ? $"상태 이름 변경 [{oldName}] ➔ [{newName}]" : $"Rename State [{oldName}] ➔ [{newName}]");
        int idx = _profile.States.IndexOf(oldName);
        if (idx >= 0) _profile.States[idx] = newName;

        if (_profile.NodePositions.TryGetValue(oldName, out var p))
        {
            _profile.NodePositions.Remove(oldName);
            _profile.NodePositions[newName] = p;
        }

        foreach (var evt in _profile.Events)
        {
            if (evt.StateTag == oldName) evt.StateTag = newName;
            foreach (var act in evt.Actions)
            {
                if (act.Type == ActionType.ChangeState && act.TargetState == oldName)
                {
                    act.TargetState = newName;
                    act.Description = $"상태 전환: [{newName}]";
                }
            }
        }

        if (_profile.InitialState == oldName) _profile.InitialState = newName;

        _isDirty = true;
        _canvas.SelectState(newName);
        SyncAndRefreshAllViews();
        OnCanvasSelectionChanged(newName, CurrentSelectedEvent);
        Log(Localization.IsKorean ? $"상태 이름 변경됨: [{oldName}] ➔ [{newName}]" : $"State renamed: [{oldName}] ➔ [{newName}]");
    }

    private void SetSelectedAsInitialState()
    {
        SetSpecificAsInitialState(CurrentSelectedState);
    }

    private void SetSpecificAsInitialState(string? stateName)
    {
        if (string.IsNullOrEmpty(stateName) || MacroEventProfile.IsGlobalState(stateName)) return;

        RecordUndo(Localization.IsKorean ? $"초기 시작 상태 설정 [{stateName}]" : $"Set Initial State [{stateName}]");
        _profile.InitialState = stateName;
        _isDirty = true;
        SyncAndRefreshAllViews();
        OnCanvasSelectionChanged(stateName, CurrentSelectedEvent);
        Log(Localization.IsKorean ? $"초기 시작 상태가 [{_profile.InitialState}]로 변경되었습니다." : $"Initial state set to [{_profile.InitialState}].");
    }

    private void DeleteSelectedState()
    {
        DeleteSpecificState(CurrentSelectedState);
    }

    private void DeleteSpecificState(string? stateName)
    {
        if (string.IsNullOrEmpty(stateName) || MacroEventProfile.IsGlobalState(stateName) || MacroEventProfile.IsDefaultState(stateName)) return;

        string prompt = string.Format(Localization.Get("PromptDeleteStateDesc"), stateName);
        if (MessageBox.Show(this, prompt, Localization.Get("PromptDeleteStateTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            RecordUndo(Localization.IsKorean ? $"상태 삭제 [{stateName}]" : $"Delete State [{stateName}]");
            _profile.States.Remove(stateName);
            _profile.NodePositions.Remove(stateName);
            _profile.Events.RemoveAll(e => e.StateTag == stateName);

            foreach (var evt in _profile.Events)
            {
                evt.Actions.RemoveAll(a => a.Type == ActionType.ChangeState && a.TargetState == stateName);
            }

            if (_profile.InitialState == stateName)
            {
                _profile.InitialState = _profile.States.FirstOrDefault(s => !MacroEventProfile.IsGlobalState(s)) ?? "기본 상태";
            }

            _isDirty = true;
            SyncAndRefreshAllViews();
            OnCanvasSelectionChanged(null, null);
            Log(Localization.IsKorean ? $"상태 [{stateName}] 및 관련 이벤트가 삭제되었습니다." : $"State [{stateName}] deleted.");
        }
    }

    private void DeleteMultipleStates(List<string> states)
    {
        var targetStates = states
            .Where(s => !MacroEventProfile.IsGlobalState(s) && !MacroEventProfile.IsDefaultState(s))
            .ToList();
        if (targetStates.Count == 0) return;

        string prompt = Localization.IsKorean
            ? $"선택된 {targetStates.Count}개의 상태 노드를 삭제하시겠습니까?\n등록된 모든 이벤트도 함께 제거됩니다."
            : $"Are you sure you want to delete {targetStates.Count} selected state nodes?\nAll registered events will also be removed.";

        if (MessageBox.Show(this, prompt, Localization.Get("PromptDeleteStateTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            RecordUndo(Localization.IsKorean ? $"{targetStates.Count}개 상태 일괄 삭제" : $"Delete {targetStates.Count} States");
            foreach (var st in targetStates)
            {
                _profile.States.Remove(st);
                _profile.NodePositions.Remove(st);
                _profile.Events.RemoveAll(e => e.StateTag == st);
            }

            foreach (var evt in _profile.Events)
            {
                evt.Actions.RemoveAll(a => a.Type == ActionType.ChangeState && targetStates.Contains(a.TargetState ?? ""));
            }

            if (targetStates.Contains(_profile.InitialState))
            {
                _profile.InitialState = _profile.States.FirstOrDefault(s => !MacroEventProfile.IsGlobalState(s)) ?? "기본 상태";
            }

            _canvas.SelectedStates.Clear();
            _isDirty = true;
            SyncAndRefreshAllViews();
            OnCanvasSelectionChanged(null, null);
            Log(Localization.IsKorean ? $"{targetStates.Count}개의 상태 노드가 삭제되었습니다." : $"{targetStates.Count} states deleted.");
        }
    }

    private void OpenEventEditDialog(MacroEvent? evt)
    {
        if (evt == null) return;
        using var dlg = new EventEditDialog(evt, _profile.States);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            RecordUndo(Localization.IsKorean ? $"이벤트 '{evt.Name}' 편집" : $"Edit Event '{evt.Name}'");
            _isDirty = true;
            SyncAndRefreshAllViews();
            OnCanvasSelectionChanged(CurrentSelectedState, evt);
            Log(Localization.IsKorean ? $"이벤트 설정 수정 완료: [{evt.Name}]" : $"Event updated: [{evt.Name}]");
        }
    }


}

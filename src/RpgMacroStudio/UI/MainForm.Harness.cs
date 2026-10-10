using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Media;
using System.Windows.Forms;
using RpgMacroHarness.Core;
using RpgMacroHarness.Data;
using RpgMacroHarness.Input;
using RpgMacroHarness.Vision;

namespace RpgMacroHarness.UI;

public partial class MainForm
{
    private void InitGlobalHotkeys()
    {
        GlobalHotkeyService.OnRecordPressed += () =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { Invoke(new Action(ToggleMacroRecording)); } catch { }
        };
        GlobalHotkeyService.OnStartPausePressed += () =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { Invoke(new Action(HandleStartPause)); } catch { }
        };
        GlobalHotkeyService.OnStopPressed += () =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { Invoke(new Action(StopMacro)); } catch { }
        };
        GlobalHotkeyService.OnEmergencyStopPressed += () =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { Invoke(new Action(HandleEmergencyStop)); } catch { }
        };
        GlobalHotkeyService.Start();
    }


    private void StartMacroRecordingForEvent(MacroEvent evt)
    {
        OnCanvasSelectionChanged(evt.StateTag, evt);
        ToggleMacroRecording();
    }

    // -------------------------------------------------------------
    // 인스펙터 리스트뷰 기반 조작 핸들러들
    // -------------------------------------------------------------

    private void ToggleMacroRecording()
    {
        bool ko = Localization.IsKorean;

        if (_macroRecorder.IsRecording)
        {
            var recordedActions = _macroRecorder.StopRecording(true);
            _btnRecordMacro.Text = $"{Localization.Get("BtnRecord")} [{AppSettings.Current.RecordHotkey}]";
            _btnRecordMacro.BackColor = Color.FromArgb(231, 76, 60);

            if (CurrentSelectedEvent != null && recordedActions.Count > 0)
            {
                RecordUndo(Localization.IsKorean ? $"매크로 녹화 ({recordedActions.Count}개 입력)" : $"Macro Recording ({recordedActions.Count} inputs)");
                CurrentSelectedEvent.Actions.AddRange(recordedActions);
                _isDirty = true;
                RefreshActionListView(CurrentSelectedEvent);
                SyncAndRefreshAllViews();
                _classicControl.RefreshSelectedEventItem();
                _canvas.Update();
                Update();

                string msg = string.Format(Localization.Get("RecordingFinished"), recordedActions.Count);
                Log($"[매크로 녹화 완료] {msg}");
                MessageBox.Show(this, msg, ko ? "녹화 완료" : "Recording Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (recordedActions.Count == 0)
            {
                Log("[매크로 녹화 중지] 수집된 입력이 없습니다.");
            }
            _btnStart.Enabled = true;
        }
        else
        {
            if (_isMacroRunning)
            {
                string runWarn = Localization.IsKorean
                    ? "매크로가 실행 중일 때는 녹화를 시작할 수 없습니다. 매크로를 먼저 정지해주세요."
                    : "Cannot start recording while the macro is running. Please stop the macro first.";
                MessageBox.Show(this, runWarn, Localization.Get("TitleNotice"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_canvas.SelectedEvent == null)
            {
                MessageBox.Show(this,
                    ko ? "녹화한 입력을 추가할 이벤트를 캔버스 또는 목록에서 먼저 선택해주세요." : "Please select an event node to record actions for first.",
                    ko ? "이벤트 미선택" : "No Event Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Rectangle btnBounds = Rectangle.Inflate(_btnRecordMacro.RectangleToScreen(_btnRecordMacro.ClientRectangle), 6, 6);
            _macroRecorder.StartRecording(btnBounds, AppSettings.Current.RecordHotkey);
            _btnStart.Enabled = false;
            _btnRecordMacro.Text = $"{Localization.Get("BtnStopRecord")} [{AppSettings.Current.RecordHotkey}]";
            _btnRecordMacro.BackColor = Color.FromArgb(192, 57, 43);

            string hint = Localization.Get("RecordingStarted");
            Log($"[매크로 녹화 시작] {hint}");
        }
    }


    private void OpenProfile()
    {
        if (_isDirty)
        {
            var res = MessageBox.Show(Localization.Get("PromptUnsavedChanges"), Localization.Get("TitleUnsavedChanges"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (res == DialogResult.Yes) SaveProfile();
            else if (res == DialogResult.Cancel) return;
        }

        using var ofd = new OpenFileDialog
        {
            Filter = "FSM Macro Profile (*.json)|*.json|All Files (*.*)|*.*",
            Title = Localization.Get("BtnOpen")
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            var loaded = MacroEventProfileManager.LoadFromFile(ofd.FileName);
            if (loaded != null)
            {
                _profile = loaded;
                _canvas.Profile = _profile;
                _classicControl.Profile = _profile;
                _canvas.AutoLayoutNodes();
                OnCanvasSelectionChanged(_profile.InitialState, null);
                _isDirty = false;
                UpdateWindowTitle();
                Log(Localization.IsKorean ? $"프로필 로드 성공: {Path.GetFileName(ofd.FileName)}" : $"Profile loaded: {Path.GetFileName(ofd.FileName)}");
            }
        }
    }

    private void SaveProfile()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "FSM Macro Profile (*.json)|*.json|All Files (*.*)|*.*",
            Title = Localization.Get("BtnSave"),
            FileName = "node_macro_profile.json"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            MacroEventProfileManager.SaveToFile(sfd.FileName, _profile);
            _isDirty = false;
            Log(Localization.IsKorean ? $"프로필 저장 완료: {Path.GetFileName(sfd.FileName)}" : $"Profile saved: {Path.GetFileName(sfd.FileName)}");
            MessageBox.Show(Localization.Get("MsgSaveSuccess"), Localization.Get("TitleNotice"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }


    private void OpenSettings()
    {
        using var dlg = new SettingsForm();
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            if (dlg.LanguageChanged || dlg.HotkeysChanged)
            {
                ApplyLanguage();
            }
            if (dlg.ThemeChanged)
            {
                ApplyTheme(AppSettings.Current.IsDarkMode);
            }
            Log(Localization.IsKorean ? "설정이 저장 및 적용되었습니다." : "Settings applied.");
        }
    }

    private void ShowChangelog()
    {
        using var dlg = new ChangelogDialog();
        dlg.ShowDialog(this);
    }

    private void LoadDefaultProfile()
    {
        string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "event_profile.json");
        if (File.Exists(defaultPath))
        {
            try
            {
                var p = MacroEventProfileManager.LoadFromFile(defaultPath);
                if (p != null)
                {
                    _profile = p;
                    _canvas.Profile = _profile;
                    _classicControl.Profile = _profile;
                    _canvas.AutoLayoutNodes();
                    OnCanvasSelectionChanged(_profile.InitialState, null);
                    _isDirty = false;
                    UpdateWindowTitle();
                    return;
                }
            }
            catch
            {
                // 로드 실패 시 새 빈 프로필로 초기화
            }
        }

        _profile = MacroEventProfileManager.CreateDefaultProfile();
        _canvas.Profile = _profile;
        _classicControl.Profile = _profile;
        _canvas.AutoLayoutNodes();
        OnCanvasSelectionChanged(_profile.InitialState, null);
        _isDirty = false;
        UpdateWindowTitle();
    }



    private void HandleStartPause()
    {
        if (!_isMacroRunning)
        {
            StartMacro();
        }
        else if (_harness != null && _harness.IsPaused)
        {
            ResumeMacro();
        }
        else
        {
            PauseMacro();
        }
    }

    private void PauseMacro()
    {
        if (!_isMacroRunning || _harness == null || _harness.IsPaused) return;
        _harness.Pause();
        _btnStart.Text = Localization.Get("BtnResume");
        _btnStart.BackColor = Color.FromArgb(52, 152, 219);
        _lblStatus.Text = Localization.Get("StatusPaused");
        _lblStatus.ForeColor = Color.FromArgb(211, 84, 0);
        AppSettings.PlaySound(SystemSounds.Asterisk);
    }

    private void ResumeMacro()
    {
        if (!_isMacroRunning || _harness == null || !_harness.IsPaused) return;
        _harness.Resume();
        _btnStart.Text = Localization.Get("BtnPause");
        _btnStart.BackColor = Color.FromArgb(243, 156, 18);
        _lblStatus.Text = Localization.Get("StatusRunning");
        _lblStatus.ForeColor = Color.DarkGreen;
        AppSettings.PlaySound(SystemSounds.Asterisk);
    }

    private void HandleEmergencyStop()
    {
        EmergencyStop.Trigger();
        StopMacro();
        Log(Localization.IsKorean ? "🚨 [긴급 비상 정지] 단축키로 즉각 강제 중단되었습니다!" : "🚨 [Emergency Stop] Immediate forced halt via hotkey!");
    }

    private void StartMacro()
    {
        if (_isMacroRunning) return;

        if (_macroRecorder.IsRecording)
        {
            string recWarn = Localization.IsKorean
                ? "매크로 녹화가 진행 중일 때는 매크로를 실행할 수 없습니다. 녹화를 먼저 종료해주세요."
                : "Cannot start macro while recording is in progress. Please stop recording first.";
            MessageBox.Show(this, recWarn, Localization.Get("TitleNotice"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_profile.Events.Count == 0 || !_profile.Events.Any(e => e.IsEnabled))
        {
            MessageBox.Show(Localization.Get("PromptNoActiveEvents"), Localization.Get("TitleNotice"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            EmergencyStop.Reset();
            _harness = new EventMacroHarness(_profile);
            _harness.OnLog += Log;
            _harness.OnStateChanged += state =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        _lblActiveState.Text = $"{Localization.Get("ActiveStatePrefix")} [{MacroEventProfile.FormatStateDisplay(state, Localization.IsKorean)}]";
                        _canvas.ActiveState = state;
                    }));
                }
                else
                {
                    _lblActiveState.Text = $"{Localization.Get("ActiveStatePrefix")} [{MacroEventProfile.FormatStateDisplay(state, Localization.IsKorean)}]";
                    _canvas.ActiveState = state;
                }
            };
            _harness.OnActionExecuting += (evId, actIdx) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (_canvas.HighlightStartPinOnFirstRun)
                        {
                            _canvas.HighlightStartPinOnFirstRun = false;
                        }
                        _canvas.SetActiveAction(evId, actIdx);
                    }));
                }
            };
            _harness.OnTriggerFired += (evId, trgIdx) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (_canvas.HighlightStartPinOnFirstRun)
                        {
                            _canvas.HighlightStartPinOnFirstRun = false;
                        }
                        _canvas.NotifyTriggerFired(evId, trgIdx);
                    }));
                }
            };

            _isMacroRunning = true;
            _canvas.HighlightStartPinOnFirstRun = true;
            _canvas.Invalidate();
            _btnStart.Text = Localization.Get("BtnPause");
            _btnStart.BackColor = Color.FromArgb(243, 156, 18);
            _btnStart.Enabled = true;
            _btnStop.Enabled = true;
            _btnRecordMacro.Enabled = false;
            _lblStatus.Text = Localization.Get("StatusRunning");
            _lblStatus.ForeColor = Color.DarkGreen;
            _lblActiveState.Text = $"{Localization.Get("ActiveStatePrefix")} [{MacroEventProfile.FormatStateDisplay(_harness.CurrentState, Localization.IsKorean)}]";
            _canvas.ActiveState = _harness.CurrentState;

            AppSettings.PlaySound(SystemSounds.Asterisk);
            Log(Localization.IsKorean ? $"하네스 FSM 노드 엔진 가동 시작! (초기 상태: [{_harness.CurrentState}])" : $"Harness FSM Node Engine started! (Initial: [{_harness.CurrentState}])");

            _harnessThread = new Thread(() =>
            {
                _harness.Start();

                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    Invoke(new Action(() =>
                    {
                        _isMacroRunning = false;
                        _btnStart.Text = Localization.Get("BtnStart");
                        _btnStart.BackColor = Color.FromArgb(46, 204, 113);
                        _btnStart.Enabled = true;
                        _btnStop.Enabled = false;
                        _btnRecordMacro.Enabled = (CurrentSelectedEvent != null);
                        _lblStatus.Text = Localization.Get("StatusStopped");
                        _lblStatus.ForeColor = Color.DarkRed;
                        _canvas.ActiveState = null;
                        _canvas.HighlightStartPinOnFirstRun = false;
                        _canvas.Invalidate();
                        AppSettings.PlaySound(SystemSounds.Beep);
                        Log(Localization.IsKorean ? "하네스 루프가 종료되었습니다." : "Harness loop terminated.");
                    }));
                }
                catch { }
            })
            {
                IsBackground = true
            };

            _harnessThread.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"매크로 시작 실패: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopMacro()
    {
        if (!_isMacroRunning) return;
        _harness?.Stop();
        _isMacroRunning = false;
        _btnStart.Text = Localization.Get("BtnStart");
        _btnStart.BackColor = Color.FromArgb(46, 204, 113);
        _btnStart.Enabled = true;
        _btnStop.Enabled = false;
        _btnRecordMacro.Enabled = (CurrentSelectedEvent != null);
        _lblStatus.Text = Localization.Get("StatusStopped");
        _lblStatus.ForeColor = Color.DarkSlateGray;
        _canvas.ActiveState = null;
        _canvas.HighlightStartPinOnFirstRun = false;
        _canvas.Invalidate();
        AppSettings.PlaySound(SystemSounds.Beep);
        Log(Localization.IsKorean ? "사용자가 매크로를 정지했습니다." : "Macro stopped by user.");
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => Log(message)));
            return;
        }

        string cleanMessage = StripEmoji(message);
        string time = DateTime.Now.ToString("HH:mm:ss");
        _logBox.Items.Insert(0, $"[{time}] {cleanMessage}");
        if (_logBox.Items.Count > 100) _logBox.Items.RemoveAt(_logBox.Items.Count - 1);
        _logBox.Invalidate();
        _scrollLog?.UpdateMetrics();
    }

    private static string StripEmoji(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // 1. Surrogate Pair (UTF-16 서러게이트 쌍: 이모지 U+1F000 ~ U+1FAFF 등)
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
                continue;
            }

            // 2. 특수 기호/이모지 단일 코드포인트 필터링
            if (c == '↶' || c == '↷' || c == '☰' || c == '⚡' || c == '✨')
            {
                continue;
            }

            // 3. Misc Symbols & Dingbats (U+2600 ~ U+27BF) 중 이모지성 기호
            if (c >= 0x2600 && c <= 0x27BF)
            {
                if (c == 0x2794) // ➔
                {
                    sb.Append("->");
                    continue;
                }
                continue;
            }

            // 4. Variation Selector (U+FE0E, U+FE0F: 이모지 변형 선택자)
            if (c == 0xFE0E || c == 0xFE0F)
            {
                continue;
            }

            sb.Append(c);
        }

        string result = sb.ToString();
        while (result.Contains("  "))
        {
            result = result.Replace("  ", " ");
        }
        return result.Trim();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // 기본값: 오른쪽 UI 최소화 적용
        SetInspectorCollapsed(true);

        if (!AppSettings.Current.HasCompletedGetStarted)
        {
            BeginInvoke(new Action(ShowGetStartedGuide));
        }
    }

    private void ShowGetStartedGuide()
    {
        using var dlg = new GetStartedDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.OpenHelperWizardRequested)
        {
            ShowTemplateWizard();
        }
    }

    private void ShowTemplateWizard()
    {
        if (!AppSettings.Current.HasOpenedTemplateWizard)
        {
            AppSettings.Current.HasOpenedTemplateWizard = true;
            AppSettings.Save();
            ApplyTheme(_currentIsDark);
        }

        using var dlg = new TemplateWizardDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.GeneratedProfile != null)
        {
            if (_isDirty)
            {
                var res = MessageBox.Show(Localization.Get("PromptUnsavedChanges"), Localization.Get("TitleUnsavedChanges"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (res == DialogResult.Cancel) return;
                if (res == DialogResult.Yes) SaveProfile();
            }

            RecordUndo(Localization.IsKorean
                ? $"템플릿 적용 [{dlg.GeneratedProfile.ProfileName}]"
                : $"Apply Template [{dlg.GeneratedProfile.ProfileName}]");
            _profile = dlg.GeneratedProfile;
            _canvas.Profile = _profile;
            _classicControl.Profile = _profile;
            _canvas.AutoLayoutNodes();
            _canvas.ResetView();
            _isDirty = true;
            SyncAndRefreshAllViews();
            Log(Localization.IsKorean
                ? $"[도우미 마법사] '{_profile.ProfileName}' 템플릿이 성공적으로 로드되었습니다."
                : $"[Helper Wizard] '{_profile.ProfileName}' template loaded successfully.");
        }
    }

    private void ShowAiAssistantDialog()
    {
        using var dlg = new AiAssistantDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.GeneratedProfile != null)
        {
            var aiProfile = dlg.GeneratedProfile;
            bool hasExistingWork = _profile.Events.Count > 0 || _profile.States.Count(s => !MacroEventProfile.IsGlobalState(s)) > 1 || _isDirty;

            AiApplyChoice choice = AiApplyChoice.Replace;
            if (hasExistingWork)
            {
                using var optDlg = new AiApplyOptionDialog(_profile.Events.Count, _profile.States.Count);
                if (optDlg.ShowDialog(this) != DialogResult.OK || optDlg.Choice == AiApplyChoice.Cancel)
                {
                    return; // 취소 시 기존 상태 온전히 보존
                }
                choice = optDlg.Choice;
            }

            if (choice == AiApplyChoice.Merge)
            {
                MergeAiProfile(aiProfile);
                return;
            }

            // 덮어쓰기(Replace) 선택 시: 저장되지 않은 변경사항 확인
            if (_isDirty)
            {
                var res = MessageBox.Show(Localization.Get("PromptUnsavedChanges"), Localization.Get("TitleUnsavedChanges"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (res == DialogResult.Cancel) return;
                if (res == DialogResult.Yes) SaveProfile();
            }

            RecordUndo(Localization.IsKorean
                ? $"AI 프로필 교체 적용 [{aiProfile.ProfileName}]"
                : $"Apply AI Profile [{aiProfile.ProfileName}]");

            _profile = aiProfile;
            _canvas.Profile = _profile;

            if (CurrentViewMode == AppViewMode.CanvasPinGraph)
            {
                _canvas.AutoLayoutPinGraph();
            }
            else
            {
                _canvas.AutoLayoutNodes();
            }

            _canvas.ResetView();
            _isDirty = true;
            SyncAndRefreshAllViews();
            Log(Localization.IsKorean
                ? $"✨ [AI 어시스턴트] '{_profile.ProfileName}' 프로필이 생성되어 캔버스에 즉시 적용되었습니다."
                : $"✨ [AI Assistant] '{_profile.ProfileName}' profile generated and applied to canvas.");
        }
    }

    private void MergeAiProfile(MacroEventProfile aiProfile)
    {
        bool ko = Localization.IsKorean;
        RecordUndo(ko
            ? $"AI 프로필 병합 [{aiProfile.ProfileName}]"
            : $"Merge AI Profile [{aiProfile.ProfileName}]");

        // 상태 이름 매핑 테이블 (구 이름 -> 신규 고유 이름)
        var stateMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int addedStatesCount = 0;
        foreach (var state in aiProfile.States)
        {
            if (MacroEventProfile.IsGlobalState(state))
            {
                stateMapping[state] = "전역";
                continue;
            }

            // 기존 상태와 충돌하면 새 고유 이름 부여
            string uniqueState = _profile.States.Contains(state)
                ? GenerateUniqueStateName(state)
                : state;

            stateMapping[state] = uniqueState;
            _profile.States.Add(uniqueState);
            addedStatesCount++;

            // 노드 위치: 기존 노드와 겹치지 않는 안전한 위치에 스폰
            aiProfile.NodePositions.TryGetValue(state, out var origPos);
            float prefX = origPos != null ? origPos.X : 300f;
            float prefY = origPos != null ? origPos.Y : 150f;
            var safePos = MacroLayoutEngine.FindSafeSpawnPosition(_profile, prefX, prefY);
            _profile.NodePositions[uniqueState] = safePos;

            // 노드 크기
            if (aiProfile.NodeSizes.TryGetValue(state, out var sz))
            {
                _profile.NodeSizes[uniqueState] = new NodeCanvasSize(sz.Width, sz.Height);
            }
        }

        // 이벤트 복제 및 매핑
        int addedEventsCount = 0;
        MacroEvent? firstAddedEvt = null;
        foreach (var origEvt in aiProfile.Events)
        {
            string mappedState = stateMapping.TryGetValue(origEvt.StateTag, out var mapped)
                ? mapped
                : _profile.InitialState;

            var cloneEvt = CloneEvent(origEvt, mappedState);

            // 이벤트 내부의 상태 전환(ChangeState) 타겟 상태도 매핑된 이름으로 변경
            foreach (var act in cloneEvt.Actions)
            {
                if (act.Type == ActionType.ChangeState && !string.IsNullOrEmpty(act.TargetState))
                {
                    if (stateMapping.TryGetValue(act.TargetState, out var targetMapped))
                    {
                        act.TargetState = targetMapped;
                        act.Description = ko ? $"상태 전환: [{targetMapped}]" : $"Change State: [{targetMapped}]";
                    }
                }
            }

            _profile.Events.Add(cloneEvt);
            firstAddedEvt ??= cloneEvt;
            addedEventsCount++;
        }

        _isDirty = true;
        SyncAndRefreshAllViews();
        if (firstAddedEvt != null)
        {
            OnCanvasSelectionChanged(firstAddedEvt.StateTag, firstAddedEvt);
        }

        Log(ko
            ? $"✨ [AI 어시스턴트] '{aiProfile.ProfileName}'의 상태 {addedStatesCount}개, 이벤트 {addedEventsCount}개가 기존 프로필에 성공적으로 병합되었습니다."
            : $"✨ [AI Assistant] Merged {addedStatesCount} states and {addedEventsCount} events from '{aiProfile.ProfileName}'.");
    }

    public void RecordUndo() => RecordUndo(Localization.IsKorean ? "이전 작업" : "Previous Action");

    public void RecordUndo(string actionName)
    {
        _undoRedo.RecordSnapshot(_profile, actionName);
        UpdateUndoRedoButtons();
    }

    private void PerformUndo()
    {
        var (prev, actionName) = _undoRedo.Undo(_profile);
        if (prev != null)
        {
            _profile = prev;
            _canvas.Profile = _profile;
            _classicControl.Profile = _profile;
            _isDirty = true;
            SyncAndRefreshAllViews();
            OnCanvasSelectionChanged(CurrentSelectedState, CurrentSelectedEvent);
            UpdateUndoRedoButtons();
            string detail = string.IsNullOrEmpty(actionName)
                ? (Localization.IsKorean ? "이전 작업" : "previous action")
                : actionName;
            Log(Localization.IsKorean
                ? $"↶ 실행 취소(Undo): [{detail}] 작업을 취소했습니다."
                : $"↶ Undo: Reverted [{detail}].");
        }
    }

    private void PerformRedo()
    {
        var (next, actionName) = _undoRedo.Redo(_profile);
        if (next != null)
        {
            _profile = next;
            _canvas.Profile = _profile;
            _classicControl.Profile = _profile;
            _isDirty = true;
            SyncAndRefreshAllViews();
            OnCanvasSelectionChanged(CurrentSelectedState, CurrentSelectedEvent);
            UpdateUndoRedoButtons();
            string detail = string.IsNullOrEmpty(actionName)
                ? (Localization.IsKorean ? "이전 작업" : "previous action")
                : actionName;
            Log(Localization.IsKorean
                ? $"↷ 다시 실행(Redo): [{detail}] 작업을 다시 적용했습니다."
                : $"↷ Redo: Reapplied [{detail}].");
        }
    }

    private void UpdateUndoRedoButtons()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(UpdateUndoRedoButtons));
            return;
        }

        bool canUndo = _undoRedo.CanUndo;
        bool canRedo = _undoRedo.CanRedo;
        _btnUndo.Enabled = canUndo;
        _btnRedo.Enabled = canRedo;

        AppThemeHelper.UpdateUndoRedoButtonState(_btnUndo, _currentIsDark);
        AppThemeHelper.UpdateUndoRedoButtonState(_btnRedo, _currentIsDark);
    }

    /// <summary>
    /// 현재 선택된 이벤트 또는 상태 노드를 JSON 클립보드 형식으로 복사합니다.
    /// 모든 뷰 모드(캔버스, 핀그래프, 클래식 트리/리스트)에서 동일하게 동작합니다.
    /// </summary>
    public bool CopyCurrentSelectionToClipboard()
    {
        try
        {
            // 1. 이벤트가 선택되어 있다면 단일 이벤트 복사 우선
            if (CurrentSelectedEvent != null)
            {
                var envelope = new NodeClipboardEnvelope
                {
                    Kind = "Event",
                    SingleEvent = CurrentSelectedEvent
                };
                string json = MacroEventProfileManager.SaveToString(envelope);
                Clipboard.SetText(json);
                Log(Localization.IsKorean
                    ? $"📋 이벤트 ['{CurrentSelectedEvent.Name}'] 클립보드 복사 완료"
                    : $"📋 Event ['{CurrentSelectedEvent.Name}'] copied to clipboard.");
                return true;
            }

            // 2. 상태 노드가 선택되어 있다면 상태 노드 및 해당 상태의 이벤트 전체 복사
            string? st = CurrentSelectedState;
            if (!string.IsNullOrEmpty(st) && _profile.States.Contains(st))
            {
                var eventsInState = _profile.Events
                    .Where(e => (MacroEventProfile.IsGlobalState(st) && MacroEventProfile.IsGlobalState(e.StateTag)) ||
                                (!MacroEventProfile.IsGlobalState(st) && e.StateTag == st))
                    .ToList();

                _profile.NodePositions.TryGetValue(st, out var pos);
                _profile.NodeSizes.TryGetValue(st, out var size);

                var envelope = new NodeClipboardEnvelope
                {
                    Kind = "State",
                    StateName = st,
                    Position = pos != null ? new NodeCanvasPosition(pos.X, pos.Y) : null,
                    Size = size != null ? new NodeCanvasSize(size.Width, size.Height) : null,
                    Events = eventsInState
                };
                string json = MacroEventProfileManager.SaveToString(envelope);
                Clipboard.SetText(json);
                Log(Localization.IsKorean
                    ? $"📋 상태 노드 ['{st}'] (이벤트 {eventsInState.Count}개) 클립보드 복사 완료"
                    : $"📋 State Node ['{st}'] ({eventsInState.Count} events) copied to clipboard.");
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 클립보드에서 노드/이벤트를 붙여넣고 Undo/Redo 스냅샷을 생성합니다.
    /// 유효한 노드 데이터가 아닌 일반 텍스트나 다른 정보일 경우 에러 없이 안전하게 무시합니다.
    /// 모든 뷰 모드(캔버스, 핀그래프, 클래식 트리/리스트)에서 완벽히 지원됩니다.
    /// </summary>
    public bool PasteFromClipboard()
    {
        try
        {
            if (!Clipboard.ContainsText()) return false;
            string text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text) || !text.Contains(NodeClipboardEnvelope.MagicHeader))
            {
                return false; // 다른 일반 텍스트나 알 수 없는 정보는 조용히 무시
            }

            var envelope = System.Text.Json.JsonSerializer.Deserialize<NodeClipboardEnvelope>(text, MacroEventProfileManager.JsonOptions);
            if (envelope == null || envelope.Header != NodeClipboardEnvelope.MagicHeader)
            {
                return false;
            }

            bool ko = Localization.IsKorean;

            // 1. 단일 이벤트 붙여넣기
            if (envelope.Kind == "Event" && envelope.SingleEvent != null)
            {
                string targetState = CurrentSelectedState ?? _profile.InitialState ?? "기본 상태";
                var cloneEvt = CloneEvent(envelope.SingleEvent, targetState);

                RecordUndo(ko ? $"이벤트 붙여넣기 [{cloneEvt.Name}]" : $"Paste Event [{cloneEvt.Name}]");
                _profile.Events.Add(cloneEvt);
                _isDirty = true;
                SyncAndRefreshAllViews();
                OnCanvasSelectionChanged(targetState, cloneEvt);

                Log(ko
                    ? $"📥 클립보드에서 이벤트 ['{cloneEvt.Name}']을(를) '{targetState}'에 붙여넣었습니다."
                    : $"📥 Pasted event ['{cloneEvt.Name}'] into '{targetState}'.");
                return true;
            }

            // 2. 상태 노드 (및 소속 이벤트 전체) 붙여넣기
            if (envelope.Kind == "State" && !string.IsNullOrWhiteSpace(envelope.StateName))
            {
                string baseName = envelope.StateName;
                if (MacroEventProfile.IsGlobalState(baseName))
                {
                    baseName = ko ? "복사된 전역 상태" : "Copied Global State";
                }

                // 중복되지 않는 고유한 상태 이름 자동 생성
                string newStateName = GenerateUniqueStateName(baseName);

                // 안전한 노드 스폰 좌표 자동 탐색 (기존 노드와의 겹침 0% 보장)
                float prefX = envelope.Position != null ? envelope.Position.X + 40f : 300f;
                float prefY = envelope.Position != null ? envelope.Position.Y + 40f : 150f;
                var safePos = MacroLayoutEngine.FindSafeSpawnPosition(_profile, prefX, prefY);

                RecordUndo(ko ? $"상태 노드 붙여넣기 [{newStateName}]" : $"Paste State Node [{newStateName}]");

                _profile.States.Add(newStateName);
                _profile.NodePositions[newStateName] = safePos;
                if (envelope.Size != null)
                {
                    _profile.NodeSizes[newStateName] = new NodeCanvasSize(envelope.Size.Width, envelope.Size.Height);
                }

                int eventCount = 0;
                MacroEvent? firstClonedEvt = null;
                foreach (var origEvt in envelope.Events)
                {
                    var cloneEvt = CloneEvent(origEvt, newStateName);
                    _profile.Events.Add(cloneEvt);
                    firstClonedEvt ??= cloneEvt;
                    eventCount++;
                }

                _isDirty = true;
                SyncAndRefreshAllViews();
                OnCanvasSelectionChanged(newStateName, firstClonedEvt);

                Log(ko
                    ? $"📥 클립보드에서 상태 노드 ['{newStateName}'] (이벤트 {eventCount}개)을(를) 붙여넣었습니다."
                    : $"📥 Pasted state node ['{newStateName}'] with {eventCount} events.");
                return true;
            }

            return false;
        }
        catch
        {
            // 그 외의 형식 파싱 실패 시 예외 없이 안전하게 무시
            return false;
        }
    }

    private static MacroEvent CloneEvent(MacroEvent source, string targetStateTag)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(source, MacroEventProfileManager.JsonOptions);
        var clone = System.Text.Json.JsonSerializer.Deserialize<MacroEvent>(json, MacroEventProfileManager.JsonOptions) ?? new MacroEvent();
        clone.Id = Guid.NewGuid().ToString();
        clone.StateTag = targetStateTag;
        return clone;
    }

    private string GenerateUniqueStateName(string baseName)
    {
        string candidate = $"{baseName} (복사)";
        if (!_profile.States.Contains(candidate)) return candidate;

        int num = 2;
        while (_profile.States.Contains($"{baseName} (복사 {num})"))
        {
            num++;
        }
        return $"{baseName} (복사 {num})";
    }
}

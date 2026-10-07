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
                _canvas.Invalidate();
                _classicControl.RefreshSelectedEventItem();

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

            _macroRecorder.StartRecording();
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
                    BeginInvoke(new Action(() => _canvas.SetActiveAction(evId, actIdx)));
                }
            };
            _harness.OnTriggerFired += (evId, trgIdx) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke(new Action(() => _canvas.NotifyTriggerFired(evId, trgIdx)));
                }
            };

            _isMacroRunning = true;
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
            if (_isDirty)
            {
                var res = MessageBox.Show(Localization.Get("PromptUnsavedChanges"), Localization.Get("TitleUnsavedChanges"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (res == DialogResult.Cancel) return;
                if (res == DialogResult.Yes) SaveProfile();
            }

            RecordUndo(Localization.IsKorean
                ? $"AI 프로필 적용 [{dlg.GeneratedProfile.ProfileName}]"
                : $"Apply AI Profile [{dlg.GeneratedProfile.ProfileName}]");

            _profile = dlg.GeneratedProfile;
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
            _canvas.Invalidate();
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
            _canvas.Invalidate();
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


}

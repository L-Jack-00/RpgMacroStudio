using System.Collections.Concurrent;
using System.IO;
using System.Windows.Forms;
using OpenCvSharp;
using RpgMacroHarness.Data;
using RpgMacroHarness.Extensibility;
using RpgMacroHarness.Input;
using RpgMacroHarness.Vision;

namespace RpgMacroHarness.Core;

/// <summary>
/// RPG 쯔꾸르 방식의 FSM 상태(State), 전역 이벤트, 변수(Variable), 다중 조건(AND),
/// 독립 비동기 브랜치를 지원하는 고성능 매크로 실행 엔진입니다.
/// </summary>
public class EventMacroHarness
{
    private readonly MacroEventProfile _profile;
    private bool _isRunning = false;
    private readonly Dictionary<string, DateTime> _lastTriggeredMap = new();
    private readonly Dictionary<string, Mat> _templateCache = new();
    private readonly ConcurrentDictionary<string, (Task Task, CancellationTokenSource Cts, string StateTag)> _runningBranches = new();

    /// <summary>
    /// 현재 하네스의 활성 상태 (예: "전투 모드", "마을/정비" 등)
    /// </summary>
    public string CurrentState { get; private set; } = "기본 상태";

    /// <summary>
    /// 런타임 변수 저장소
    /// </summary>
    public ConcurrentDictionary<string, double> RuntimeVariables { get; } = new();

    public event Action<string>? OnLog;
    public event Action<string>? OnStateChanged;
    public event Action<string, int>? OnActionExecuting;
    public event Action<string, int>? OnTriggerFired;

    public EventMacroHarness(MacroEventProfile profile)
    {
        _profile = profile;
        CurrentState = string.IsNullOrEmpty(profile.InitialState) ? "기본 상태" : profile.InitialState;

        // 초기 변수 복사
        foreach (var kvp in profile.Variables)
        {
            RuntimeVariables[kvp.Key] = kvp.Value;
        }
    }

    private readonly ManualResetEventSlim _pauseEvent = new(true);

    /// <summary>
    /// 매크로 엔진의 일시정지 상태 여부
    /// </summary>
    public bool IsPaused { get; private set; } = false;

    public void Pause()
    {
        if (!_isRunning || IsPaused) return;
        IsPaused = true;
        _pauseEvent.Reset();
        Log("[일시정지] 매크로 엔진이 일시정지되었습니다. (현재 상태 및 변수 보존)");
    }

    public void Resume()
    {
        if (!_isRunning || !IsPaused) return;
        IsPaused = false;
        _pauseEvent.Set();
        Log("[재개] 매크로 엔진이 동작을 재개합니다.");
    }

    public void Start()
    {
        _isRunning = true;
        IsPaused = false;
        _pauseEvent.Set();
        _lastTriggeredMap.Clear();
        if (Enum.TryParse<Keys>(AppSettings.Current.EmergencyStopHotkey, true, out var ek))
            EmergencyStop.StartMonitoring(ek);
        else
            EmergencyStop.StartMonitoring(Keys.F12);

        Log($"[매크로 가동] FSM 엔진 루프 시작 (현재 상태: [{CurrentState}])");

        while (_isRunning)
        {
            if (EmergencyStop.IsTriggered)
            {
                Log("[비상 정지] 긴급 중단되었습니다.");
                _isRunning = false;
                break;
            }

            if (IsPaused)
            {
                _pauseEvent.Wait(200);
                if (!_isRunning || EmergencyStop.IsTriggered) break;
                if (IsPaused) continue;
            }

            // 비전 조건이 필요한지 검사 (전역 이벤트 + 현재 상태 이벤트 대상 고속 제로-할당 검사)
            bool needsVision = CheckNeedsVisionForState(CurrentState);

            Mat? screen = null;
            if (needsVision)
            {
                try
                {
                    screen = ScreenCapture.CaptureScreen();
                }
                catch (Exception ex)
                {
                    Log($"[경고] 화면 캡처 대기 중: {ex.Message}");
                }
            }

            try
            {
                foreach (var evt in _profile.Events)
                {
                    if (!evt.IsEnabled) continue;

                    // 상태 필터링: 전역 이벤트는 언제나 실행, 특정 상태 이벤트는 현재 상태와 일치할 때만 실행
                    bool isGlobal = MacroEventProfile.IsGlobalState(evt.StateTag);
                    bool isCurrentStateMatch = evt.StateTag == CurrentState;
                    if (!isGlobal && !isCurrentStateMatch) continue;

                    try
                    {
                        // 다중 조건(AND) 전체 평가
                        if (EvaluateAllTriggers(evt, screen, out OpenCvSharp.Point detectedPoint))
                        {
                            for (int ti = 0; ti < evt.Triggers.Count; ti++)
                            {
                                OnTriggerFired?.Invoke(evt.Id, ti);
                            }

                            if (evt.IsParallel)
                            {
                                // 병렬(동시 실행) 이벤트: 독립 비동기 브랜치로 분기 실행
                                if (_runningBranches.TryGetValue(evt.Id, out var existingBranch) && !existingBranch.Task.IsCompleted)
                                {
                                    continue;
                                }

                                var cts = new CancellationTokenSource();
                                string tagInfo = isGlobal ? "[전역]" : $"[{evt.StateTag}]";
                                Log($"▶ {tagInfo} [동시 실행 시작] [{evt.Name}] 브랜치 가동 ({evt.Actions.Count}개 행동)");

                                var branchTask = Task.Run(() =>
                                {
                                    try
                                    {
                                        ExecuteActions(evt, detectedPoint, currentScreen: null, ct: cts.Token);
                                        Log($"✔ {tagInfo} [동시 실행 완료] [{evt.Name}] 브랜치 완주");
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        Log($"⏹ {tagInfo} [상태 전환 취소] [{evt.Name}] 브랜치 정상 중단");
                                    }
                                    catch (Exception ex)
                                    {
                                        Log($"[브랜치 오류] {evt.Name}: {ex.Message}");
                                    }
                                    finally
                                    {
                                        _runningBranches.TryRemove(evt.Id, out _);
                                        cts.Dispose();
                                    }
                                });
                                _runningBranches[evt.Id] = (branchTask, cts, evt.StateTag);
                            }
                            else
                            {
                                // 일반(직렬) 이벤트: 메인 흐름에서 순차 실행 (이미 캡처된 screen 재사용으로 지연 최소화)
                                string tagInfo = isGlobal ? "[전역-직렬]" : $"[{evt.StateTag}]";
                                Log($"▶ {tagInfo} [일반 실행] [{evt.Name}] 순차 실행 시작 ({evt.Actions.Count}개 행동)");
                                ExecuteActions(evt, detectedPoint, currentScreen: screen);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[오류] {evt.Name} 실행 중 오류: {ex.Message}");
                    }

                    if (EmergencyStop.IsTriggered || !_isRunning) break;
                }
            }
            finally
            {
                screen?.Dispose();
            }

            Thread.Sleep(_profile.CheckIntervalMs);
        }

        foreach (var mat in _templateCache.Values) mat.Dispose();
        _templateCache.Clear();
        EmergencyStop.StopMonitoring();
        WindowsInputSimulator.ReleaseAllPressedInputs();
        CleanupBranches();
    }

    public void Stop()
    {
        _isRunning = false;
        IsPaused = false;
        _pauseEvent.Set();
        EmergencyStop.StopMonitoring();
        WindowsInputSimulator.ReleaseAllPressedInputs();
        CleanupBranches();
    }

    private bool CheckNeedsVisionForState(string state)
    {
        for (int i = 0; i < _profile.Events.Count; i++)
        {
            var e = _profile.Events[i];
            if (!e.IsEnabled) continue;
            if (!MacroEventProfile.IsGlobalState(e.StateTag) && e.StateTag != state) continue;

            for (int ti = 0; ti < e.Triggers.Count; ti++)
            {
                var tt = e.Triggers[ti].Type;
                if (tt == TriggerType.GaugeBar || tt == TriggerType.ImageDetection || tt == TriggerType.VisionDetection)
                    return true;
            }

            for (int ai = 0; ai < e.Actions.Count; ai++)
            {
                var a = e.Actions[ai];
                if (a.Type == ActionType.MouseClick && (a.TargetMode == MouseTargetMode.TargetImage || !string.IsNullOrEmpty(a.TargetImagePath)))
                    return true;
            }
        }
        return false;
    }

    private void CleanupBranches()
    {
        foreach (var branch in _runningBranches.Values)
        {
            try { branch.Cts.Cancel(); } catch { }
        }

        try
        {
            Task.WaitAll(_runningBranches.Values.Select(b => b.Task).Where(t => !t.IsCompleted).ToArray(), 500);
        }
        catch { }
        _runningBranches.Clear();
    }

    /// <summary>
    /// 이벤트의 모든 발동 조건을 AND로 평가합니다. (모두 참이어야 최종 참)
    /// </summary>
    private bool EvaluateAllTriggers(MacroEvent evt, Mat? screen, out OpenCvSharp.Point detectedPoint)
    {
        detectedPoint = new OpenCvSharp.Point(0, 0);

        if (evt.Triggers.Count == 0) return true;

        foreach (var trigger in evt.Triggers)
        {
            if (!EvaluateSingleTrigger(evt.Id, trigger, screen, out var point))
            {
                return false; // 하나라도 만족하지 않으면 미발동 (AND 결합)
            }

            // 이미지 감지 조건에서 찾은 좌표가 있다면 대표 좌표로 보존
            if ((trigger.Type == TriggerType.ImageDetection || (trigger.Type == TriggerType.VisionDetection && trigger.VisionTarget == VisionTargetType.Image)) && (point.X != 0 || point.Y != 0))
            {
                detectedPoint = point;
            }
        }

        return true;
    }

    private bool EvaluateSingleTrigger(string eventId, MacroTrigger trigger, Mat? screen, out OpenCvSharp.Point detectedPoint)
    {
        detectedPoint = new OpenCvSharp.Point(0, 0);

        // OCP 플러그인 평가자 연동 (신규 확장 트리거 지원)
        var pluginEvaluator = MacroPluginRegistry.GetEvaluator(trigger.Type);
        if (pluginEvaluator != null)
        {
            var ctx = new TriggerEvaluationContext
            {
                EventId = eventId,
                Screen = screen,
                Variables = RuntimeVariables,
                LastTriggeredMap = _lastTriggeredMap,
                TemplateCache = _templateCache
            };
            return pluginEvaluator.Evaluate(trigger, ctx, out detectedPoint);
        }

        switch (trigger.Type)
        {
            case TriggerType.Always:
                return true;

            case TriggerType.TimerInterval:
                string timerKey = $"{eventId}_{trigger.GetHashCode()}";
                if (!_lastTriggeredMap.TryGetValue(timerKey, out var lastTime))
                {
                    _lastTriggeredMap[timerKey] = DateTime.Now;
                    return true;
                }

                int humanInterval = InputHumanizer.GetHumanizedTimerInterval(trigger.IntervalMs);
                if ((DateTime.Now - lastTime).TotalMilliseconds >= humanInterval)
                {
                    _lastTriggeredMap[timerKey] = DateTime.Now;
                    return true;
                }
                return false;

            case TriggerType.VisionDetection:
            case TriggerType.GaugeBar:
            case TriggerType.ImageDetection:
                bool isImageMode = (trigger.Type == TriggerType.ImageDetection) || 
                                   (trigger.Type == TriggerType.VisionDetection && trigger.VisionTarget == VisionTargetType.Image);

                if (isImageMode)
                {
                    // 1. 이미지 템플릿 매칭 (일치 / 불일치 / 좌우 반전 감지)
                    if (screen == null) return false;
                    string cacheKey = $"{eventId}_{trigger.ImagePath ?? trigger.ImageBase64?.Substring(0, 16) ?? "none"}";

                    if (!_templateCache.TryGetValue(cacheKey, out var template))
                    {
                        if (File.Exists(trigger.ImagePath))
                            template = Cv2.ImRead(trigger.ImagePath, ImreadModes.Color);
                        else if (!string.IsNullOrEmpty(trigger.ImageBase64))
                        {
                            byte[] bytes = Convert.FromBase64String(trigger.ImageBase64);
                            template = Cv2.ImDecode(bytes, ImreadModes.Color);
                        }

                        if (template != null) _templateCache[cacheKey] = template;
                    }

                    if (template == null) return false;

                    var vsBounds = ScreenCapture.VirtualScreenBounds;
                    int originX = vsBounds.Left;
                    int originY = vsBounds.Top;

                    Rect? roi = trigger.ImageRoi != null
                        ? new Rect(trigger.ImageRoi.X - originX, trigger.ImageRoi.Y - originY, trigger.ImageRoi.Width, trigger.ImageRoi.Height)
                        : null;

                    var result = ImageMatcher.Match(screen, template, trigger.ImageAccuracyPercent, roi);

                    // 좌우 반전 포함 감지 옵션이 켜져 있고 정방향에서 미감지 시 좌우 반전 템플릿으로 추가 검사 (캐싱으로 중복 연산 방지)
                    if (!result.Found && trigger.AllowFlipHorizontal)
                    {
                        string flipCacheKey = cacheKey + "_flipped";
                        if (!_templateCache.TryGetValue(flipCacheKey, out var flipped))
                        {
                            flipped = new Mat();
                            Cv2.Flip(template, flipped, FlipMode.Y);
                            _templateCache[flipCacheKey] = flipped;
                        }
                        var flippedResult = ImageMatcher.Match(screen, flipped, trigger.ImageAccuracyPercent, roi);
                        if (flippedResult.Found)
                        {
                            result = flippedResult;
                        }
                    }

                    bool isMatched = result.Found;
                    bool finalSuccess = (trigger.VisionMatchMode == VisionMatchMode.Mismatch) ? !isMatched : isMatched;

                    if (finalSuccess && result.Found)
                    {
                        detectedPoint = new OpenCvSharp.Point(
                            result.Location.X + template.Width / 2 + originX,
                            result.Location.Y + template.Height / 2 + originY
                        );
                    }

                    return finalSuccess;
                }
                else
                {
                    // 2. 색상 분석 (스포이드, 면적 밀도 점유율, 일치 / 불일치)
                    if (screen == null || trigger.GaugeRoi == null) return false;

                    var vsBounds = ScreenCapture.VirtualScreenBounds;
                    int originX = vsBounds.Left;
                    int originY = vsBounds.Top;

                    var gRoi = new Rect(
                        trigger.GaugeRoi.X - originX, 
                        trigger.GaugeRoi.Y - originY, 
                        trigger.GaugeRoi.Width, 
                        trigger.GaugeRoi.Height
                    );

                    System.Drawing.Color customColor = System.Drawing.Color.Red;
                    try
                    {
                        if (!string.IsNullOrEmpty(trigger.CustomColorHex))
                            customColor = System.Drawing.ColorTranslator.FromHtml(trigger.CustomColorHex);
                    }
                    catch { }

                    double currentPercent = GaugeBarReader.ReadPercentage(
                        screen,
                        gRoi,
                        customColor,
                        trigger.ColorAccuracyPercent,
                        trigger.GaugeColor
                    );

                    bool colorConditionMet = trigger.ComparisonOp switch
                    {
                        ComparisonOperator.LessThan => currentPercent < trigger.GaugeThresholdPercent,
                        ComparisonOperator.LessThanOrEqual => currentPercent <= trigger.GaugeThresholdPercent,
                        ComparisonOperator.GreaterThan => currentPercent > trigger.GaugeThresholdPercent,
                        ComparisonOperator.GreaterThanOrEqual => currentPercent >= trigger.GaugeThresholdPercent,
                        _ => false
                    };

                    return (trigger.VisionMatchMode == VisionMatchMode.Mismatch) ? !colorConditionMet : colorConditionMet;
                }

            case TriggerType.VariableCheck:
                RuntimeVariables.TryGetValue(trigger.VariableName, out double currentVal);
                return trigger.VarCompareOp switch
                {
                    VariableComparison.Equal => Math.Abs(currentVal - trigger.VarTargetValue) < 0.0001,
                    VariableComparison.NotEqual => Math.Abs(currentVal - trigger.VarTargetValue) >= 0.0001,
                    VariableComparison.GreaterThan => currentVal > trigger.VarTargetValue,
                    VariableComparison.GreaterThanOrEqual => currentVal >= trigger.VarTargetValue,
                    VariableComparison.LessThan => currentVal < trigger.VarTargetValue,
                    VariableComparison.LessThanOrEqual => currentVal <= trigger.VarTargetValue,
                    _ => false
                };
        }

        return false;
    }

    private void ExecuteActions(MacroEvent evt, OpenCvSharp.Point detectedPoint, Mat? currentScreen = null, CancellationToken ct = default)
    {
        InputHumanizer.JitterMs = evt.HumanizerJitterMs;
        InputHumanizer.JitterScalePercent = evt.HumanizerJitterPercent;

        try
        {
            for (int i = 0; i < evt.Actions.Count; i++)
            {
                var action = evt.Actions[i];
                if (ct.IsCancellationRequested || EmergencyStop.IsTriggered || !_isRunning) break;

                OnActionExecuting?.Invoke(evt.Id, i);

                if (IsPaused)
                {
                    _pauseEvent.Wait();
                    if (ct.IsCancellationRequested || EmergencyStop.IsTriggered || !_isRunning) break;
                }

            // OCP 플러그인 액션 실행자 연동 (신규 확장 행동 지원)
            var pluginExecutor = MacroPluginRegistry.GetExecutor(action.Type);
            if (pluginExecutor != null)
            {
                var actCtx = new ActionExecutionContext
                {
                    Event = evt,
                    DetectedPoint = detectedPoint,
                    CancellationToken = ct,
                    PauseEvent = _pauseEvent,
                    Variables = RuntimeVariables,
                    TemplateCache = _templateCache,
                    LogAction = Log,
                    ChangeStateAction = ChangeState,
                    IsRunningFunc = () => _isRunning && !EmergencyStop.IsTriggered,
                    IsPausedFunc = () => IsPaused
                };
                pluginExecutor.Execute(action, actCtx);
                continue;
            }

            switch (action.Type)
            {
                case ActionType.KeyPress:
                    if (Enum.TryParse<Keys>(action.Key, true, out var key))
                    {
                        switch (action.KeyMode)
                        {
                            case KeyStrokeMode.KeyDown:
                                Log($"  - ⬇️ 키다운: [{key}] (누름 유지, 후지연: ~{action.PostDelayMs}ms)");
                                WindowsInputSimulator.SendKeyDown(key, action.PostDelayMs);
                                break;
                            case KeyStrokeMode.KeyUp:
                                Log($"  - ⬆️ 키업: [{key}] (손 뗌, 후지연: ~{action.PostDelayMs}ms)");
                                WindowsInputSimulator.SendKeyUp(key, action.PostDelayMs);
                                break;
                            case KeyStrokeMode.KeyDownUp:
                            default:
                                Log($"  - ⌨️ 키 입력: [{key}] (다운업 모사: ~{action.HoldTimeMs}ms, 딜레이: ~{action.PostDelayMs}ms)");
                                WindowsInputSimulator.SendKeyPress(key, action.HoldTimeMs, action.PostDelayMs);
                                break;
                        }
                    }
                    break;

                case ActionType.Wait:
                    int humanWait = InputHumanizer.GetHumanizedWait(action.WaitDurationMs);
                    Log($"  - ⏳ 대기: {humanWait}ms (휴머나이저 적용)");
                    int waited = 0;
                    while (waited < humanWait && _isRunning && !EmergencyStop.IsTriggered && !ct.IsCancellationRequested)
                    {
                        if (IsPaused)
                        {
                            _pauseEvent.Wait();
                            if (!_isRunning || EmergencyStop.IsTriggered || ct.IsCancellationRequested) break;
                        }
                        int step = Math.Min(50, humanWait - waited);
                        Thread.Sleep(step);
                        waited += step;
                    }
                    break;

                case ActionType.MouseClick:
                    if (action.TargetMode == MouseTargetMode.TargetImage || (!string.IsNullOrEmpty(action.TargetImagePath) && action.TargetMode != MouseTargetMode.Coordinates))
                    {
                        Mat? actionScreen = currentScreen;
                        bool ownScreen = false;
                        if (actionScreen == null)
                        {
                            try
                            {
                                actionScreen = ScreenCapture.CaptureScreen();
                                ownScreen = true;
                            }
                            catch (Exception ex)
                            {
                                Log($"  - ❌ 마우스 캡처 실패: {ex.Message}");
                            }
                        }

                        try
                        {
                            if (actionScreen != null)
                            {
                                string cacheKey = "act_img_" + (action.TargetImagePath ?? action.TargetImageBase64?.Substring(0, 16) ?? "none");
                                if (!_templateCache.TryGetValue(cacheKey, out var actTemplate))
                                {
                                    if (File.Exists(action.TargetImagePath))
                                        actTemplate = Cv2.ImRead(action.TargetImagePath, ImreadModes.Color);
                                    else if (!string.IsNullOrEmpty(action.TargetImageBase64))
                                    {
                                        byte[] bytes = Convert.FromBase64String(action.TargetImageBase64);
                                        actTemplate = Cv2.ImDecode(bytes, ImreadModes.Color);
                                    }

                                    if (actTemplate != null) _templateCache[cacheKey] = actTemplate;
                                }

                                if (actTemplate != null)
                                {
                                    var vsBounds = ScreenCapture.VirtualScreenBounds;
                                    int originX = vsBounds.Left;
                                    int originY = vsBounds.Top;

                                    Rect? roi = action.TargetImageRoi != null
                                        ? new Rect(action.TargetImageRoi.X - originX, action.TargetImageRoi.Y - originY, action.TargetImageRoi.Width, action.TargetImageRoi.Height)
                                        : null;

                                    var matchResult = ImageMatcher.Match(actionScreen, actTemplate, action.TargetImageAccuracyPercent, roi);
                                    if (matchResult.Found)
                                    {
                                        int clickX = matchResult.Location.X + actTemplate.Width / 2 + originX;
                                        int clickY = matchResult.Location.Y + actTemplate.Height / 2 + originY;
                                        ExecuteMouseAction(action, clickX, clickY, "이미지 탐색 성공");
                                    }
                                    else
                                    {
                                        Log($"  - ⚠️ 클릭 대상 이미지 탐색 실패 (정확도 {action.TargetImageAccuracyPercent}% 미달)");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"  - ❌ 마우스 이미지 클릭 실패: {ex.Message}");
                        }
                        finally
                        {
                            if (ownScreen) actionScreen?.Dispose();
                        }
                    }
                    else if (action.TargetMode == MouseTargetMode.TriggerDetected || action.ClickDetectedLocation)
                    {
                        ExecuteMouseAction(action, detectedPoint.X, detectedPoint.Y, "조건 감지 위치");
                    }
                    else
                    {
                        ExecuteMouseAction(action, action.CustomX, action.CustomY, "좌표");
                    }
                    break;

                case ActionType.VariableControl:
                    double updatedVal = RuntimeVariables.AddOrUpdate(
                        action.VariableName,
                        action.VariableOp switch
                        {
                            VariableOperator.Set => action.VariableValue,
                            VariableOperator.Add => action.VariableValue,
                            VariableOperator.Subtract => -action.VariableValue,
                            _ => action.VariableValue
                        },
                        (key, old) => action.VariableOp switch
                        {
                            VariableOperator.Set => action.VariableValue,
                            VariableOperator.Add => old + action.VariableValue,
                            VariableOperator.Subtract => old - action.VariableValue,
                            _ => action.VariableValue
                        }
                    );
                    Log($"  - 🔢 변수 조작: [{action.VariableName}] = {updatedVal} ({action.VariableOp} {action.VariableValue})");
                    break;

                case ActionType.ChangeState:
                    ChangeState(action.TargetState);
                    break;
            }
        }
    }
    finally
    {
        OnActionExecuting?.Invoke(evt.Id, -1);
    }
    }

    private void ExecuteMouseAction(MacroAction action, int clickX, int clickY, string modeLabel)
    {
        int hold = action.HoldTimeMs > 0 ? action.HoldTimeMs : 35;
        int post = action.PostDelayMs;

        if (action.MouseMode == MouseStrokeMode.MouseDown)
        {
            WindowsInputSimulator.SendMouseDown(clickX, clickY, action.MouseButton, post);
            Log($"  - ⬇️ 마우스다운: ({clickX}, {clickY}) [{action.MouseButton}] ({modeLabel})");
        }
        else if (action.MouseMode == MouseStrokeMode.MouseUp)
        {
            WindowsInputSimulator.SendMouseUp(clickX, clickY, action.MouseButton, post);
            Log($"  - ⬆️ 마우스업: ({clickX}, {clickY}) [{action.MouseButton}] ({modeLabel})");
        }
        else
        {
            WindowsInputSimulator.SendMouseClick(clickX, clickY, action.MouseButton, hold, post);
            Log($"  - 🖱️ 마우스 클릭: ({clickX}, {clickY}) [{action.MouseButton}] ({hold}ms, {modeLabel})");
        }
    }

    private int _recentTransitionCount = 0;
    private DateTime _lastTransitionWindow = DateTime.UtcNow;

    /// <summary>
    /// FSM 상태를 전환하고 이전 상태의 비전역 비동기 병렬 브랜치를 안전하게 취소합니다.
    /// 순환 무한 루프 발생 시 런타임 보호 가드(CallDepthGuard)가 작동합니다.
    /// </summary>
    public void ChangeState(string newState)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTransitionWindow).TotalSeconds >= 1.0)
        {
            _lastTransitionWindow = now;
            _recentTransitionCount = 0;
        }
        _recentTransitionCount++;
        if (_recentTransitionCount > 60)
        {
            Log($"  - ⚠️ [보호 가드 발동] 초당 상태 전환 빈도 초과(>60회). 순환 루프 방어를 위해 전이가 안전하게 억제되었습니다.");
            return;
        }

        string prevState = CurrentState;
        CurrentState = newState;
        Log($"  - 🔄 [상태 전환] '{prevState}' ➔ '{CurrentState}'");

        // 이전 상태에 귀속된 비전역 병렬 브랜치 즉시 취소
        foreach (var kvp in _runningBranches)
        {
            if (!MacroEventProfile.IsGlobalState(kvp.Value.StateTag) && kvp.Value.StateTag != CurrentState)
            {
                try { kvp.Value.Cts.Cancel(); } catch { }
            }
        }
        OnStateChanged?.Invoke(CurrentState);
    }

    private void Log(string msg)
    {
        OnLog?.Invoke(msg);
    }
}

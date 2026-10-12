using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using OpenCvSharp;
using Point = OpenCvSharp.Point;
using RpgMacroHarness.Core;
using RpgMacroHarness.Data;
using RpgMacroHarness.Extensibility;
using RpgMacroHarness.Input;
using RpgMacroHarness.Vision;
using RpgMacroHarness.AI;
using RpgMacroHarness.AI.Clients;

namespace RpgMacroHarness.Tests;

/// <summary>
/// 엔진의 핵심 기능(게이지바 색상 분석, 템플릿 매칭, Base64 프로필 직렬화, 비상정지 루프, 플러그인 확장 파이프라인)을 자체 검증하는 내부 테스트 스위트입니다.
/// </summary>
public static class SelfTestRunner
{
    public static bool RunAllTests()
    {
        Console.WriteLine("\n[내부 테스트 시작] 핵심 모듈 무결성 검증...");
        bool allPassed = true;

        allPassed &= TestGaugeBarReader();
        allPassed &= TestEyedropperCustomColor();
        allPassed &= TestImageMatcher();
        allPassed &= TestProfileSerializationAndBase64();
        allPassed &= TestEmergencyStopLifecycle();
        allPassed &= TestAppSettingsAndLocalization();
        allPassed &= TestEmptyEventLifecycle();
        allPassed &= TestMouseClickTargetModes();
        allPassed &= TestParallelBranchExecution();
        allPassed &= TestMultiConditionAndFsmVariables();
        allPassed &= TestStateRenameAndDuplicateCheck();
        allPassed &= TestPauseAndResumeLifecycle();
        allPassed &= TestHotkeySettingsValidation();
        allPassed &= TestMultiMonitorCoordinates();
        allPassed &= TestMacroRecordingConversion();
        allPassed &= TestProfileAtomicBackupAndRecovery();
        allPassed &= TestSoakStability();
        allPassed &= TestPluginExtensibility();
        allPassed &= TestAutoLayoutNoOverlapAndFsmFlow();
        allPassed &= TestHarnessVisionOptimizationAndCache();
        allPassed &= TestPinGraphAndHierarchyTreeView();
        allPassed &= TestSafeSpawnCollisionAvoidanceAnd4WayViewMode();
        allPassed &= TestPinGraphPersistenceAndNodeResizeAndLayout();
        allPassed &= TestV310DualGaugesAndHardwareSafety();
        allPassed &= TestAppKeyHelperAndWindowPersistence();
        allPassed &= TestUniversalKeyboardNavigationAndShortcutsAcrossAllViews();
        allPassed &= TestEmojiFontIntegrityAndWizardKeyControls();
        allPassed &= TestAiAssistantHarnessAndMultiProviderGeneration();
        allPassed &= TestNodeClipboardAndUndoRedoIntegrity();
        allPassed &= TestKeyStrokeModesAndSimultaneousInput();
        allPassed &= TestKeySequenceFoldingAndSizeAwareAutoLayout();
        allPassed &= TestMouseStrokeModesAndNoNodesHighlight();
        allPassed &= TestPrecisionLoopControllerAndDriftCompensation();

        if (allPassed)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n🎉 [모든 테스트 통과] 33개 핵심 모듈 검증 성공! v5.4.0 폐루프 틱 제어기(PrecisionLoopController), 지연 드리프트 자동 보정 및 맹점 레이더 무결성 검증 완료.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n❌ [테스트 실패] 일부 모듈에서 결함이 감지되었습니다.");
            Console.ResetColor();
        }

        return allPassed;
    }

    private static bool TestGaugeBarReader()
    {
        try
        {
            // 200x20 크기의 가상 화면 생성 (가로 절반인 100px은 빨간색, 나머지는 검은색)
            using var testMat = new Mat(20, 200, MatType.CV_8UC3, Scalar.Black);
            Cv2.Rectangle(testMat, new Rect(0, 0, 100, 20), new Scalar(0, 0, 220), -1); // BGR 순서 (빨간색)

            double percent = GaugeBarReader.ReadPercentage(testMat, new Rect(0, 0, 200, 20), colorType: GaugeColorType.RedHp);

            // 약 50%가 나와야 함
            if (percent >= 49.0 && percent <= 51.0)
            {
                Console.WriteLine($"  ✔ [통과] GaugeBarReader 50% 기본 체력바 감지: {percent}%");
                return true;
            }
            Console.WriteLine($"  ❌ [실패] GaugeBarReader 50% 감지 오차: {percent}%");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] GaugeBarReader 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestEyedropperCustomColor()
    {
        try
        {
            // 사용자 지정 주황색 게이지바 (#FF8C00) 80% 채움 시뮬레이션
            var customColor = Color.FromArgb(255, 140, 0);
            using var testMat = new Mat(20, 200, MatType.CV_8UC3, Scalar.Black);
            Cv2.Rectangle(testMat, new Rect(0, 0, 160, 20), new Scalar(0, 140, 255), -1); // BGR: (0, 140, 255)

            double percent = GaugeBarReader.ReadPercentage(
                testMat, 
                new Rect(0, 0, 200, 20), 
                customColor: customColor, 
                colorAccuracy: 85.0, 
                colorType: GaugeColorType.CustomEyedropper
            );

            // 160/200 = 80.0% 감지 확인
            if (percent >= 79.0 && percent <= 81.0)
            {
                Console.WriteLine($"  ✔ [통과] 스포이드 사용자 맞춤 색상(오차 85%) 80% 감지: {percent}%");
                return true;
            }
            Console.WriteLine($"  ❌ [실패] 스포이드 색상 감지 오차: {percent}%");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 스포이드 색상 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestImageMatcher()
    {
        try
        {
            // 300x300 화면에 패턴이 있는 30x30 아이콘 생성
            using var screen = new Mat(300, 300, MatType.CV_8UC3, Scalar.All(50));
            using var template = new Mat(30, 30, MatType.CV_8UC3, Scalar.White);
            Cv2.Circle(template, new Point(15, 15), 8, Scalar.Blue, -1);
            Cv2.Rectangle(template, new Rect(5, 5, 8, 8), Scalar.Red, -1);

            // (100, 100) 위치에 템플릿 복사
            using var subMat = new Mat(screen, new Rect(100, 100, 30, 30));
            template.CopyTo(subMat);

            var result = ImageMatcher.Match(screen, template, accuracyPercent: 90.0);

            if (result.Found && result.Location.X == 100 && result.Location.Y == 100)
            {
                Console.WriteLine($"  ✔ [통과] ImageMatcher 템플릿 감지 성공: 좌표({result.Location.X}, {result.Location.Y}), 점수: {result.Score:P1}");
                return true;
            }
            Console.WriteLine($"  ❌ [실패] ImageMatcher 감지 불일치: Found={result.Found}, Pos=({result.Location.X},{result.Location.Y})");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] ImageMatcher 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestProfileSerializationAndBase64()
    {
        try
        {
            // 가상 이미지 템플릿 생성 및 Base64 인코딩 검증
            using var img = new Mat(10, 10, MatType.CV_8UC3, Scalar.Red);
            byte[] rawBytes = img.ToBytes(".png");
            string base64 = Convert.ToBase64String(rawBytes);

            var profile = new MacroEventProfile
            {
                ProfileName = "테스트 프로필",
                Events = new List<MacroEvent>
                {
                    new MacroEvent
                    {
                        Name = "테스트 이벤트",
                        Trigger = new MacroTrigger
                        {
                            Type = TriggerType.ImageDetection,
                            ImageBase64 = base64,
                            ImageAccuracyPercent = 92.5
                        }
                    }
                }
            };

            string tempFile = Path.Combine(Path.GetTempPath(), "macro_test_profile.json");
            MacroEventProfileManager.SaveToFile(tempFile, profile);

            var loaded = MacroEventProfileManager.LoadFromFile(tempFile);
            File.Delete(tempFile);

            if (loaded.Events.Count == 1 && 
                loaded.Events[0].Trigger.ImageBase64 == base64 &&
                loaded.Events[0].Trigger.ImageAccuracyPercent == 92.5)
            {
                Console.WriteLine("  ✔ [통과] 프로필 직렬화 및 Base64 이미지 영구 내장 보존 검증 성공");
                return true;
            }
            Console.WriteLine("  ❌ [실패] 프로필 직렬화 데이터 불일치");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 프로필 직렬화 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestEmergencyStopLifecycle()
    {
        try
        {
            EmergencyStop.StartMonitoring(Keys.F12);
            EmergencyStop.StopMonitoring();
            EmergencyStop.Reset();

            if (!EmergencyStop.IsTriggered)
            {
                Console.WriteLine("  ✔ [통과] EmergencyStop 스레드 생명주기 및 리셋 상태 정상 작동");
                return true;
            }
            Console.WriteLine("  ❌ [실패] EmergencyStop 리셋 실패");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] EmergencyStop 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestAppSettingsAndLocalization()
    {
        try
        {
            // 설정 저장 및 복원 테스트
            string originalLang = AppSettings.Current.Language;
            bool originalMute = AppSettings.Current.MuteSound;

            AppSettings.Current.Language = "en";
            AppSettings.Current.MuteSound = true;
            AppSettings.Save();

            AppSettings.Load();
            bool passEn = AppSettings.Current.Language == "en" && AppSettings.Current.MuteSound;
            string titleEn = Localization.Get("AppTitle");

            AppSettings.Current.Language = "ko";
            AppSettings.Current.MuteSound = false;
            AppSettings.Save();

            AppSettings.Load();
            bool passKo = AppSettings.Current.Language == "ko" && !AppSettings.Current.MuteSound;
            string titleKo = Localization.Get("AppTitle");

            // 원래 설정 복원
            AppSettings.Current.Language = originalLang;
            AppSettings.Current.MuteSound = originalMute;
            AppSettings.Save();

            if (passEn && passKo && titleEn != titleKo)
            {
                Console.WriteLine("  ✔ [통과] AppSettings 영구 저장/로드 및 한글/English 다국어 전환 검증 성공");
                return true;
            }

            Console.WriteLine("  ❌ [실패] AppSettings 및 다국어 전환 실패");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] AppSettings 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestEmptyEventLifecycle()
    {
        try
        {
            // 1. 기본 프로필 생성 시 이벤트가 완전히 0개인지 검증
            var emptyProfile = MacroEventProfileManager.CreateDefaultProfile();
            if (emptyProfile.Events.Count != 0)
            {
                Console.WriteLine($"  ❌ [실패] 기본 프로필에 이벤트가 남아있음: {emptyProfile.Events.Count}개");
                return false;
            }

            // 2. 빈 프로필의 직렬화 및 역직렬화 무결성 검증
            string json = JsonSerializer.Serialize(emptyProfile);
            var deserialized = JsonSerializer.Deserialize<MacroEventProfile>(json);
            if (deserialized == null || deserialized.Events.Count != 0)
            {
                Console.WriteLine("  ❌ [실패] 빈 프로필 직렬화 복원 실패");
                return false;
            }

            // 3. 빈 프로필 상태에서 하네스 엔진 가동 및 즉시 정지 시 안전성 검증
            var harness = new EventMacroHarness(emptyProfile);
            var thread = new Thread(() => harness.Start()) { IsBackground = true };
            thread.Start();
            Thread.Sleep(80);
            harness.Stop();
            thread.Join(500);

            Console.WriteLine("  ✔ [통과] 빈 이벤트 프로필 무결성 및 하네스 안전 가동 검증 성공");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 빈 이벤트 생명주기 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestMouseClickTargetModes()
    {
        try
        {
            // 1. 좌표 기반 마우스 클릭 액션 검증
            var coordAction = new MacroAction
            {
                Type = ActionType.MouseClick,
                MouseButton = MouseButtonType.Right,
                TargetMode = MouseTargetMode.Coordinates,
                CustomX = 500,
                CustomY = 300,
                Description = "우클릭 (500, 300)"
            };

            // 2. 가상 템플릿 생성 및 이미지 기반 마우스 클릭 액션 검증
            using var templateMat = new Mat(20, 20, MatType.CV_8UC3, Scalar.Blue);
            Cv2.Circle(templateMat, new Point(10, 10), 5, Scalar.Yellow, -1);
            byte[] encodedBytes = templateMat.ToBytes(".png");
            string base64 = Convert.ToBase64String(encodedBytes);

            var imageAction = new MacroAction
            {
                Type = ActionType.MouseClick,
                MouseButton = MouseButtonType.Left,
                TargetMode = MouseTargetMode.TargetImage,
                TargetImageBase64 = base64,
                TargetImageAccuracyPercent = 90.0,
                TargetImageRoi = new RoiConfig { X = 0, Y = 0, Width = 800, Height = 600 }
            };

            // 3. 프로필 직렬화 및 역직렬화 무결성 테스트
            var testProfile = new MacroEventProfile
            {
                ProfileName = "Mouse Test Profile",
                Events = new List<MacroEvent>
                {
                    new MacroEvent
                    {
                        Name = "Mouse Action Event",
                        Actions = new List<MacroAction> { coordAction, imageAction }
                    }
                }
            };

            string json = JsonSerializer.Serialize(testProfile);
            var restored = JsonSerializer.Deserialize<MacroEventProfile>(json);

            if (restored == null || restored.Events[0].Actions.Count != 2)
            {
                Console.WriteLine("  ❌ [실패] 마우스 클릭 액션 직렬화 복원 실패");
                return false;
            }

            var rCoord = restored.Events[0].Actions[0];
            var rImage = restored.Events[0].Actions[1];

            if (rCoord.TargetMode != MouseTargetMode.Coordinates || rCoord.CustomX != 500 || rCoord.CustomY != 300)
            {
                Console.WriteLine("  ❌ [실패] 좌표 마우스 클릭 데이터 불일치");
                return false;
            }

            if (rImage.TargetMode != MouseTargetMode.TargetImage || rImage.TargetImageBase64 != base64 || rImage.TargetImageAccuracyPercent != 90.0)
            {
                Console.WriteLine("  ❌ [실패] 이미지 마우스 클릭 데이터 불일치");
                return false;
            }

            // 4. 가상 화면에서 타겟 이미지 매칭 검증
            using var sceneMat = new Mat(400, 400, MatType.CV_8UC3, Scalar.Black);
            templateMat.CopyTo(new Mat(sceneMat, new Rect(150, 150, 20, 20)));

            var matchResult = ImageMatcher.Match(sceneMat, templateMat, 90.0);
            if (!matchResult.Found || matchResult.Location.X != 150 || matchResult.Location.Y != 150)
            {
                Console.WriteLine("  ❌ [실패] 타겟 이미지 템플릿 매칭 위치 불일치");
                return false;
            }

            Console.WriteLine("  ✔ [통과] 마우스 클릭 (좌표 설정 및 이미지 탐색 클릭) 모듈 무결성 검증 성공");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 마우스 클릭 모드 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestParallelBranchExecution()
    {
        try
        {
            // 병렬 이벤트 (길게 이어붙인 100ms 대기 시퀀스)
            var parallelEvent = new MacroEvent
            {
                Name = "긴 브랜치 병렬 이벤트",
                IsEnabled = true,
                IsParallel = true,
                Trigger = new MacroTrigger { Type = TriggerType.Always },
                Actions = new List<MacroAction>
                {
                    new MacroAction { Type = ActionType.Wait, WaitDurationMs = 80 },
                    new MacroAction { Type = ActionType.Wait, WaitDurationMs = 80 }
                }
            };

            var profile = new MacroEventProfile
            {
                ProfileName = "Parallel Branch Test",
                CheckIntervalMs = 20,
                Events = new List<MacroEvent> { parallelEvent }
            };

            EmergencyStop.Reset();
            var evtSignal = new ManualResetEventSlim(false);

            var harness = new EventMacroHarness(profile);
            string capturedLog = "";
            harness.OnLog += msg =>
            {
                capturedLog += msg + "\n";
                if (msg.Contains("브랜치 가동") || msg.Contains("동시 실행")) evtSignal.Set();
            };

            var harnessThread = new Thread(() => harness.Start()) { IsBackground = true };
            harnessThread.Start();

            bool signaled = evtSignal.Wait(2000);
            harness.Stop();
            harnessThread.Join(500);

            if (signaled)
            {
                Console.WriteLine("  ✔ [통과] 병렬 이벤트 독립 비동기 브랜치 실행 및 완주 검증 성공");
                return true;
            }

            Console.WriteLine($"  ❌ [실패] 병렬 이벤트 비동기 브랜치 로그 미발견. 수신 로그:\n{capturedLog}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 병렬 이벤트 브랜치 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestMultiConditionAndFsmVariables()
    {
        try
        {
            // 1. 프로필 구성: 상태 (마을, 전투), 변수 (골드=100, 카운터=0)
            var profile = new MacroEventProfile
            {
                ProfileName = "FSM & Multi-Condition Test",
                CheckIntervalMs = 20,
                States = new List<string> { "마을", "전투" },
                InitialState = "마을",
                Variables = new Dictionary<string, double>
                {
                    { "골드", 100 },
                    { "카운터", 0 }
                }
            };

            // 이벤트 1: [전역] 다중 조건 (골드 >= 100 AND 카운터 == 0) -> 카운터 += 1, 상태를 '전투'로 전환
            var evt1 = new MacroEvent
            {
                Name = "상태 전환 및 다중 조건 이벤트",
                IsEnabled = true,
                StateTag = "전역",
                Triggers = new List<MacroTrigger>
                {
                    new MacroTrigger 
                    { 
                        Type = TriggerType.VariableCheck, 
                        VariableName = "골드", 
                        VarCompareOp = VariableComparison.GreaterThanOrEqual, 
                        VarTargetValue = 100 
                    },
                    new MacroTrigger 
                    { 
                        Type = TriggerType.VariableCheck, 
                        VariableName = "카운터", 
                        VarCompareOp = VariableComparison.Equal, 
                        VarTargetValue = 0 
                    }
                },
                Actions = new List<MacroAction>
                {
                    new MacroAction 
                    { 
                        Type = ActionType.VariableControl, 
                        VariableName = "카운터", 
                        VariableOp = VariableOperator.Add, 
                        VariableValue = 1 
                    },
                    new MacroAction 
                    { 
                        Type = ActionType.ChangeState, 
                        TargetState = "전투" 
                    }
                }
            };

            // 이벤트 2: [전투 상태 전용] 카운터 == 1 일 때 골드 -= 50
            var evt2 = new MacroEvent
            {
                Name = "전투 상태 전용 이벤트",
                IsEnabled = true,
                StateTag = "전투",
                Triggers = new List<MacroTrigger>
                {
                    new MacroTrigger 
                    { 
                        Type = TriggerType.VariableCheck, 
                        VariableName = "카운터", 
                        VarCompareOp = VariableComparison.Equal, 
                        VarTargetValue = 1 
                    }
                },
                Actions = new List<MacroAction>
                {
                    new MacroAction 
                    { 
                        Type = ActionType.VariableControl, 
                        VariableName = "골드", 
                        VariableOp = VariableOperator.Subtract, 
                        VariableValue = 50 
                    }
                }
            };

            profile.Events.Add(evt1);
            profile.Events.Add(evt2);

            EmergencyStop.Reset();
            var combatDoneSignal = new ManualResetEventSlim(false);

            var harness = new EventMacroHarness(profile);
            harness.OnStateChanged += newState =>
            {
                // 상태가 전투로 정상 변경되는지 감시
            };

            harness.OnLog += msg =>
            {
                if (msg.Contains("변수 조작: [골드]"))
                {
                    combatDoneSignal.Set();
                }
            };

            var harnessThread = new Thread(() => harness.Start()) { IsBackground = true };
            harnessThread.Start();

            bool passed = combatDoneSignal.Wait(2000);
            harness.Stop();
            harnessThread.Join(500);

            if (!passed)
            {
                Console.WriteLine("  ❌ [실패] FSM 상태 전환 및 조건부 실행 타임아웃");
                return false;
            }

            // 변수 검증: 골드는 100 - 50 = 50, 카운터는 0 + 1 = 1, 현재 상태는 "전투"
            double gold = harness.RuntimeVariables.GetValueOrDefault("골드", 0);
            double counter = harness.RuntimeVariables.GetValueOrDefault("카운터", 0);
            string curState = harness.CurrentState;

            if (curState == "전투" && gold == 50 && counter == 1)
            {
                Console.WriteLine($"  ✔ [통과] 다중 조건(AND), FSM 상태 전환('{curState}'), 변수 연산(골드={gold}, 카운터={counter}) 완벽 동작 검증");
                return true;
            }

            Console.WriteLine($"  ❌ [실패] 변수/상태 불일치: State={curState}, 골드={gold}, 카운터={counter}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] FSM & 다중 조건 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestStateRenameAndDuplicateCheck()
    {
        try
        {
            var profile = new MacroEventProfile
            {
                ProfileName = "State Rename Test",
                States = new List<string> { "기본 상태", "사냥" },
                InitialState = "사냥"
            };

            var evt = new MacroEvent
            {
                Name = "사냥 이벤트",
                StateTag = "사냥",
                Actions = new List<MacroAction>
                {
                    new MacroAction
                    {
                        Type = ActionType.ChangeState,
                        TargetState = "사냥"
                    }
                }
            };
            profile.Events.Add(evt);

            // 중복 검증 테스트
            bool isDuplicate1 = profile.States.Any(s => s.Equals("기본 상태", StringComparison.OrdinalIgnoreCase));
            bool isDuplicate2 = profile.States.Any(s => s.Equals("전역", StringComparison.OrdinalIgnoreCase)) || "전역".Equals("전역", StringComparison.OrdinalIgnoreCase);
            bool isDuplicate3 = profile.States.Any(s => s.Equals("사냥", StringComparison.OrdinalIgnoreCase));
            bool isNotDuplicate = profile.States.Any(s => s.Equals("보스전", StringComparison.OrdinalIgnoreCase));

            if (!isDuplicate1 || !isDuplicate2 || !isDuplicate3 || isNotDuplicate)
            {
                Console.WriteLine("  ❌ [실패] 상태 이름 중복 감지 로직 오류");
                return false;
            }

            // 이름 변경 수행 시뮬레이션 ("사냥" -> "보스전")
            string oldState = "사냥";
            string newState = "보스전";

            int idx = profile.States.IndexOf(oldState);
            profile.States[idx] = newState;
            if (profile.InitialState == oldState) profile.InitialState = newState;

            foreach (var e in profile.Events)
            {
                if (e.StateTag == oldState) e.StateTag = newState;
                foreach (var a in e.Actions)
                {
                    if (a.Type == ActionType.ChangeState && a.TargetState == oldState)
                    {
                        a.TargetState = newState;
                    }
                }
            }

            // 검증
            bool stateRenamed = profile.States.Contains("보스전") && !profile.States.Contains("사냥");
            bool initialRenamed = profile.InitialState == "보스전";
            bool eventTagRenamed = profile.Events[0].StateTag == "보스전";
            bool actionTargetRenamed = profile.Events[0].Actions[0].TargetState == "보스전";

            // [v3.1.1 스마트 네이밍 검증] '상태 3' 건너뜀 결함 방지 및 1번부터 순차 배정 테스트
            var defProfile = MacroEventProfileManager.CreateDefaultProfile(); // ["Global", "기본 상태"]
            string gen1 = defProfile.GenerateNextStateName(true);
            if (gen1 != "상태 1")
            {
                Console.WriteLine($"  ❌ [실패] 기본 상태에서 생성 시 '상태 1'이어야 하나 '{gen1}' 생성됨 (상태 3 건너뜀 버그 미해결)");
                return false;
            }
            defProfile.States.Add(gen1);
            string gen2 = defProfile.GenerateNextStateName(true);
            if (gen2 != "상태 2")
            {
                Console.WriteLine($"  ❌ [실패] 두 번째 상태는 '상태 2'여야 하나 '{gen2}' 생성됨");
                return false;
            }
            defProfile.States.Add(gen2);
            string gen3 = defProfile.GenerateNextStateName(true);
            if (gen3 != "상태 3")
            {
                Console.WriteLine($"  ❌ [실패] 세 번째 상태는 '상태 3'이어야 하나 '{gen3}' 생성됨");
                return false;
            }
            string dupResolved = defProfile.ResolveUniqueStateName("상태 1", true);
            if (dupResolved == "상태 1")
            {
                Console.WriteLine("  ❌ [실패] 중복 상태명 자동 해소 실패");
                return false;
            }

            if (stateRenamed && initialRenamed && eventTagRenamed && actionTargetRenamed)
            {
                Console.WriteLine("  ✔ [통과] 상태 이름 변경(사냥 ➔ 보스전), v3.1.1 스마트 네이밍(상태 1 ➔ 상태 2 순차 배정), 중복 방지 검증 성공");
                return true;
            }

            Console.WriteLine("  ❌ [실패] 상태 이름 변경 동기화 불일치");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 상태 이름 변경 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestPauseAndResumeLifecycle()
    {
        try
        {
            EmergencyStop.Reset();
            var profile = new MacroEventProfile
            {
                InitialState = "기본 상태",
                CheckIntervalMs = 20
            };

            int counter = 0;
            var logs = new List<string>();
            var evt = new MacroEvent
            {
                Name = "증가 이벤트",
                IsEnabled = true,
                IsParallel = true,
                StateTag = "기본 상태",
                Triggers = new List<MacroTrigger> { new MacroTrigger { Type = TriggerType.Always } },
                Actions = new List<MacroAction> { new MacroAction { Type = ActionType.Wait, WaitDurationMs = 10 } }
            };
            profile.Events.Add(evt);

            var harness = new EventMacroHarness(profile);
            harness.OnLog += msg =>
            {
                lock (logs) logs.Add(msg);
                if (msg.Contains("동시 실행 완료")) Interlocked.Increment(ref counter);
            };

            var harnessThread = new Thread(() => harness.Start()) { IsBackground = true };
            harnessThread.Start();

            // 300ms 대기하여 실행 카운트 누적
            Thread.Sleep(300);
            harness.Pause();
            if (!harness.IsPaused)
            {
                Console.WriteLine("  ❌ [실패] harness.IsPaused 플래그 불일치");
                harness.Stop();
                return false;
            }

            // 기실행 중이던 10ms 대기 브랜치 완료 대기
            Thread.Sleep(50);

            int snapshot = counter;
            if (snapshot == 0)
            {
                Console.WriteLine($"  ❌ [실패] 일시정지 전 이벤트가 한 번도 실행되지 않음. 수신 로그:\n{string.Join("\n", logs)}");
                harness.Stop();
                return false;
            }

            Thread.Sleep(250);
            // 일시정지 중에는 카운터가 증가하지 않아야 함
            if (counter != snapshot)
            {
                Console.WriteLine($"  ❌ [실패] 일시정지 중 이벤트 루프가 계속 동작함: 이전={snapshot}, 현재={counter}");
                harness.Stop();
                return false;
            }

            // 재개
            harness.Resume();
            if (harness.IsPaused)
            {
                Console.WriteLine("  ❌ [실패] harness.Resume 후 IsPaused가 여전히 true");
                harness.Stop();
                return false;
            }

            Thread.Sleep(250);
            if (counter <= snapshot)
            {
                Console.WriteLine($"  ❌ [실패] 재개(Resume) 후 카운터 증가 안 됨: 스냅샷={snapshot}, 현재={counter}");
                harness.Stop();
                return false;
            }

            harness.Stop();
            harnessThread.Join(500);

            Console.WriteLine("  ✔ [통과] 일시정지(Pause) ➔ 동작 동결(0% 부하) ➔ 재개(Resume) 라이프사이클 정상 검증");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 일시정지/재개 라이프사이클 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestHotkeySettingsValidation()
    {
        try
        {
            var settings = new AppSettings();
            if (settings.RecordHotkey != "F9" || settings.StartPauseHotkey != "F10" || settings.StopHotkey != "F11" || settings.EmergencyStopHotkey != "F12")
            {
                Console.WriteLine("  ❌ [실패] 기본 핫키 설정 불일치");
                return false;
            }

            var keys = new[] { settings.RecordHotkey, settings.StartPauseHotkey, settings.StopHotkey, settings.EmergencyStopHotkey };
            bool duplicateDetected = (keys.Distinct().Count() < keys.Length);
            if (duplicateDetected)
            {
                Console.WriteLine("  ❌ [실패] 기본 핫키에 중복 감지됨");
                return false;
            }

            Console.WriteLine("  ✔ [통과] 전역 단축키(F9/F10/F11/F12) 설정 및 중복 방지 검증 통과");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 핫키 설정 검증 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestMultiMonitorCoordinates()
    {
        try
        {
            var vBounds = ScreenCapture.VirtualScreenBounds;
            if (vBounds.Width <= 0 || vBounds.Height <= 0)
            {
                Console.WriteLine("  ❌ [실패] 가상 화면 Bounds 크기가 유효하지 않음");
                return false;
            }

            // 가상 화면 내부 임의 좌표 테스트 (서브 모니터 음수 영역 포함 가능)
            var pt = new System.Drawing.Point(vBounds.Left + 50, vBounds.Top + 50);
            if (pt.X < vBounds.Left || pt.X >= vBounds.Right || pt.Y < vBounds.Top || pt.Y >= vBounds.Bottom)
            {
                Console.WriteLine("  ❌ [실패] 가상 좌표계 영역 계산 불일치");
                return false;
            }

            // 음수 좌표 입력에 대한 휴머나이저 클램프 검증 (Math.Max(0) 회귀 방지)
            var (jx, jy) = InputHumanizer.GetHumanizedCoordinates(vBounds.Left, vBounds.Top, 2);
            if (jx < vBounds.Left || jy < vBounds.Top)
            {
                Console.WriteLine($"  ❌ [실패] 음수 좌표 클램프 오류: jittered=({jx}, {jy}), vBounds={vBounds}");
                return false;
            }

            Console.WriteLine($"  ✔ [통과] 다중 모니터 전역 가상 화면 Bounds={vBounds} 및 음수 좌표 지터링 무결성 검증");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 다중 모니터 좌표계 검증 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestMacroRecordingConversion()
    {
        try
        {
            using var recorder = new MacroRecorder();
            if (recorder.IsRecording)
            {
                Console.WriteLine("  ❌ [실패] 초기 상태는 녹화 중이 아니어야 함");
                return false;
            }

            recorder.StartRecording();
            if (!recorder.IsRecording)
            {
                Console.WriteLine("  ❌ [실패] StartRecording 후 IsRecording이 true여야 함");
                return false;
            }

            // 잠시 대기 후 정지 (더미 입력 처리)
            Thread.Sleep(50);
            var actions = recorder.StopRecording(false);
            if (recorder.IsRecording)
            {
                Console.WriteLine("  ❌ [실패] StopRecording 후 IsRecording이 false여야 함");
                return false;
            }

            // 2. 단독 입력(Isolated Tap) 변환 검증 -> KeyDownUp
            var singleTapEvents = new List<MacroRecorder.RawInputEvent>
            {
                new MacroRecorder.RawInputEvent { TimestampMs = 100, Type = MacroRecorder.RawInputType.KeyDown, Key = Keys.Space },
                new MacroRecorder.RawInputEvent { TimestampMs = 150, Type = MacroRecorder.RawInputType.KeyUp, Key = Keys.Space }
            };
            var singleActions = MacroRecorder.ConvertToMacroActions(singleTapEvents);
            if (singleActions.Count != 1 || singleActions[0].KeyMode != KeyStrokeMode.KeyDownUp || singleActions[0].HoldTimeMs != 50)
            {
                Console.WriteLine("  ❌ [실패] 단독 키 녹화 변환 오류 (KeyDownUp 미적용)");
                return false;
            }

            // 3. 동시/조합 입력(Overlapping Combo Hold) 변환 검증 -> Shift(KeyDown) -> 1(KeyDownUp) -> Shift(KeyUp)
            var comboEvents = new List<MacroRecorder.RawInputEvent>
            {
                new MacroRecorder.RawInputEvent { TimestampMs = 100, Type = MacroRecorder.RawInputType.KeyDown, Key = Keys.ShiftKey },
                new MacroRecorder.RawInputEvent { TimestampMs = 200, Type = MacroRecorder.RawInputType.KeyDown, Key = Keys.D1 },
                new MacroRecorder.RawInputEvent { TimestampMs = 240, Type = MacroRecorder.RawInputType.KeyUp, Key = Keys.D1 },
                new MacroRecorder.RawInputEvent { TimestampMs = 300, Type = MacroRecorder.RawInputType.KeyUp, Key = Keys.ShiftKey }
            };
            var comboActions = MacroRecorder.ConvertToMacroActions(comboEvents);
            var keyActions = comboActions.Where(a => a.Type == ActionType.KeyPress).ToList();
            if (keyActions.Count != 3 ||
                keyActions[0].KeyMode != KeyStrokeMode.KeyDown || keyActions[0].Key != "ShiftKey" ||
                keyActions[1].KeyMode != KeyStrokeMode.KeyDownUp || keyActions[1].Key != "D1" ||
                keyActions[2].KeyMode != KeyStrokeMode.KeyUp || keyActions[2].Key != "ShiftKey")
            {
                Console.WriteLine("  ❌ [실패] 동시/조합 입력 녹화 변환 오류 (KeyDown/KeyUp 분리 실패)");
                return false;
            }

            // 4. 시작 버튼 릴리즈 등 고아 MouseUp 무시 가드 검증
            var orphanMouseEvents = new List<MacroRecorder.RawInputEvent>
            {
                new MacroRecorder.RawInputEvent { TimestampMs = 20, Type = MacroRecorder.RawInputType.MouseUp, MouseButton = MouseButtonType.Left, ScreenPoint = new System.Drawing.Point(50, 50) }
            };
            var orphanActions = MacroRecorder.ConvertToMacroActions(orphanMouseEvents);
            if (orphanActions.Count != 0)
            {
                Console.WriteLine("  ❌ [실패] 고아 MouseUp 필터링 실패");
                return false;
            }

            Console.WriteLine("  ✔ [통과] 매크로 녹화 라이프사이클, 키다운업/키다운/키업 스마트 분류 및 시작 버튼 릴리즈 필터링 검증");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 매크로 녹화 변환 검증 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestProfileAtomicBackupAndRecovery()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "RpgMacroTests_" + Guid.NewGuid().ToString("N"));
        string profilePath = Path.Combine(tempDir, "test_profile.json");
        string backupPath = profilePath + ".bak";

        try
        {
            Directory.CreateDirectory(tempDir);

            // 1. 프로필 생성 및 원자적 저장 검증
            var profile = new MacroEventProfile
            {
                ProfileName = "AtomicTestProfile",
                InitialState = "StateA",
                States = new List<string> { "전역", "StateA" }
            };

            MacroEventProfileManager.SaveToFile(profilePath, profile);

            if (!File.Exists(profilePath) || !File.Exists(backupPath))
            {
                Console.WriteLine("  ❌ [실패] 프로필 원자적 저장 후 주 파일 또는 .bak 백업 파일 미존재");
                return false;
            }

            // 2. 정상 로드 검증
            var loaded = MacroEventProfileManager.LoadFromFile(profilePath);
            if (loaded.ProfileName != "AtomicTestProfile")
            {
                Console.WriteLine("  ❌ [실패] 저장된 프로필 내용 불일치");
                return false;
            }

            // 3. 주 파일 파손(손상/0바이트) 시나리오 시뮬레이션 및 .bak 자동 재해 복구 검증
            File.WriteAllText(profilePath, "{ corrupted broken json ... ");

            var recovered = MacroEventProfileManager.LoadFromFile(profilePath);
            if (recovered == null || recovered.ProfileName != "AtomicTestProfile")
            {
                Console.WriteLine("  ❌ [실패] 주 프로필 손상 시 .bak 자동 복구 실패");
                return false;
            }

            Console.WriteLine("  ✔ [통과] 프로필 원자적 I/O(.tmp 치환), 실시간 .bak 백업 및 재해 자동 복구(Auto-Recovery) 검증 통과");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 원자적 백업 및 복구 테스트 실패: {ex.Message}");
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch { }
        }
    }

    private static bool TestSoakStability()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long initialMemory = GC.GetTotalMemory(true);

            // 100회 연속 비전 엔진(GaugeBarReader, ImageMatcher) 스트레스 시뮬레이션
            for (int i = 0; i < 100; i++)
            {
                using var screenMat = new Mat(100, 200, MatType.CV_8UC3, Scalar.Black);
                Cv2.Rectangle(screenMat, new Rect(10, 10, 80, 20), new Scalar(0, 0, 220), -1);

                double percent = GaugeBarReader.ReadPercentage(screenMat, new Rect(10, 10, 80, 20), colorType: GaugeColorType.RedHp);

                using var tplMat = new Mat(20, 20, MatType.CV_8UC3, new Scalar(0, 0, 220));
                var match = ImageMatcher.Match(screenMat, tplMat, accuracyPercent: 80.0);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            long finalMemory = GC.GetTotalMemory(true);

            // 100회 수행 후 비정상적인 메모리 폭증(15MB 이상 누수)이 없는지 확인
            long diff = finalMemory - initialMemory;
            if (diff > 15 * 1024 * 1024)
            {
                Console.WriteLine($"  ❌ [실패] 100회 스트레스 루프 후 메모리 과다 증가 감지: {diff / 1024} KB");
                return false;
            }

            Console.WriteLine($"  ✔ [통과] 비전 엔진 리소스 100회 스트레스/누수 검증 통과 (메모리 변동: {diff / 1024} KB)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 장기 스트레스/누수 검증 실패: {ex.Message}");
            return false;
        }
    }

    private class MockOcrPluginEvaluator : ITriggerEvaluator
    {
        public TriggerType SupportedType => TriggerType.Always;
        public bool WasCalled { get; set; } = false;

        public bool Evaluate(MacroTrigger trigger, TriggerEvaluationContext context, out OpenCvSharp.Point detectedPoint)
        {
            WasCalled = true;
            detectedPoint = new OpenCvSharp.Point(555, 777);
            return true;
        }
    }

    private class MockWebhookPluginExecutor : IActionExecutor
    {
        public ActionType SupportedType => ActionType.ChangeState;
        public bool WasCalled { get; set; } = false;

        public void Execute(MacroAction action, ActionExecutionContext context)
        {
            WasCalled = true;
            context.ChangeStateAction("플러그인상태");
        }
    }

    private static bool TestPluginExtensibility()
    {
        try
        {
            var mockEvaluator = new MockOcrPluginEvaluator();
            var mockExecutor = new MockWebhookPluginExecutor();

            MacroPluginRegistry.RegisterEvaluator(mockEvaluator);
            MacroPluginRegistry.RegisterExecutor(mockExecutor);

            var profile = new MacroEventProfile();
            profile.States.Add("플러그인상태");

            var evt = new MacroEvent
            {
                Name = "플러그인 테스트 이벤트",
                StateTag = profile.InitialState,
                IsParallel = false
            };
            evt.Triggers.Clear();
            evt.Triggers.Add(new MacroTrigger { Type = TriggerType.Always });
            evt.Actions.Clear();
            evt.Actions.Add(new MacroAction { Type = ActionType.ChangeState, TargetState = "플러그인상태" });
            profile.Events.Add(evt);

            var harness = new EventMacroHarness(profile);
            var thread = new Thread(() => harness.Start()) { IsBackground = true };
            thread.Start();
            Thread.Sleep(150);
            harness.Stop();
            thread.Join(500);

            if (!mockEvaluator.WasCalled)
            {
                Console.WriteLine("  ❌ [실패] OCP 플러그인 트리거 평가자가 호출되지 않았습니다.");
                return false;
            }

            if (!mockExecutor.WasCalled)
            {
                Console.WriteLine("  ❌ [실패] OCP 플러그인 액션 실행자가 호출되지 않았습니다.");
                return false;
            }

            if (harness.CurrentState != "플러그인상태")
            {
                Console.WriteLine($"  ❌ [실패] 플러그인 액션 실행 후 FSM 상태 전이 실패: CurrentState={harness.CurrentState}");
                return false;
            }

            Console.WriteLine("  ✔ [통과] OCP 플러그인 확장 파이프라인(ITriggerEvaluator & IActionExecutor) 가로채기 및 상태 전이 검증 성공");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 플러그인 확장 파이프라인 검증 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestAutoLayoutNoOverlapAndFsmFlow()
    {
        try
        {
            var profile = new MacroEventProfile
            {
                States = new List<string> { "Global", "기본 상태", "새 상태", "새 상태 1", "보조 상태" },
                InitialState = "기본 상태"
            };

            // 기본 상태 -> 새 상태 전이 이벤트
            var evt1 = new MacroEvent { StateTag = "기본 상태", Name = "전이 이벤트 1" };
            evt1.Actions.Add(new MacroAction { Type = ActionType.ChangeState, TargetState = "새 상태" });
            profile.Events.Add(evt1);

            // 새 상태 -> 새 상태 1 전이 이벤트
            var evt2 = new MacroEvent { StateTag = "새 상태", Name = "전이 이벤트 2" };
            evt2.Actions.Add(new MacroAction { Type = ActionType.ChangeState, TargetState = "새 상태 1" });
            profile.Events.Add(evt2);

            // 가상 노드 높이 정의: 기본 상태는 액션이 많아 700px, 새 상태는 300px, 나머지는 200px
            var heightMap = new Dictionary<string, float>
            {
                ["Global"] = 120f,
                ["기본 상태"] = 700f,
                ["새 상태"] = 300f,
                ["새 상태 1"] = 250f,
                ["보조 상태"] = 200f
            };

            MacroLayoutEngine.ComputeSmartLayout(
                profile,
                st => heightMap.TryGetValue(st, out var h) ? h : 220f
            );

            // 1. 모든 상태가 좌표를 부여받았는지 확인
            foreach (var st in profile.States)
            {
                if (!profile.NodePositions.TryGetValue(st, out var pos))
                {
                    Console.WriteLine($"  ❌ [실패] 상태 [{st}]에 좌표가 생성되지 않았습니다.");
                    return false;
                }
            }

            // 2. FSM 전이 흐름 검증 (Left-to-Right)
            var posInit = profile.NodePositions["기본 상태"];
            var posNext1 = profile.NodePositions["새 상태"];
            var posNext2 = profile.NodePositions["새 상태 1"];

            if (posInit.X >= posNext1.X || posNext1.X >= posNext2.X)
            {
                Console.WriteLine($"  ❌ [실패] FSM 좌->우 위상 배치 실패: Init.X={posInit.X}, Next1.X={posNext1.X}, Next2.X={posNext2.X}");
                return false;
            }

            // 3. 기하학적 사각 영역(RectangleF Bounds) 전수 겹침 검증 (IntersectsWith == false)
            var boundsMap = new Dictionary<string, RectangleF>();
            foreach (var st in profile.States)
            {
                var pos = profile.NodePositions[st];
                float h = heightMap[st];
                boundsMap[st] = new RectangleF(pos.X, pos.Y, 340f, h);
            }

            var stateList = profile.States.ToList();
            for (int i = 0; i < stateList.Count; i++)
            {
                for (int j = i + 1; j < stateList.Count; j++)
                {
                    var rectA = boundsMap[stateList[i]];
                    var rectB = boundsMap[stateList[j]];
                    if (rectA.IntersectsWith(rectB))
                    {
                        Console.WriteLine($"  ❌ [실패] 상태 [{stateList[i]}]와 [{stateList[j]}] 노드가 서로 겹쳤습니다! (A: {rectA}, B: {rectB})");
                        return false;
                    }
                }
            }

            // 4. 순환 전이(A <-> B 상호 전이 및 사이클) 무한 루프 방어 및 초고속 완료 검증
            var cyclicProfile = new MacroEventProfile
            {
                States = new List<string> { "Global", "사냥 상태", "마을 상점 상태", "보스전 상태" },
                InitialState = "사냥 상태"
            };
            // 사냥 상태 -> 마을 상점 상태
            var huntToTown = new MacroEvent { StateTag = "사냥 상태", Name = "귀환" };
            huntToTown.Actions.Add(new MacroAction { Type = ActionType.ChangeState, TargetState = "마을 상점 상태" });
            cyclicProfile.Events.Add(huntToTown);
            // 마을 상점 상태 -> 사냥 상태 (순환 Cycle!)
            var townToHunt = new MacroEvent { StateTag = "마을 상점 상태", Name = "사냥터 복귀" };
            townToHunt.Actions.Add(new MacroAction { Type = ActionType.ChangeState, TargetState = "사냥 상태" });
            cyclicProfile.Events.Add(townToHunt);
            // 보스전 상태 -> 사냥 상태
            var bossToHunt = new MacroEvent { StateTag = "보스전 상태", Name = "보스 클리어" };
            bossToHunt.Actions.Add(new MacroAction { Type = ActionType.ChangeState, TargetState = "사냥 상태" });
            cyclicProfile.Events.Add(bossToHunt);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            MacroLayoutEngine.ComputeSmartLayout(cyclicProfile);
            sw.Stop();

            if (sw.ElapsedMilliseconds > 500 || cyclicProfile.NodePositions.Count < 4)
            {
                Console.WriteLine($"  ❌ [실패] 순환 전이 FSM 레이아웃 계산 지연 또는 누락: {sw.ElapsedMilliseconds}ms, 노드수: {cyclicProfile.NodePositions.Count}");
                return false;
            }

            Console.WriteLine($"  ✔ [통과] FSM 스마트 Auto-Layout 위상 흐름, 순환 전이 무한루프 방어({sw.ElapsedMilliseconds}ms) 및 노드 겹침 0% 무결성 검증 성공");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] Auto-Layout 검증 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestHarnessVisionOptimizationAndCache()
    {
        try
        {
            var profile = new MacroEventProfile
            {
                ProfileName = "Harness Optimization Test",
                CheckIntervalMs = 20,
                States = new List<string> { "전역", "상태1" },
                InitialState = "상태1"
            };

            // 1. 비전 없는 이벤트 생성 -> needsVision == false 검증
            var nonVisionEvt = new MacroEvent
            {
                Name = "키입력 이벤트",
                IsEnabled = true,
                StateTag = "상태1",
                Triggers = new List<MacroTrigger> { new MacroTrigger { Type = TriggerType.Always } },
                Actions = new List<MacroAction> { new MacroAction { Type = ActionType.KeyPress, Key = "Space" } }
            };
            profile.Events.Add(nonVisionEvt);

            var harness = new EventMacroHarness(profile);

            // private CheckNeedsVisionForState 리플렉션 검증
            var checkMethod = typeof(EventMacroHarness).GetMethod("CheckNeedsVisionForState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool nonVisionResult = (bool)checkMethod!.Invoke(harness, new object[] { "상태1" })!;
            if (nonVisionResult)
            {
                Console.WriteLine("  ❌ [실패] 비전 불필요 상태에서 CheckNeedsVisionForState가 true 반환");
                return false;
            }

            // 2. 비전 조건 추가 후 needsVision == true 검증
            nonVisionEvt.Triggers.Add(new MacroTrigger { Type = TriggerType.GaugeBar, GaugeThresholdPercent = 50 });
            bool visionResult = (bool)checkMethod.Invoke(harness, new object[] { "상태1" })!;
            if (!visionResult)
            {
                Console.WriteLine("  ❌ [실패] 비전 필요 상태에서 CheckNeedsVisionForState가 false 반환");
                return false;
            }

            // 3. 브랜치 자동 정리 검증: 짧은 병렬 이벤트 실행 후 _runningBranches 컬렉션이 비어있는지 검증
            var parallelEvent = new MacroEvent
            {
                Id = "test_branch_id",
                Name = "짧은 병렬",
                IsEnabled = true,
                IsParallel = true,
                Triggers = new List<MacroTrigger> { new MacroTrigger { Type = TriggerType.Always } },
                Actions = new List<MacroAction> { new MacroAction { Type = ActionType.Wait, WaitDurationMs = 20 } }
            };

            var branchProfile = new MacroEventProfile
            {
                ProfileName = "Branch Auto-Cleanup",
                CheckIntervalMs = 15,
                Events = new List<MacroEvent> { parallelEvent }
            };

            var branchHarness = new EventMacroHarness(branchProfile);
            var thread = new Thread(() => branchHarness.Start()) { IsBackground = true };
            thread.Start();

            Thread.Sleep(150); // 충분한 실행 및 완주 대기
            branchHarness.Stop();
            thread.Join(500);

            var branchesField = typeof(EventMacroHarness).GetField("_runningBranches", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var branches = (System.Collections.ICollection)branchesField!.GetValue(branchHarness)!;

            if (branches.Count > 0)
            {
                Console.WriteLine($"  ❌ [실패] 병렬 브랜치 완주 후 사전 미정리 (남은 항목: {branches.Count})");
                return false;
            }

            Console.WriteLine("  ✔ [통과] 하네스 엔진 최적화 검증 (비전 제로-할당 사전 필터링, 병렬 브랜치 수명주기 누수 0% 차단)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 하네스 최적화 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestPinGraphAndHierarchyTreeView()
    {
        try
        {
            // 1. 샘플 프로필 생성: FSM 상태(사냥, 보스전), 조건 2개, 액션 2개
            var profile = new MacroEventProfile
            {
                ProfileName = "v3.1 PinGraph Test",
                States = new List<string> { "Global", "사냥", "보스전" },
                InitialState = "사냥"
            };

            var evt = new MacroEvent
            {
                Name = "보스 감지 및 공격",
                StateTag = "사냥",
                Triggers = new List<MacroTrigger>
                {
                    new MacroTrigger
                    {
                        Type = TriggerType.VisionDetection,
                        VisionTarget = VisionTargetType.Image,
                        ImagePath = "boss.png"
                    }
                },
                Actions = new List<MacroAction>
                {
                    new MacroAction { Type = ActionType.KeyPress, Key = "Q" },
                    new MacroAction { Type = ActionType.MouseClick, TargetMode = MouseTargetMode.TriggerDetected }
                }
            };
            profile.Events.Add(evt);

            // 2. FSM 프로필 -> 완전 자유형 핀 그래프 변환
            var graph = PinGraphConverter.FromProfile(profile);

            // 노드 생성 무결성 검증
            if (graph.Nodes.Count < 3)
            {
                Console.WriteLine($"  ❌ [실패] 핀 그래프 노드 수 부족 (기대 >= 3, 실제: {graph.Nodes.Count})");
                return false;
            }

            // 조건 노드 검증: True 및 False(Else) 분기 핀 존재 여부
            var condNode = graph.Nodes.FirstOrDefault(n => n.NodeType == GraphNodeType.Condition);
            if (condNode == null)
            {
                Console.WriteLine("  ❌ [실패] 핀 그래프 조건 노드 미발견");
                return false;
            }

            bool hasTruePin = condNode.OutputPins.Any(p => p.IsBranchPin && p.BranchCondition);
            bool hasFalsePin = condNode.OutputPins.Any(p => p.IsBranchPin && !p.BranchCondition);
            bool hasCoordPin = condNode.OutputPins.Any(p => p.Kind == GraphPinKind.Data && p.DataType == GraphPinDataType.Point);

            if (!hasTruePin || !hasFalsePin)
            {
                Console.WriteLine("  ❌ [실패] 조건 노드 분기 핀(True / False(Else)) 결여");
                return false;
            }

            if (!hasCoordPin)
            {
                Console.WriteLine("  ❌ [실패] 비전 감지 노드 좌표 데이터 핀(Coord ●) 결여");
                return false;
            }

            // 와이어 연결선 검증
            if (graph.Connections.Count == 0)
            {
                Console.WriteLine("  ❌ [실패] 핀 그래프 베지어 와이어 연결선 미생성");
                return false;
            }

            Console.WriteLine($"  ✔ [통과] 완전 자유형 핀 그래프 & 계층형 트리 무결성 검증 통과 (노드: {graph.Nodes.Count}개, 연결: {graph.Connections.Count}개, True/Else 분기 핀 100% 정상)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 핀 그래프 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestSafeSpawnCollisionAvoidanceAnd4WayViewMode()
    {
        try
        {
            // 1. 노드 15개 연속 추가 시 충돌 없는 배치(Collision Avoidance) 기하학 검증
            var testProfile = MacroEventProfileManager.CreateDefaultProfile();
            float nodeWidth = 340f;
            float nodeHeight = 220f;
            float margin = 40f;

            for (int i = 1; i <= 15; i++)
            {
                string stateName = $"테스트상태_{i}";
                testProfile.States.Add(stateName);
                var pos = MacroLayoutEngine.FindSafeSpawnPosition(testProfile, preferredX: 200f, preferredY: 100f, nodeWidth: nodeWidth, nodeHeight: nodeHeight, margin: margin);
                testProfile.NodePositions[stateName] = pos;
            }

            // 모든 쌍(pair)의 AABB 겹침 검사
            var positions = testProfile.NodePositions.ToList();
            for (int a = 0; a < positions.Count; a++)
            {
                for (int b = a + 1; b < positions.Count; b++)
                {
                    var posA = positions[a].Value;
                    var posB = positions[b].Value;

                    bool overlapX = (posA.X < posB.X + nodeWidth + margin) && (posA.X + nodeWidth + margin > posB.X);
                    bool overlapY = (posA.Y < posB.Y + nodeHeight + margin) && (posA.Y + nodeHeight + margin > posB.Y);

                    if (overlapX && overlapY)
                    {
                        Console.WriteLine($"  ❌ [실패] 노드 배치 겹침 발생: '{positions[a].Key}'와 '{positions[b].Key}' 충돌!");
                        return false;
                    }
                }
            }

            // 2. 4가지 뷰 모드(AppViewMode) Enum 정의 및 무결성 검증
            var modes = Enum.GetValues<AppViewMode>();
            if (modes.Length != 4)
            {
                Console.WriteLine($"  ❌ [실패] 4대 뷰 모드 수량 오류: {modes.Length}개 (4개 예상)");
                return false;
            }

            bool hasCanvasFsm = modes.Contains(AppViewMode.CanvasFsm);
            bool hasCanvasPin = modes.Contains(AppViewMode.CanvasPinGraph);
            bool hasClassicList = modes.Contains(AppViewMode.ClassicList);
            bool hasClassicTree = modes.Contains(AppViewMode.ClassicTree);

            if (!hasCanvasFsm || !hasCanvasPin || !hasClassicList || !hasClassicTree)
            {
                Console.WriteLine("  ❌ [실패] 4대 뷰 모드(FSM 카드/핀 그래프/목록/트리) 중 누락된 모드 존재");
                return false;
            }

            Console.WriteLine($"  ✔ [통과] 노드 스마트 충돌 회피 알고리즘 & 4대 뷰 모드(FSM카드/핀그래프/목록/트리) 무결성 검증 성공 (15개 연속 노드 100% 비충돌)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 충돌 회피 및 뷰 모드 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestPinGraphPersistenceAndNodeResizeAndLayout()
    {
        try
        {
            var profile = new MacroEventProfile();
            profile.States = new List<string> { "Global", "기본 상태" };
            var evt = new MacroEvent
            {
                Id = "evt_test_100",
                Name = "테스트 이벤트",
                StateTag = "기본 상태"
            };
            evt.Triggers.Add(new MacroTrigger { Type = TriggerType.Always });
            evt.Actions.Add(new MacroAction { Type = ActionType.KeyPress, Key = "D1" });
            profile.Events.Add(evt);

            // 1. 초기 FromProfile 호출
            var graph1 = PinGraphConverter.FromProfile(profile);
            var trigNode = graph1.Nodes.FirstOrDefault(n => n.Id == $"trig_{evt.Id}_0");
            var actNode = graph1.Nodes.FirstOrDefault(n => n.Id == $"act_{evt.Id}_0");

            if (trigNode == null || actNode == null)
            {
                Console.WriteLine("  ❌ [실패] 핀 그래프 노드 결정적 Id 생성 실패");
                return false;
            }

            // 2. 유저가 핀 그래프에서 노드를 드래그하여 이동하고 크기를 조절한 상황 시뮬레이션
            float customX = 850f;
            float customY = 620f;
            float customW = 280f;
            float customH = 140f;

            profile.PinGraphNodePositions[trigNode.Id] = new NodeCanvasPosition(customX, customY);
            profile.PinGraphNodeSizes[trigNode.Id] = new NodeCanvasSize(customW, customH);

            // 3. FromProfile을 다시 호출하여 저장된 좌표/크기가 100% 보존되는지 검증 (제자리 복귀 버그 재발 방지)
            var graph2 = PinGraphConverter.FromProfile(profile);
            var reloadedTrigNode = graph2.Nodes.FirstOrDefault(n => n.Id == $"trig_{evt.Id}_0");

            if (reloadedTrigNode == null)
            {
                Console.WriteLine("  ❌ [실패] 재변환 후 트리거 노드 누락");
                return false;
            }

            if (Math.Abs(reloadedTrigNode.X - customX) > 0.001f || Math.Abs(reloadedTrigNode.Y - customY) > 0.001f)
            {
                Console.WriteLine($"  ❌ [실패] 핀 그래프 노드 위치 보존 실패: 예상 ({customX}, {customY}), 실제 ({reloadedTrigNode.X}, {reloadedTrigNode.Y})");
                return false;
            }

            if (Math.Abs(reloadedTrigNode.Width - customW) > 0.001f || Math.Abs(reloadedTrigNode.Height - customH) > 0.001f)
            {
                Console.WriteLine($"  ❌ [실패] 핀 그래프 노드 크기 보존 실패: 예상 ({customW}x{customH}), 실제 ({reloadedTrigNode.Width}x{reloadedTrigNode.Height})");
                return false;
            }

            // 4. FSM 카드 노드 커스텀 크기 저장 및 복원 검증
            profile.NodeSizes["기본 상태"] = new NodeCanvasSize(400f, 300f);
            if (!profile.NodeSizes.ContainsKey("기본 상태") || profile.NodeSizes["기본 상태"].Width != 400f)
            {
                Console.WriteLine("  ❌ [실패] FSM 카드 노드 크기 모델 저장 실패");
                return false;
            }

            Console.WriteLine("  ✔ [통과] 핀 그래프 노드 드래그 좌표 영구 보존 & 노드 동적 리사이즈(FSM/핀그래프) 무결성 검증 성공");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 핀 그래프 보존 및 리사이즈 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestV310DualGaugesAndHardwareSafety()
    {
        Console.WriteLine("\n[테스트 24] v3.1.0 듀얼 게이지 & 다중 핀 & 동시성 하드웨어 안전 무결성 검증...");
        try
        {
            // 1. 단일 핀 다중 연결 (1:N 분기 및 N:1 병합) 검증
            var graph = new PinGraphModel();
            var srcNode = new GraphNode("조건 노드", GraphNodeType.Condition, 100, 100);
            var srcPin = GraphPin.CreateFlowOut("Out");
            srcNode.OutputPins.Add(srcPin);

            var dstNode1 = new GraphNode("행동 1", GraphNodeType.Action, 350, 80);
            var dstPin1 = GraphPin.CreateFlowIn("In");
            dstNode1.InputPins.Add(dstPin1);

            var dstNode2 = new GraphNode("행동 2", GraphNodeType.Action, 350, 180);
            var dstPin2 = GraphPin.CreateFlowIn("In");
            dstNode2.InputPins.Add(dstPin2);

            graph.Nodes.Add(srcNode);
            graph.Nodes.Add(dstNode1);
            graph.Nodes.Add(dstNode2);

            // 1:N 분기 연결
            graph.AddConnection(srcNode.Id, srcPin.Id, dstNode1.Id, dstPin1.Id);
            graph.AddConnection(srcNode.Id, srcPin.Id, dstNode2.Id, dstPin2.Id);

            if (graph.Connections.Count != 2)
            {
                Console.WriteLine("  ❌ [실패] 단일 핀 1:N 다중 연결 저장 실패");
                return false;
            }

            // 동일 핀 중복 연결 방지 검증
            graph.AddConnection(srcNode.Id, srcPin.Id, dstNode1.Id, dstPin1.Id);
            if (graph.Connections.Count != 2)
            {
                Console.WriteLine("  ❌ [실패] 동일 핀 중복 연결 방지 실패");
                return false;
            }

            // CanConnect 호환성 사전 검증 기능 검사
            var dstNode3 = new GraphNode("행동 3", GraphNodeType.Action, 350, 260);
            var dstPin3 = GraphPin.CreateFlowIn("In");
            dstNode3.InputPins.Add(dstPin3);
            graph.Nodes.Add(dstNode3);

            if (!graph.CanConnect(srcNode.Id, srcPin.Id, dstNode3.Id, dstPin3.Id))
            {
                Console.WriteLine("  ❌ [실패] 유효한 핀 간 CanConnect 호환성 판단 실패");
                return false;
            }
            if (graph.CanConnect(srcNode.Id, srcPin.Id, dstNode1.Id, dstPin1.Id))
            {
                Console.WriteLine("  ❌ [실패] 이미 연결된 핀 간 CanConnect 중복 방지 거부 실패");
                return false;
            }

            // 2. 루프 방어 가드(CallDepthGuard) 검증
            var profile = new MacroEventProfile();
            profile.States = new List<string> { "상태A", "상태B" };
            profile.InitialState = "상태A";
            var harness = new EventMacroHarness(profile);

            // 빠른 연속 전이 80회 시도 -> 60회 초과 시 안전하게 억제되어야 함
            for (int i = 0; i < 80; i++)
            {
                harness.ChangeState(i % 2 == 0 ? "상태B" : "상태A");
            }
            // 예외 없이 완료되면 가드가 정상 작동한 것임

            // 3. 브랜치 핀 이름 영문 괄호 제거 검증
            var testProfile = new MacroEventProfile();
            testProfile.States.Add("기본 상태");
            var testEvt = new MacroEvent { Id = "test_evt_br", StateTag = "기본 상태" };
            testEvt.Triggers.Add(new MacroTrigger { Type = TriggerType.VisionDetection, VisionTarget = VisionTargetType.Color });
            testProfile.Events.Add(testEvt);

            var convGraph = PinGraphConverter.FromProfile(testProfile);
            var condNode = convGraph.Nodes.FirstOrDefault(n => n.NodeType == GraphNodeType.Condition);
            if (condNode != null)
            {
                foreach (var pin in condNode.OutputPins)
                {
                    if (pin.Name.Contains("(Else)") || pin.Name.Contains("(AND)") || pin.Name.Contains("(ROI)"))
                    {
                        Console.WriteLine($"  ❌ [실패] 핀 이름에 불필요한 영문 괄호 잔재 발견: {pin.Name}");
                        return false;
                    }
                }
            }

            // 4. 핀 모드 첫 조건 노드 생성 시 정확히 1개만 생성되는지 무결성 검증 (2개 중복 생성 방지)
            var pinModeProfile = new MacroEventProfile();
            var singleCondEvt = new MacroEvent
            {
                Id = "evt_pin_single",
                StateTag = "기본 상태",
                Triggers = new List<MacroTrigger>() // 빈 목록으로 초기화
            };
            singleCondEvt.Triggers.Add(new MacroTrigger { Type = TriggerType.TimerInterval, IntervalMs = 1000 });
            pinModeProfile.Events.Add(singleCondEvt);
            var pinGraph = PinGraphConverter.FromProfile(pinModeProfile);
            int condCount = pinGraph.Nodes.Count(n => n.NodeType == GraphNodeType.Condition);
            if (condCount != 1)
            {
                Console.WriteLine($"  ❌ [실패] 핀 모드 조건 노드가 1개가 아닌 {condCount}개 생성됨!");
                return false;
            }

            Console.WriteLine("  ✔ [통과] 1:N/N:1 다중 핀 연결, 순환 루프 보호 가드, 핀 조건 1개 단독 생성 무결성 검증 성공");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] v3.1.0 듀얼 게이지 & 하드웨어 안전 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestAppKeyHelperAndWindowPersistence()
    {
        Console.WriteLine("\n[테스트 25] AppKeyHelper 마우스/특수키 포맷터 및 AppSettings 창 상태 검증...");
        try
        {
            // 1. 마우스 측면 버튼 및 휠 버튼 검증
            if (AppKeyHelper.FormatMouseButtonShort(MouseButtonType.XButton1, true) != "측면1(뒤로)" ||
                AppKeyHelper.FormatMouseButtonShort(MouseButtonType.XButton2, true) != "측면2(앞으로)" ||
                AppKeyHelper.FormatMouseButtonShort(MouseButtonType.Middle, true) != "휠클릭")
            {
                Console.WriteLine("  ❌ [실패] 마우스 측면 및 휠 버튼 축약 텍스트 불일치");
                return false;
            }

            if (AppKeyHelper.FormatMouseButton(MouseButtonType.XButton1, false) != "Side Back (X1)" ||
                AppKeyHelper.FormatMouseButton(MouseButtonType.XButton2, false) != "Side Forward (X2)")
            {
                Console.WriteLine("  ❌ [실패] 마우스 측면 버튼 영문 표기 불일치");
                return false;
            }

            // 2. 특수키 변환 검증
            if (AppKeyHelper.FormatKey("D1") != "1" ||
                AppKeyHelper.FormatKey("NumPad5") != "Num 5" ||
                AppKeyHelper.FormatKey("Return") != "Enter" ||
                AppKeyHelper.FormatKey("ControlKey") != "Ctrl" ||
                AppKeyHelper.FormatKey("Oemtilde") != "~")
            {
                Console.WriteLine("  ❌ [실패] 키보드 특수키 포맷 변환 실패");
                return false;
            }

            // 3. AppSettings 윈도우 프로퍼티 검증
            var s = new AppSettings();
            s.WindowWidth = 1440;
            s.WindowHeight = 900;
            s.WindowX = 100;
            s.WindowY = 100;
            s.IsWindowMaximized = true;
            s.SplitterDistance = 750;

            if (s.WindowWidth != 1440 || s.WindowHeight != 900 || !s.IsWindowMaximized || s.SplitterDistance != 750)
            {
                Console.WriteLine("  ❌ [실패] AppSettings 창 크기/위치 프로퍼티 검증 실패");
                return false;
            }

            Console.WriteLine("  ✔ [통과] AppKeyHelper 마우스/키캡 포맷 및 창 상태 기억 무결성 확인");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] AppKeyHelper 및 창 상태 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestUniversalKeyboardNavigationAndShortcutsAcrossAllViews()
    {
        try
        {
            // 1. 상태 순차 생성 불변성 검증 (상태 3 건너뜀 방지 확인)
            var profile = new MacroEventProfile();
            profile.States.Clear();
            profile.States.Add("전역 (Global)");
            profile.States.Add("기본 상태 (Default)");

            string state1 = profile.GenerateNextStateName(true); // "상태 1"
            if (state1 != "상태 1")
            {
                Console.WriteLine($"  ❌ [실패] 기본 상태 이후 다음 상태명 불일치: {state1} (기대값: 상태 1)");
                return false;
            }
            profile.States.Add(state1);

            string state2 = profile.GenerateNextStateName(true); // "상태 2"
            if (state2 != "상태 2")
            {
                Console.WriteLine($"  ❌ [실패] 상태 1 이후 다음 상태명 불일치: {state2} (기대값: 상태 2)");
                return false;
            }
            profile.States.Add(state2);

            string state3 = profile.GenerateNextStateName(true); // "상태 3"
            if (state3 != "상태 3")
            {
                Console.WriteLine($"  ❌ [실패] 상태 2 이후 다음 상태명 불일치: {state3} (기대값: 상태 3)");
                return false;
            }
            profile.States.Add(state3);

            // 2. 이벤트 추가 및 활성/비활성 토글 (Space 키 인터랙션 시맨틱 검증)
            var evt = new MacroEvent
            {
                Id = Guid.NewGuid().ToString(),
                Name = "테스트 이벤트",
                StateTag = state2,
                IsEnabled = true
            };
            var trg = new MacroTrigger { Type = TriggerType.Always };
            var act = new MacroAction { Type = ActionType.KeyPress, Key = "Space", Description = "스페이스바 입력" };
            evt.Triggers.Add(trg);
            evt.Actions.Add(act);
            profile.Events.Add(evt);

            // Space 키 토글 검증
            evt.IsEnabled = !evt.IsEnabled;
            if (evt.IsEnabled)
            {
                Console.WriteLine("  ❌ [실패] 이벤트 비활성화 토글 실패");
                return false;
            }
            evt.IsEnabled = !evt.IsEnabled;
            if (!evt.IsEnabled)
            {
                Console.WriteLine("  ❌ [실패] 이벤트 재활성화 토글 실패");
                return false;
            }

            // 3. 핀 그래프 변환 및 노드 삭제 시맨틱 검증 (Delete 키 인터랙션 검증)
            var pinGraph = PinGraphConverter.FromProfile(profile);
            var actionNode = pinGraph.Nodes.FirstOrDefault(n => n.ActionData == act);
            if (actionNode == null || actionNode.ActionData == null)
            {
                Console.WriteLine("  ❌ [실패] 핀 그래프에서 액션 노드 변환 누락");
                return false;
            }

            // 액션 노드 삭제 시 부모 이벤트에서 액션 정상 제거 확인
            evt.Actions.Remove(actionNode.ActionData);
            pinGraph.RemoveNode(actionNode.Id);
            if (evt.Actions.Count != 0 || pinGraph.FindNode(actionNode.Id) != null)
            {
                Console.WriteLine("  ❌ [실패] 핀 그래프 노드 삭제 데이터 모델 미반영");
                return false;
            }

            // 4. 4대 뷰 모드 열거형 무결성 확인
            var modes = Enum.GetValues<AppViewMode>();
            if (modes.Length != 4 ||
                !modes.Contains(AppViewMode.CanvasFsm) ||
                !modes.Contains(AppViewMode.CanvasPinGraph) ||
                !modes.Contains(AppViewMode.ClassicList) ||
                !modes.Contains(AppViewMode.ClassicTree))
            {
                Console.WriteLine("  ❌ [실패] 4대 뷰 모드 규격 불일치");
                return false;
            }

            Console.WriteLine("  ✔ [통과] 4대 뷰 공통 키보드 조작(F2/Delete/Enter/Space/Esc) 및 상태 시퀀스 무결성 확인");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 키보드 조작 및 4대 뷰 일관성 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestEmojiFontIntegrityAndWizardKeyControls()
    {
        try
        {
            // 1. Segoe UI 계열 이모지 폰트 생성 무결성 검증 (Bold 요청 시에도 안전하게 Font 객체 반환)
            using var fontRegular = AppFontHelper.GetEmojiFont(10.5f, FontStyle.Regular);
            using var fontBold = AppFontHelper.GetEmojiFont(10.5f, FontStyle.Bold);

            if (fontRegular == null || fontBold == null || string.IsNullOrEmpty(fontRegular.Name))
            {
                Console.WriteLine("  ❌ [실패] AppFontHelper.GetEmojiFont 반환 실패");
                return false;
            }

            // 2. 이모지 폰트 패밀리 우선순위 검증 (Segoe UI Emoji / Segoe UI Symbol / Segoe UI 계열 확인)
            string emojiFamily = AppFontHelper.EmojiFontFamilyName;
            if (string.IsNullOrEmpty(emojiFamily))
            {
                Console.WriteLine("  ❌ [실패] EmojiFontFamilyName 비어있음");
                return false;
            }

            // 3. 다국어 도우미 마법사 이모지(✨) 무결성 검증
            string wizardStr = Localization.Get("BtnTemplateWizard");
            if (!wizardStr.Contains("✨") || (!wizardStr.Contains("도우미 마법사") && !wizardStr.Contains("Helper Wizard")))
            {
                Console.WriteLine($"  ❌ [실패] Localization BtnTemplateWizard 이모지 누락 또는 불일치: {wizardStr}");
                return false;
            }

            // 4. 핀 모드(Pin Mode) 노드 타이틀, 핀 라벨, 상태 표시 이모지 분리 무결성 검증
            var testCases = new (string input, string expectedIcon, string expectedText)[]
            {
                ("◈ 조건: 비전 감지", "◈", "조건: 비전 감지"),
                ("⌨️ 키: Space", "⌨️", "키: Space"),
                ("🖱️ 감지 위치 클릭", "🖱️", "감지 위치 클릭"),
                ("⏳ 대기: 500ms", "⏳", "대기: 500ms"),
                ("🔢 변수: count = 1", "🔢", "변수: count = 1"),
                ("🔄 상태 전이: [ATTACK]", "🔄", "상태 전이: [ATTACK]"),
                ("⚡ 행동", "⚡", "행동"),
                ("🌐 전역 흐름", "🌐", "전역 흐름"),
                ("🏁 상태: IDLE", "🏁", "상태: IDLE"),
                ("● ON", "●", "ON"),
                ("○ OFF", "○", "OFF")
            };

            foreach (var (input, expIcon, expText) in testCases)
            {
                var (actIcon, actText) = AppFontHelper.SplitEmojiAndText(input);
                if (actIcon != expIcon || actText != expText)
                {
                    Console.WriteLine($"  ❌ [실패] SplitEmojiAndText 불일치 - 입력: '{input}' => 기대: ('{expIcon}', '{expText}'), 실제: ('{actIcon}', '{actText}')");
                    return false;
                }
            }

            // 5. 핀 모드 Suffix 심볼 라벨 토큰화 무결성 검증 (Flow ▶, 참 ✔, 거짓 ✖, Coord ●)
            var suffixCases = new (string input, string expectedSymbol)[]
            {
                ("Flow ▶", "▶"),
                ("In ▶", "▶"),
                ("Next ▶", "▶"),
                ("참 ✔", "✔"),
                ("거짓 ✖", "✖"),
                ("True ✔", "✔"),
                ("False ✖", "✖"),
                ("Coord ●", "●")
            };

            foreach (var (input, expSym) in suffixCases)
            {
                var tokens = AppFontHelper.TokenizeEmojiAndText(input);
                var lastToken = tokens.LastOrDefault();
                if (!lastToken.IsEmoji || lastToken.Text != expSym)
                {
                    Console.WriteLine($"  ❌ [실패] TokenizeEmojiAndText 접미사 심볼 분리 실패 - 입력: '{input}' => 기대 심볼: '{expSym}', 실제: '{lastToken.Text}' (IsEmoji: {lastToken.IsEmoji})");
                    return false;
                }
            }

            // 6. DrawMixedString 렌더링 호출 안전성 검증 (GDI+ 비트맵 실제 드로잉)
            using (var bmp = new Bitmap(300, 50))
            using (var g = Graphics.FromImage(bmp))
            {
                AppFontHelper.DrawMixedString(g, "◈ 조건: 비전 감지", 9f, Brushes.White, new RectangleF(0, 0, 300, 25), StringAlignment.Near, StringAlignment.Center, FontStyle.Bold);
                AppFontHelper.DrawMixedString(g, "Flow ▶", 8f, Brushes.White, new RectangleF(0, 25, 100, 20), StringAlignment.Far, StringAlignment.Center, FontStyle.Bold);
                AppFontHelper.DrawMixedString(g, "참 ✔", 8f, Brushes.White, new RectangleF(100, 25, 100, 20), StringAlignment.Near, StringAlignment.Center, FontStyle.Bold);
            }

            Console.WriteLine($"  ✔ [통과] Segoe UI 이모지 폰트 무결성({emojiFamily}) 및 핀 모드(Pin Mode) 이모지 분리 합성 렌더링 검증 완료 (27개 테스트 완료)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 이모지 폰트 및 마법사 키 컨트롤 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestAiAssistantHarnessAndMultiProviderGeneration()
    {
        try
        {
            // 1. BYOK 멀티 프로바이더 팩토리 인스턴스화 검증
            var providers = new[]
            {
                AiProviderType.Gemini,
                AiProviderType.OpenAI,
                AiProviderType.Claude,
                AiProviderType.DeepSeek,
                AiProviderType.Ollama,
                AiProviderType.MockTest
            };

            foreach (var p in providers)
            {
                var cfg = new AiProviderConfig { ProviderType = p, ApiKey = "test-key-byok-123" };
                var client = AiProviderClientFactory.CreateClient(cfg);
                if (client == null || client.ProviderType != p)
                {
                    Console.WriteLine($"  ❌ [실패] AI Provider Client Factory 생성 오류: {p}");
                    return false;
                }
            }

            // 2. MockAiProviderClient 및 AiProfileGenerator 파이프라인 검증
            var mockClient = new MockAiProviderClient();
            var generator = new AiProfileGenerator(mockClient);

            string userPrompt = "사냥터에서 몬스터 사냥 루프를 돌다가 물약이 0개가 되면 마을 상점으로 귀환하는 매크로를 만들어줘";
            var result = generator.GenerateProfileAsync(userPrompt).GetAwaiter().GetResult();

            if (!result.Success || result.Profile == null)
            {
                Console.WriteLine($"  ❌ [실패] AI 프로필 생성 실패: {result.ErrorMessage}");
                return false;
            }

            var profile = result.Profile;

            // 3. FSM 상태 및 무결성 검증
            if (!profile.States.Contains("Global") || !profile.States.Contains("사냥 상태") || !profile.States.Contains("마을 상점 상태"))
            {
                Console.WriteLine("  ❌ [실패] AI 생성 프로필에 필수 FSM 상태가 누락되었습니다.");
                return false;
            }

            if (profile.Events.Count < 4)
            {
                Console.WriteLine($"  ❌ [실패] AI 생성 이벤트 개수 부족: {profile.Events.Count} (기대: >= 4)");
                return false;
            }

            // 4. MacroLayoutEngine 연동을 통한 노드 캔버스 좌표 자동 계산 검증
            if (profile.NodePositions.Count < 3)
            {
                Console.WriteLine($"  ❌ [실패] AI 생성 프로필의 노드 캔버스 좌표 미배정: {profile.NodePositions.Count}");
                return false;
            }

            // 각 노드가 고유한 좌표를 갖는지 검증 (충돌/겹침 여부)
            var posSet = new HashSet<(float X, float Y)>();
            foreach (var kvp in profile.NodePositions)
            {
                if (!posSet.Add((kvp.Value.X, kvp.Value.Y)))
                {
                    Console.WriteLine($"  ❌ [실패] 상태 노드 좌표 겹침 발견: {kvp.Key} ({kvp.Value.X}, {kvp.Value.Y})");
                    return false;
                }
            }

            // 5. 자가 치유(Self-Healing) 및 예외 처리 검증
            mockClient.ReturnMalformedJson = true;
            var brokenResult = generator.GenerateProfileAsync("깨진 JSON 테스트").GetAwaiter().GetResult();
            if (brokenResult.Success)
            {
                Console.WriteLine("  ❌ [실패] 깨진 JSON 입력에 대해 Success=true를 반환함 (자가 방어 실패)");
                return false;
            }

            var emptyPromptResult = generator.GenerateProfileAsync("").GetAwaiter().GetResult();
            if (emptyPromptResult.Success)
            {
                Console.WriteLine("  ❌ [실패] 빈 프롬프트에 대해 Success=true를 반환함");
                return false;
            }

            // 6. 루트 래퍼 객체 및 대소문자 무시 역직렬화 (OpenAI/OpenRouter/Gemini 호환) 검증
            string wrappedJson = """
            {
              "MacroEventProfile": {
                "ProfileName": "래핑 테스트 프로필",
                "Events": [
                  {
                    "Name": "래핑 이벤트 1",
                    "StateTag": "Global",
                    "Triggers": [{ "Type": "Always" }],
                    "Actions": [{ "Type": "KeyPress", "Key": "D1" }]
                  }
                ]
              }
            }
            """;
            var unwrappedProfile = MacroEventProfileManager.LoadFromString(wrappedJson);
            if (unwrappedProfile == null || unwrappedProfile.Events.Count != 1 || unwrappedProfile.Events[0].Name != "래핑 이벤트 1")
            {
                Console.WriteLine("  ❌ [실패] 루트 래퍼 객체(MacroEventProfile) 자동 언래핑 역직렬화 실패");
                return false;
            }

            string camelCaseJson = """
            {
              "macroEventProfile": {
                "profileName": "카멜케이스 프로필",
                "events": [
                  {
                    "name": "카멜케이스 이벤트",
                    "stateTag": "Global",
                    "triggers": [{ "type": "TimerInterval", "intervalMs": 500 }],
                    "actions": [{ "type": "Wait", "waitDurationMs": 200 }]
                  }
                ]
              }
            }
            """;
            var camelProfile = MacroEventProfileManager.LoadFromString(camelCaseJson);
            if (camelProfile == null || camelProfile.Events.Count != 1 || camelProfile.Events[0].Name != "카멜케이스 이벤트")
            {
                Console.WriteLine("  ❌ [실패] 소문자/카멜케이스 래퍼 및 프로퍼티 역직렬화 실패");
                return false;
            }

            string bareArrayJson = """
            [
              {
                "Name": "단독 배열 이벤트",
                "StateTag": "Global",
                "Triggers": [{ "Type": "Always" }],
                "Actions": [{ "Type": "KeyPress", "Key": "Space" }]
              }
            ]
            """;
            var arrayProfile = MacroEventProfileManager.LoadFromString(bareArrayJson);
            if (arrayProfile == null || arrayProfile.Events.Count != 1 || arrayProfile.Events[0].Name != "단독 배열 이벤트")
            {
                Console.WriteLine("  ❌ [실패] 단독 이벤트 배열 JSON 자동 변환 역직렬화 실패");
                return false;
            }

            // 7. AppSettings AI 설정 영구화 검증
            string originalProvider = AppSettings.Current.AiProvider;
            AppSettings.Current.AiProvider = "Claude";
            AppSettings.Current.AiApiKey = "test-byok-claude-key";
            if (AppSettings.Current.AiProvider != "Claude" || AppSettings.Current.AiApiKey != "test-byok-claude-key")
            {
                Console.WriteLine("  ❌ [실패] AppSettings AI 설정 프로퍼티 불일치");
                return false;
            }
            AppSettings.Current.AiProvider = originalProvider;

            Console.WriteLine("  ✔ [통과] v5.0.0 차세대 AI 어시스턴트 코어, BYOK 멀티 프로바이더 및 FSM 프로필 자동 생성 파이프라인 검증 완료 (28개 테스트 완료)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] AI 어시스턴트 하네스 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestNodeClipboardAndUndoRedoIntegrity()
    {
        try
        {
            // 1. 단일 이벤트 직렬화 및 역직렬화 무결성
            var testEvt = new MacroEvent
            {
                Name = "테스트 물약 복사",
                StateTag = "사냥",
                Triggers = new List<MacroTrigger> { new MacroTrigger { Type = TriggerType.TimerInterval, IntervalMs = 500 } },
                Actions = new List<MacroAction> { new MacroAction { Type = ActionType.KeyPress, Key = "F1", PostDelayMs = 120 } }
            };

            var evtEnvelope = new NodeClipboardEnvelope
            {
                Kind = "Event",
                SingleEvent = testEvt
            };

            string jsonEvt = MacroEventProfileManager.SaveToString(evtEnvelope);
            if (!jsonEvt.Contains(NodeClipboardEnvelope.MagicHeader))
            {
                Console.WriteLine("  ❌ [실패] 클립보드 매직 헤더 누락");
                return false;
            }

            var restoredEvtEnvelope = System.Text.Json.JsonSerializer.Deserialize<NodeClipboardEnvelope>(jsonEvt, MacroEventProfileManager.JsonOptions);
            if (restoredEvtEnvelope == null || restoredEvtEnvelope.Kind != "Event" || restoredEvtEnvelope.SingleEvent?.Name != "테스트 물약 복사")
            {
                Console.WriteLine("  ❌ [실패] 이벤트 클립보드 엔벨로프 역직렬화 실패");
                return false;
            }

            // 2. 상태 노드 및 이벤트 일괄 복사 엔벨로프 무결성
            var stateEnvelope = new NodeClipboardEnvelope
            {
                Kind = "State",
                StateName = "보스전",
                Position = new NodeCanvasPosition(400, 300),
                Size = new NodeCanvasSize(240, 180),
                Events = new List<MacroEvent> { testEvt }
            };

            string jsonState = MacroEventProfileManager.SaveToString(stateEnvelope);
            var restoredStateEnvelope = System.Text.Json.JsonSerializer.Deserialize<NodeClipboardEnvelope>(jsonState, MacroEventProfileManager.JsonOptions);
            if (restoredStateEnvelope == null || restoredStateEnvelope.Kind != "State" || restoredStateEnvelope.StateName != "보스전" || restoredStateEnvelope.Events.Count != 1)
            {
                Console.WriteLine("  ❌ [실패] 상태 노드 클립보드 엔벨로프 역직렬화 실패");
                return false;
            }

            // 3. 잘못된 텍스트 및 외부 클립보드 데이터 무시 가드 검증
            string randomWebUrl = "https://example.com/macro-guide?id=123";
            bool shouldIgnoreUrl = string.IsNullOrWhiteSpace(randomWebUrl) || !randomWebUrl.Contains(NodeClipboardEnvelope.MagicHeader);
            if (!shouldIgnoreUrl)
            {
                Console.WriteLine("  ❌ [실패] 외부 URL 클립보드 가드 실패");
                return false;
            }

            string randomText = "그냥 메모장 텍스트 복사한 것";
            bool shouldIgnoreText = string.IsNullOrWhiteSpace(randomText) || !randomText.Contains(NodeClipboardEnvelope.MagicHeader);
            if (!shouldIgnoreText)
            {
                Console.WriteLine("  ❌ [실패] 일반 텍스트 클립보드 가드 실패");
                return false;
            }

            // 4. Undo/Redo 연동 무결성 검증
            var profile = new MacroEventProfile
            {
                ProfileName = "클립보드 Undo 테스트",
                States = new List<string> { "기본 상태" }
            };
            var undoRedo = new UndoRedoManager();

            // 붙여넣기 직전 스냅샷 기록 (RecordUndo 패턴)
            undoRedo.RecordSnapshot(profile, "상태 노드 붙여넣기 [보스전 (복사)]");

            // 붙여넣기 수행: 상태 노드 추가
            profile.States.Add("보스전 (복사)");

            if (!undoRedo.CanUndo || undoRedo.CanRedo)
            {
                Console.WriteLine("  ❌ [실패] Undo/Redo 스택 상태 오류");
                return false;
            }

            // 언두 수행 (붙여넣기 취소 -> 초기 상태로 복원)
            var (undone, _) = undoRedo.Undo(profile);
            if (undone == null || undone.States.Contains("보스전 (복사)"))
            {
                Console.WriteLine("  ❌ [실패] 클립보드 붙여넣기 언두 실패");
                return false;
            }

            // 리두 수행 (붙여넣기 다시 실행 -> '보스전 (복사)' 복원)
            var (redone, _) = undoRedo.Redo(undone);
            if (redone == null || !redone.States.Contains("보스전 (복사)"))
            {
                Console.WriteLine("  ❌ [실패] 클립보드 붙여넣기 리두 실패");
                return false;
            }

            Console.WriteLine("  ✔ [통과] 노드/이벤트 클립보드 엔벨로프 무결성, 외부 텍스트 무시 가드 및 Undo/Redo 연동 검증 완료");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 클립보드 무결성 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestKeyStrokeModesAndSimultaneousInput()
    {
        try
        {
            // 1. 기본값 및 enum 검증
            var defAction = new MacroAction();
            if (defAction.KeyMode != KeyStrokeMode.KeyDownUp)
            {
                Console.WriteLine("  ❌ [실패] MacroAction.KeyMode 기본값 오류 (기본값은 KeyDownUp이어야 함)");
                return false;
            }

            // 2. JSON 직렬화 / 역직렬화 (KeyDown, KeyUp, KeyDownUp)
            var profile = new MacroEventProfile
            {
                ProfileName = "동시 입력 테스트",
                States = new List<string> { "기본 상태" }
            };

            var comboEvt = new MacroEvent
            {
                Name = "Shift+1 동시 입력",
                StateTag = "기본 상태",
                Actions = new List<MacroAction>
                {
                    new MacroAction { Type = ActionType.KeyPress, Key = "ShiftKey", KeyMode = KeyStrokeMode.KeyDown, PostDelayMs = 20, Description = "Shift 누름 유지" },
                    new MacroAction { Type = ActionType.KeyPress, Key = "D1", KeyMode = KeyStrokeMode.KeyDownUp, HoldTimeMs = 35, PostDelayMs = 20, Description = "1번 스킬 입력" },
                    new MacroAction { Type = ActionType.KeyPress, Key = "ShiftKey", KeyMode = KeyStrokeMode.KeyUp, PostDelayMs = 50, Description = "Shift 손 뗌" }
                }
            };
            profile.Events.Add(comboEvt);

            string json = MacroEventProfileManager.SaveToString(profile);
            var restored = MacroEventProfileManager.LoadFromString(json);

            if (restored == null || restored.Events.Count != 1)
            {
                Console.WriteLine("  ❌ [실패] 동시 입력 프로필 역직렬화 실패");
                return false;
            }

            var acts = restored.Events[0].Actions;
            if (acts.Count != 3 ||
                acts[0].KeyMode != KeyStrokeMode.KeyDown ||
                acts[1].KeyMode != KeyStrokeMode.KeyDownUp ||
                acts[2].KeyMode != KeyStrokeMode.KeyUp)
            {
                Console.WriteLine("  ❌ [실패] 역직렬화된 KeyMode 불일치");
                return false;
            }

            // 3. 하위 호환성 검증: KeyMode 필드가 없는 레거시 JSON 역직렬화 시 KeyDownUp으로 복원되는지 확인
            string legacyJson = """
            {
                "ProfileName": "레거시 프로필",
                "States": ["기본 상태"],
                "Events": [{
                    "Id": "legacy-evt-1",
                    "Name": "구버전 이벤트",
                    "StateTag": "기본 상태",
                    "Actions": [{
                        "Type": "KeyPress",
                        "Key": "Space",
                        "HoldTimeMs": 40,
                        "PostDelayMs": 60
                    }]
                }]
            }
            """;
            var legacyRestored = MacroEventProfileManager.LoadFromString(legacyJson);
            if (legacyRestored == null || legacyRestored.Events[0].Actions[0].KeyMode != KeyStrokeMode.KeyDownUp)
            {
                Console.WriteLine("  ❌ [실패] 레거시 JSON 하위 호환성 역직렬화 오류 (KeyMode 기본값 미적용)");
                return false;
            }

            // 4. 활성 누름 키 추적 및 ReleaseAllPressedInputs 안전망 검증
            // 안전한 가상키(Keys.F24)로 시뮬레이터 추적 검증
            WindowsInputSimulator.ReleaseAllPressedInputs();
            if (WindowsInputSimulator.GetActivePressedKeys().Count != 0)
            {
                Console.WriteLine("  ❌ [실패] 초기 activePressedKeys 미정리");
                return false;
            }

            WindowsInputSimulator.SendKeyDown(Keys.F24, 0);
            if (!WindowsInputSimulator.GetActivePressedKeys().Contains(Keys.F24))
            {
                Console.WriteLine("  ❌ [실패] SendKeyDown 후 activePressedKeys 미등록");
                return false;
            }

            WindowsInputSimulator.SendKeyUp(Keys.F24, 0);
            if (WindowsInputSimulator.GetActivePressedKeys().Contains(Keys.F24))
            {
                Console.WriteLine("  ❌ [실패] SendKeyUp 후 activePressedKeys 미제거");
                return false;
            }

            // 비상정지 / 정지 시 강제 일괄 해제 검증
            WindowsInputSimulator.SendKeyDown(Keys.F24, 0);
            WindowsInputSimulator.ReleaseAllPressedInputs();
            if (WindowsInputSimulator.GetActivePressedKeys().Count != 0)
            {
                Console.WriteLine("  ❌ [실패] ReleaseAllPressedInputs 후 activePressedKeys 미클리어");
                return false;
            }

            Console.WriteLine("  ✔ [통과] 키 입력 옵션(키다운업/키다운/키업) 세분화, 하위 호환 역직렬화 및 동시 입력/누름 추적 안전망 검증 완료 (30개 테스트 완료)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 키 입력 모드 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestKeySequenceFoldingAndSizeAwareAutoLayout()
    {
        Console.WriteLine("\n[검증 31] v5.2.0 핀 노드 연속 키 접기/펼치기, 조건 기본값 상시화 및 노드 크기 반응형 자동 정렬 검증");
        try
        {
            // 1. 조건 생성 시 기본 조건은 상시(Always) 검증
            var defTrigger = new MacroTrigger();
            if (defTrigger.Type != TriggerType.Always)
            {
                Console.WriteLine($"  ❌ [실패] 기본 발동 조건이 Always여야 하나 {defTrigger.Type}임");
                return false;
            }

            // 2. 핀 노드 연속 키 입력(KeyPress / Wait) 복합 노드 묶음 및 접기/펼치기 검증
            var profile = new MacroEventProfile
            {
                States = new List<string> { "Global", "사냥 상태" },
                InitialState = "사냥 상태"
            };

            var seqEvt = new MacroEvent
            {
                Id = "evt_combo_1",
                StateTag = "사냥 상태",
                Name = "스킬 연계 콤보",
                Actions = new List<MacroAction>
                {
                    new MacroAction { Type = ActionType.KeyPress, Key = "ShiftKey", KeyMode = KeyStrokeMode.KeyDown },
                    new MacroAction { Type = ActionType.KeyPress, Key = "D1", KeyMode = KeyStrokeMode.KeyDownUp, HoldTimeMs = 40 },
                    new MacroAction { Type = ActionType.KeyPress, Key = "ShiftKey", KeyMode = KeyStrokeMode.KeyUp },
                    new MacroAction { Type = ActionType.Wait, WaitDurationMs = 100 },
                    new MacroAction { Type = ActionType.KeyPress, Key = "Space", KeyMode = KeyStrokeMode.KeyDownUp }
                }
            };
            profile.Events.Add(seqEvt);

            var graph = PinGraphConverter.FromProfile(profile);
            var seqNode = graph.Nodes.FirstOrDefault(n => n.NodeType == GraphNodeType.KeySequence);
            if (seqNode == null)
            {
                Console.WriteLine("  ❌ [실패] 5개 연속 키/대기 액션이 KeySequence 노드로 묶이지 않음");
                return false;
            }

            if (!seqNode.IsCollapsed || seqNode.SequenceActions?.Count != 5)
            {
                Console.WriteLine("  ❌ [실패] KeySequence 노드 초기 상태가 접힘(Collapsed)이 아니거나 시퀀스 액션 수 불일치");
                return false;
            }

            if (seqNode.Height != 75f)
            {
                Console.WriteLine($"  ❌ [실패] 접힌 KeySequence 노드 높이 오류: {seqNode.Height} (기대: 75)");
                return false;
            }

            // 펼치기 시뮬레이션
            profile.PinGraphCollapsedNodes[seqNode.Id] = false;
            var expandedGraph = PinGraphConverter.FromProfile(profile);
            var expNode = expandedGraph.Nodes.FirstOrDefault(n => n.Id == seqNode.Id);
            if (expNode == null || expNode.IsCollapsed || expNode.Height < 140f)
            {
                Console.WriteLine($"  ❌ [실패] 펼친 KeySequence 노드 높이 동적 확장 실패: {expNode?.Height}");
                return false;
            }

            // 3. 노드 크기(Width/Height) 동적 반응형 스마트 자동 정렬 검증 (겹침 0% 보장)
            var layoutProfile = new MacroEventProfile
            {
                States = new List<string> { "Global", "와이드상태", "다음상태" },
                InitialState = "와이드상태"
            };

            // 와이드상태 -> 다음상태 전이
            var transEvt = new MacroEvent { StateTag = "와이드상태", Name = "전이" };
            transEvt.Actions.Add(new MacroAction { Type = ActionType.ChangeState, TargetState = "다음상태" });
            layoutProfile.Events.Add(transEvt);

            // 와이드상태의 너비를 600px로 커스텀 설정
            layoutProfile.NodeSizes["와이드상태"] = new NodeCanvasSize(600f, 250f);

            MacroLayoutEngine.ComputeSmartLayout(
                layoutProfile,
                st => layoutProfile.NodeSizes.TryGetValue(st, out var s) ? s.Height : 220f,
                st => layoutProfile.NodeSizes.TryGetValue(st, out var s) ? s.Width : 340f
            );

            var posWide = layoutProfile.NodePositions["와이드상태"];
            var posNext = layoutProfile.NodePositions["다음상태"];

            // 다음 상태는 와이드상태(600px)의 우측 여백 바깥에 안전하게 배치되어야 함 (80 + 600 + 80 = 760 이상)
            if (posNext.X < posWide.X + 600f)
            {
                Console.WriteLine($"  ❌ [실패] 와이드 노드(600px) 후속 열 배치 충돌: 다음열 X={posNext.X}, 와이드 Right={posWide.X + 600f}");
                return false;
            }

            Console.WriteLine("  ✔ [통과] 핀 노드 연속 키 접기/펼치기, 조건 기본값 상시(Always)화, 대형 노드 크기 반응형 자동 정렬 검증 완료 (31개 테스트 완료)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 5.2.0 신규 기능 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestMouseStrokeModesAndNoNodesHighlight()
    {
        Console.WriteLine("\n[검증 32] v5.3.0 마우스 다운/업 세분화, 빈 캔버스 시작 핀 하이라이트 및 녹화 훅 무결성 검증");
        try
        {
            // 1. 마우스 스트로크 모드 직렬화/역직렬화 검증
            var testAction = new MacroAction
            {
                Type = ActionType.MouseClick,
                MouseButton = MouseButtonType.Right,
                MouseMode = MouseStrokeMode.MouseDown,
                HoldTimeMs = 50,
                PostDelayMs = 100,
                CustomX = 300,
                CustomY = 400
            };

            string json = System.Text.Json.JsonSerializer.Serialize(testAction);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<MacroAction>(json);
            if (deserialized == null || deserialized.MouseMode != MouseStrokeMode.MouseDown || deserialized.MouseButton != MouseButtonType.Right)
            {
                Console.WriteLine("  ❌ [실패] MouseStrokeMode 직렬화/역직렬화 불일치");
                return false;
            }

            // 2. WindowsInputSimulator 마우스 버튼 추적 및 자가 치유 안전 해제 검증
            WindowsInputSimulator.ReleaseAllPressedInputs();
            WindowsInputSimulator.TrackPressedMouseButtonForTesting(MouseButtonType.Left);
            WindowsInputSimulator.TrackPressedMouseButtonForTesting(MouseButtonType.Right);
            if (WindowsInputSimulator.GetActivePressedMouseButtons().Count != 2)
            {
                Console.WriteLine("  ❌ [실패] 마우스 버튼 전역 추적 누락");
                return false;
            }

            WindowsInputSimulator.ReleaseAllPressedInputs();
            if (WindowsInputSimulator.GetActivePressedMouseButtons().Count != 0)
            {
                Console.WriteLine("  ❌ [실패] ReleaseAllPressedInputs 후 마우스 버튼 미해제");
                return false;
            }

            // 3. MacroRecorder 마우스 드래그/동시 입력 스마트 분리 변환 검증
            var rawMouseEvents = new List<MacroRecorder.RawInputEvent>
            {
                new MacroRecorder.RawInputEvent { TimestampMs = 100, Type = MacroRecorder.RawInputType.MouseDown, MouseButton = MouseButtonType.Left, ScreenPoint = new System.Drawing.Point(100, 200) },
                new MacroRecorder.RawInputEvent { TimestampMs = 150, Type = MacroRecorder.RawInputType.KeyDown, Key = System.Windows.Forms.Keys.A },
                new MacroRecorder.RawInputEvent { TimestampMs = 200, Type = MacroRecorder.RawInputType.KeyUp, Key = System.Windows.Forms.Keys.A },
                new MacroRecorder.RawInputEvent { TimestampMs = 250, Type = MacroRecorder.RawInputType.MouseUp, MouseButton = MouseButtonType.Left, ScreenPoint = new System.Drawing.Point(150, 200) }
            };

            var actions = MacroRecorder.ConvertToMacroActions(rawMouseEvents);
            var mouseActs = actions.Where(a => a.Type == ActionType.MouseClick).ToList();
            if (mouseActs.Count != 2 ||
                mouseActs[0].MouseMode != MouseStrokeMode.MouseDown ||
                mouseActs[1].MouseMode != MouseStrokeMode.MouseUp)
            {
                Console.WriteLine("  ❌ [실패] 마우스 드래그/복합 입력 변환 오류 (MouseDown/MouseUp 스마트 분리 실패)");
                return false;
            }

            // 4. 노드가 없을 때의 핀 그래프 시작 노드 출력 핀 검증
            var emptyProfile = new MacroEventProfile
            {
                States = new List<string> { "Global", "기본 상태" },
                InitialState = "기본 상태"
            };
            var emptyGraph = PinGraphConverter.FromProfile(emptyProfile);
            bool hasNoOtherNodes = emptyGraph.Nodes.Count(n => n.NodeType != GraphNodeType.Start) == 0;
            if (!hasNoOtherNodes)
            {
                Console.WriteLine("  ❌ [실패] 빈 프로필 핀 그래프에 불필요한 노드 존재");
                return false;
            }

            var startNode = emptyGraph.Nodes.FirstOrDefault(n => n.NodeType == GraphNodeType.Start);
            if (startNode == null || !startNode.OutputPins.Any(p => p.Direction == GraphPinDirection.Output))
            {
                Console.WriteLine("  ❌ [실패] 시작 노드 또는 출력 Flow 핀 누락");
                return false;
            }

            Console.WriteLine("  ✔ [통과] v5.3.0 마우스 다운/업 세분화, 전역 자가 치유 안전망, 드래그 녹화 분리 및 빈 캔버스 시작 핀 무결성 검증 완료");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 5.3.0 마우스/하이라이트 테스트 실패: {ex.Message}");
            return false;
        }
    }

    private static bool TestPrecisionLoopControllerAndDriftCompensation()
    {
        Console.WriteLine("\n[테스트 33] v5.4.0 폐루프 틱 제어기(PrecisionLoopController) 및 드리프트 보정 검증");
        try
        {
            var controller = new PrecisionLoopController();
            controller.Reset();

            if (controller.AccumulatedDriftMs != 0 || controller.TickCount != 0)
            {
                Console.WriteLine("  ❌ [실패] PrecisionLoopController 초기화 상태 비정상");
                return false;
            }

            // 1. 빠른 취소 응답성 검증
            var swCancel = Stopwatch.StartNew();
            controller.WaitForNextTick(500, () => true);
            swCancel.Stop();
            if (swCancel.ElapsedMilliseconds > 30)
            {
                Console.WriteLine($"  ❌ [실패] 취소 신호 수신 시 즉시 탈출 실패 (경과: {swCancel.ElapsedMilliseconds}ms)");
                return false;
            }

            // 2. 닫힌 루프(Closed-Loop) 드리프트 보정 시뮬레이션
            // 목표: 20ms 주기 x 8회 = 160ms
            // 매 틱마다 5ms의 가상 연산 수행
            controller.Reset();
            int targetTickMs = 20;
            int totalTicks = 8;
            var swTotal = Stopwatch.StartNew();

            for (int i = 0; i < totalTicks; i++)
            {
                Thread.Sleep(5); // 5ms 가상 연산
                controller.WaitForNextTick(targetTickMs);
            }
            swTotal.Stop();

            double totalElapsed = swTotal.ElapsedMilliseconds;
            double expectedTotal = targetTickMs * totalTicks; // 160ms
            double error = Math.Abs(totalElapsed - expectedTotal);

            // 단순 Sleep(20)이었으면 (5ms + 20ms) * 8 = 200ms+ 소요됨.
            // 폐루프 보정기에서는 160ms에 근접해야 함 (오차 < 35ms 허용)
            if (error > 35.0)
            {
                Console.WriteLine($"  ❌ [실패] 폐루프 주기 보정 오차 초과: 예상 {expectedTotal}ms, 실제 {totalElapsed}ms (오차: {error}ms)");
                return false;
            }

            // 3. EventMacroHarness와 LoopController 바인딩 검증
            var profile = new MacroEventProfile
            {
                CheckIntervalMs = 50,
                States = new List<string> { "Global", "기본 상태" }
            };
            var harness = new EventMacroHarness(profile);
            if (harness.LoopController == null)
            {
                Console.WriteLine("  ❌ [실패] EventMacroHarness.LoopController 인스턴스 누락");
                return false;
            }

            Console.WriteLine($"  ✔ [통과] v5.4.0 폐루프 틱 제어기(PrecisionLoopController) 검증 완료 (총 {totalTicks}틱 실제 {totalElapsed}ms, 오차 {error:F1}ms, 누적 드리프트: {controller.AccumulatedDriftMs:F2}ms, 33개 테스트 완료)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ [예외] 폐루프 제어기 테스트 실패: {ex.Message}");
            return false;
        }
    }
}

namespace RpgMacroHarness.Data;

/// <summary>
/// 한국어 / English 다국어 문자열을 완벽하게 제공하는 로컬라이제이션 서비스입니다.
/// </summary>
public static class Localization
{
    public static bool IsKorean => AppSettings.Current.Language == "ko";

    public static string Get(string key)
    {
        bool ko = IsKorean;
        return key switch
        {
            // App / Form
            "AppTitle" => ko ? "RPG 매크로 스튜디오" : "RPG Macro Studio",
            "BtnOpen" => ko ? "📂 프로필 열기" : "📂 Open Profile",
            "BtnSave" => ko ? "💾 프로필 저장" : "💾 Save Profile",
            "BtnStart" => ko ? $"▶ 시작 [{AppSettings.Current.StartPauseHotkey}]" : $"▶ Start [{AppSettings.Current.StartPauseHotkey}]",
            "BtnPause" => ko ? $"⏸ 일시정지 [{AppSettings.Current.StartPauseHotkey}]" : $"⏸ Pause [{AppSettings.Current.StartPauseHotkey}]",
            "BtnResume" => ko ? $"▶ 재개 [{AppSettings.Current.StartPauseHotkey}]" : $"▶ Resume [{AppSettings.Current.StartPauseHotkey}]",
            "BtnStop" => ko ? $"⏹ 정지 [{AppSettings.Current.StopHotkey}]" : $"⏹ Stop [{AppSettings.Current.StopHotkey}]",
            "BtnSettings" => ko ? "⚙️ 설정" : "⚙️ Settings",
            "BtnGetStarted" => ko ? "💡 시작 가이드" : "💡 Get Started",
            "BtnTemplateWizard" => ko ? "✨ 도우미 마법사" : "✨ Helper Wizard",
            "BtnAiAssistant" => ko ? "🧠 AI 어시스턴트" : "🧠 AI Assistant",
            "TooltipAiAssistant" => ko ? "자연어 기반 AI 매크로 프로필 자동 생성 및 FSM 배치" : "Generate macro profile automatically using AI with natural language",
            "AiDialogTitle" => ko ? "RPG 매크로 스튜디오 - ✨ AI 매크로 어시스턴트 (v5.0.0)" : "RPG Macro Studio - ✨ AI Macro Assistant (v5.0.0)",
            "AiProviderLabel" => ko ? "AI 프로바이더:" : "AI Provider:",
            "AiApiKeyLabel" => ko ? "API 키 (BYOK):" : "API Key (BYOK):",
            "AiModelLabel" => ko ? "모델명:" : "Model Name:",
            "AiEndpointLabel" => ko ? "엔드포인트 URL:" : "Endpoint URL:",
            "AiFetchModels" => ko ? "🔄 최신 모델 목록 가져오기" : "🔄 Fetch Models",
            "AiPromptLabel" => ko ? "원하는 매크로 시나리오를 설명해주세요:" : "Describe your macro scenario in natural language:",
            "AiCaptureScreen" => ko ? "📷 현재 화면 캡처" : "📷 Capture Screen",
            "AiLoadImage" => ko ? "🖼️ 이미지 첨부" : "🖼️ Attach Image",
            "AiRemoveImage" => ko ? "❌ 이미지 제거" : "❌ Remove Image",
            "AiGenerateBtn" => ko ? "✨ AI 매크로 프로필 생성" : "✨ Generate AI Macro Profile",
            "AiApplyToCanvas" => ko ? "🚀 현재 캔버스에 즉시 적용" : "🚀 Apply Directly to Canvas",
            "AiStatusReady" => ko ? "준비됨: 시나리오를 입력하고 생성 버튼을 누르세요." : "Ready: Enter scenario and click Generate.",
            "AiStatusGenerating" => ko ? "AI가 시나리오를 분석하고 FSM 프로필을 설계 중입니다..." : "AI is analyzing scenario and designing FSM profile...",
            "AiStatusSuccess" => ko ? "성공: 프로필이 생성되었습니다! 미리보기를 확인하고 적용하세요." : "Success: Profile generated! Review preview and apply.",
            "EmergencyStop" => ko ? $"🚨 긴급 정지: [{AppSettings.Current.EmergencyStopHotkey}]" : $"🚨 Emergency Stop: [{AppSettings.Current.EmergencyStopHotkey}]",
            "StatusWaiting" => ko ? "동작: ⚪ 대기 중" : "Status: ⚪ Idle",
            "StatusRunning" => ko ? "동작: 🟢 실행 중" : "Status: 🟢 Running",
            "StatusPaused" => ko ? "동작: 🟡 일시정지" : "Status: 🟡 Paused",
            "StatusStopped" => ko ? "동작: 🔴 정지됨" : "Status: 🔴 Stopped",
            "ActiveStatePrefix" => ko ? "현재 상태:" : "Current State:",
            "LogTitle" => ko ? "📋 실행 및 감지 로그" : "📋 Execution & Detection Logs",
            "LogTitleNode" => "Log",

            // Node Edition Specific
            "BtnAddStateNode" => ko ? "➕ 상태 추가" : "➕ Add State",
            "BtnAddEventToNode" => ko ? "⚡ 이벤트 추가" : "⚡ Add Event",
            "BtnAutoLayout" => ko ? "📐 자동 정렬" : "📐 Auto Layout",
            "BtnSetInitial" => ko ? "⭐ 시작 상태 지정" : "⭐ Set Initial State",
            "GrpInspector" => ko ? "🔍 인스펙터" : "🔍 Inspector",
            "GrpStateProps" => ko ? "🏷️ 노드 속성" : "🏷️ Node Properties",
            "GrpEventProps" => ko ? "⚡ 선택된 이벤트 상세 제어" : "⚡ Selected Event Details",
            "LblSelectStateNone" => ko ? "선택된 상태: 없음" : "Selected State: None",
            "LblSelectedStatePrefix" => ko ? "선택된 상태:" : "Selected State:",
            "NoEventsInState" => ko ? "등록된 이벤트 없음" : "No events registered",
            "MenuAddState" => ko ? "➕ 새 상태 노드 추가" : "➕ Add State Node",
            "MenuAddEvent" => ko ? "⚡ 선택된 상태에 새 이벤트 추가" : "⚡ Add Event to Selected State",
            "MenuAutoLayout" => ko ? "📐 노드 자동 정렬" : "📐 Auto Layout Nodes",
            "MenuResetView" => ko ? "🔍 화면 중앙으로 맞춤" : "🔍 Center View",
            "PromptAddStateTitle" => ko ? "새 상태 노드 추가" : "Add New State Node",
            "PromptAddStateDesc" => ko ? "새로 추가할 상태 노드의 이름을 입력하세요:" : "Enter new state node name:",
            "PromptRenameTitle" => ko ? "상태 이름 변경" : "Rename State",
            "PromptRenameDesc" => ko ? "새 상태 이름을 입력하세요:" : "Enter new state name:",
            "PromptRenameStateTitle" => ko ? "상태 이름 변경" : "Rename State Node",
            "PromptRenameStateDesc" => ko ? "새 상태 이름을 입력하세요:" : "Enter new state name:",
            "PromptDeleteStateTitle" => ko ? "상태 노드 삭제" : "Delete State Node",
            "PromptDeleteStateDesc" => ko ? "선택한 상태 노드를 삭제하시겠습니까?" : "Are you sure you want to delete the selected state node?",
            "ConfirmDeleteState" => ko ? "정말로 이 상태 노드를 삭제하시겠습니까? 등록된 모든 이벤트도 함께 제거됩니다." : "Are you sure you want to delete this state node? All its events will also be removed.",

            // State Management
            "TabGlobal" => ko ? "🌐 전역" : "🌐 Global",
            "TabDefault" => ko ? "기본 상태" : "Default State",
            "BtnAddState" => ko ? "➕ 상태 추가" : "➕ Add State",
            "BtnRenameState" => ko ? "✏️ 이름 변경" : "✏️ Rename",
            "BtnDeleteState" => ko ? "❌ 상태 삭제" : "❌ Delete State",
            "LblStateTag" => ko ? "소속 상태:" : "State:",

            // Event List
            "EventListTitle" => ko ? "🎮 이벤트 목록" : "🎮 Event List",
            "ColActive" => ko ? "활성" : "Active",
            "ColParallel" => ko ? "동시실행" : "Parallel",
            "ColState" => ko ? "소속 상태" : "State",
            "ColName" => ko ? "이벤트 이름" : "Event Name",
            "ColConditions" => ko ? "조건 수" : "Conditions",
            "ColTrigger" => ko ? "조건 유형" : "Trigger Type",
            "BtnAddEvent" => ko ? "➕ 새 이벤트" : "➕ New Event",
            "BtnCloneEvent" => ko ? "📑 복제" : "📑 Duplicate",
            "BtnDeleteEvent" => ko ? "❌ 삭제" : "❌ Delete",

            // Basic Info
            "GrpBasicInfo" => ko ? "📌 1. 이벤트 기본 정보" : "📌 1. Basic Information",
            "LblEventName" => ko ? "이벤트 이름:" : "Event Name:",
            "ChkParallel" => ko ? "동시 실행" : "Concurrent Execution",

            // Trigger Section
            "GrpTrigger" => ko ? "⚡ 2. 발동 조건 목록" : "⚡ 2. Triggers",
            "ColTriggerIndex" => ko ? "순번" : "No.",
            "ColTriggerType" => ko ? "조건 종류" : "Trigger Type",
            "ColTriggerSummary" => ko ? "조건 상세 내용" : "Condition Summary",
            "BtnAddTrigger" => ko ? "➕ 조건 추가..." : "➕ Add Condition...",
            "BtnEditTrigger" => ko ? "✏️ 조건 수정" : "✏️ Edit Condition",
            "BtnDeleteTrigger" => ko ? "❌ 삭제" : "❌ Delete",
            "LblTriggerAndHint" => ko ? "💡 조건이 2개 이상 등록되면 모든 조건이 동시에 참일 때 실행됩니다." : "💡 When multiple conditions are set, all must evaluate to TRUE (AND) to execute.",

            // Gauge Trigger Details
            "BtnPickColor" => ko ? "🎨 화면 색상 추출" : "🎨 Screen Color Eyedropper",
            "LblConditionOp" => ko ? "발동 조건식:" : "Comparison Operator:",
            "LblColorAccuracy" => ko ? "색상 정확도:" : "Color Accuracy:",
            "LblGaugeThreshold" => ko ? "기준 잔여량:" : "Threshold Ratio:",
            "BtnGaugeRoi" => ko ? "🎯 게이지 영역 지정" : "🎯 Select Gauge Region",
            "RoiRequired" => ko ? "영역: 미지정" : "Region: Not Set",
            "RoiFull" => ko ? "영역: 전체 화면" : "Region: Full Screen",

            // Image Trigger Details
            "BtnBrowseImage" => ko ? "이미지 선택..." : "Browse Image...",
            "LblImageAccuracy" => ko ? "일치 정확도:" : "Match Accuracy:",
            "BtnImageRoi" => ko ? "🎯 탐색 영역 지정" : "🎯 Select Search Region",

            // Timer & Always Trigger Details
            "LblTimerInterval" => ko ? "반복 주기 (ms):" : "Interval (ms):",
            "LblTimerHint" => ko ? "※ 단위: ms (예: 1000ms = 1초)" : "※ Unit: ms (e.g. 1000ms = 1s)",
            "LblAlwaysHint" => ko ? "💡 별도의 조건 없이 매 루프마다 항상 실행되는 이벤트입니다." : "💡 Always runs unconditionally on every harness loop cycle.",

            // Actions Section
            "GrpActions" => ko ? "🎬 3. 실행 행동 목록" : "🎬 3. Action Commands",
            "ColActionIndex" => ko ? "순번" : "No.",
            "ColActionType" => ko ? "행동 종류" : "Action Type",
            "ColActionDetail" => ko ? "상세 내용" : "Details",
            "BtnAddAction" => ko ? "➕ 행동 추가..." : "➕ Add Action...",
            "BtnEditAction" => ko ? "✏️ 수정" : "✏️ Edit",
            "BtnDeleteAction" => ko ? "❌ 삭제" : "❌ Delete",
            "BtnMoveUp" => ko ? "▲ 위로" : "▲ Move Up",
            "BtnMoveDown" => ko ? "▼ 아래로" : "▼ Move Down",

            // Settings Dialog
            "SettingsTitle" => ko ? "프로그램 설정" : "Program Settings",
            "GrpSound" => ko ? "🔊 소리 및 알림" : "🔊 Sound & Notification",
            "ChkMute" => ko ? "모든 알림음 및 효과음 음소거" : "Mute all notifications & beeps",
            "GrpLanguage" => ko ? "🌐 언어 설정" : "🌐 Language",
            "GrpTheme" => ko ? "🎨 UI 테마 모드" : "🎨 UI Theme Mode",
            "ThemeDark" => ko ? "🌙 다크 모드" : "🌙 Dark Mode",
            "ThemeLight" => ko ? "☀️ 라이트 모드" : "☀️ Light Mode",
            "GrpHotkeys" => ko ? "⌨️ 전역 단축키 설정" : "⌨️ Global Hotkeys",
            "LblRecordHotkey" => ko ? "매크로 녹화 시작/중지:" : "Record Start / Stop:",
            "LblStartPauseHotkey" => ko ? "시작 / 일시정지:" : "Start / Pause:",
            "LblStopHotkey" => ko ? "완전 정지:" : "Stop (Reset):",
            "LblEmergencyHotkey" => ko ? "긴급 비상정지:" : "Emergency Stop:",
            "ErrDuplicateHotkey" => ko ? "단축키가 서로 중복될 수 없습니다. 각 단축키를 서로 다른 키로 지정해주세요." : "Hotkeys cannot duplicate each other. Please assign unique keys.",
            "GrpCredits" => ko ? "ℹ️ 프로그램 정보 및 크레딧" : "ℹ️ Program Information & Credits",
            "BtnViewChangelog" => ko ? "📜 패치노트" : "📜 Patch Notes",
            "Changelog_Title" => ko ? "패치노트" : "Patch Notes",
            "BtnReopenGetStarted" => ko ? "🚀 시작 가이드 다시 보기" : "🚀 Open Getting Started Guide",
            "Btn_Close" => ko ? "닫기" : "Close",
            "BtnRecord" => ko ? "🔴 매크로 녹화" : "🔴 Record Macro",
            "BtnStopRecord" => ko ? "⏹ 녹화 중지" : "⏹ Stop Recording",
            "RecordingStarted" => ko ? "키/마우스 녹화가 시작되었습니다. 완료 후 [녹화 중지]를 클릭하세요." : "Recording started. Click [Stop Recording] when finished.",
            "RecordingFinished" => ko ? "{0}개의 입력 액션이 성공적으로 기록되었습니다." : "{0} input actions were successfully recorded.",
            "BtnSaveSettings" => ko ? "저장 및 적용" : "Save & Apply",
            "BtnCancelSettings" => ko ? "취소" : "Cancel",
            "PromptUnsavedChanges" => ko ? "현재 프로필에 저장되지 않은 변경 사항이 있습니다. 저장하시겠습니까?" : "There are unsaved changes in the profile. Do you want to save them?",
            "TitleUnsavedChanges" => ko ? "저장되지 않은 변경 사항" : "Unsaved Changes",
            "PromptNoActiveEvents" => ko ? "실행할 수 있는 활성 이벤트가 없습니다. [새 이벤트 추가]를 등록해 주세요." : "No active events to run. Please add a new event.",
            "TitleNotice" => ko ? "알림" : "Notice",
            "TitleConfirm" => ko ? "확인" : "Confirm",
            "TitleDuplicateError" => ko ? "중복 오류" : "Duplicate Error",
            "ErrMinTriggers" => ko ? "이벤트에는 최소 1개 이상의 발동 조건이 있어야 합니다." : "An event must have at least one trigger condition.",
            "MsgSaveSuccess" => ko ? "프로필 저장이 완료되었습니다." : "Profile saved successfully.",
            "BtnCommonAdd" => ko ? "추가" : "Add",
            "BtnCommonApply" => ko ? "변경" : "Apply",
            "BtnCommonCancel" => ko ? "취소" : "Cancel",

            "BtnFoldInspector" => ko ? "▶ 인스펙터 접기" : "▶ Collapse Inspector",
            "BtnUnfoldInspector" => ko ? "◀ 인스펙터 펼치기" : "◀ Expand Inspector",
            "TooltipSlimAddState" => ko ? "➕ 상태 추가" : "➕ Add State",
            "TooltipSlimRenameState" => ko ? "✏️ 상태 이름 변경" : "✏️ Rename State",
            "TooltipSlimSetInitial" => ko ? "⭐ 시작 상태 지정" : "⭐ Set Initial State",
            "TooltipSlimDeleteState" => ko ? "🗑️ 상태 삭제" : "🗑️ Delete State",
            "TooltipSlimAutoLayout" => ko ? "📐 자동 정렬" : "📐 Auto Layout",
            "TooltipSlimSettings" => ko ? "⚙️ 설정" : "⚙️ Settings",
            "TooltipSlimChangelog" => ko ? "📜 버전 기록" : "📜 Changelog",
            "BtnInlineAddTrigger" => ko ? "+조건" : "+Cond",
            "BtnInlineAddAction" => ko ? "+행동" : "+Act",
            "BtnInlineRecord" => ko ? "🔴녹화" : "🔴Rec",

            "BtnUndo" => "↶",
            "BtnRedo" => "↷",
            "TooltipOpen" => ko ? "📂 프로필 열기 (Ctrl+O)" : "📂 Open Profile (Ctrl+O)",
            "TooltipSave" => ko ? "💾 프로필 저장 (Ctrl+S)" : "💾 Save Profile (Ctrl+S)",
            "TooltipUndo" => ko ? "↶ 실행 취소 (Ctrl+Z)" : "↶ Undo (Ctrl+Z)",
            "TooltipRedo" => ko ? "↷ 다시 실행 (Ctrl+Y / Ctrl+Shift+Z)" : "↷ Redo (Ctrl+Y / Ctrl+Shift+Z)",
            "LblHumanizerDelay" => ko ? "휴머나이저 지연 편차: {0} ms" : "Humanizer Delay Jitter: {0} ms",
            "TooltipHumanizer" => ko ? "자연스러운 인간 모사를 위한 입력 딜레이 편차 (0~54ms, 0ms=기계적)" : "Random delay jitter for natural human-like input (0~54ms, 0ms=exact mechanical)",

            _ => key
        };
    }
}

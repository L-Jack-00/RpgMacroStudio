using System;
using System.Collections.Generic;

namespace RpgMacroHarness.Data;

public record VersionChangeEntry(
    string Version,
    string ReleaseDate,
    string TitleKo,
    string TitleEn,
    List<string> ChangesKo,
    List<string> ChangesEn
)
{
    // 기존 코드 하위 호환성을 위한 편의 프로퍼티
    public string Title => Localization.IsKorean ? TitleKo : TitleEn;
    public List<string> Changes => Localization.IsKorean ? ChangesKo : ChangesEn;
}

/// <summary>
/// 프로그램 버전 명명 규칙: 유의적 버전 (Semantic Versioning - Major.Minor.Patch)
/// </summary>
public static class VersionInfo
{
    public const string CurrentVersion = "5.1.0";
    public const string AppName = "RPG 매크로 스튜디오";

    public static readonly List<VersionChangeEntry> History = new()
    {
        new VersionChangeEntry(
            "5.1.0",
            "2026-10-10",
            "v5.1.0 키 동시 입력 세분화(키다운업/키다운/키업) 및 물리 누름 자가치유 안전망 탑재",
            "v5.1.0 Simultaneous Key Input Segmentation (KeyDownUp/KeyDown/KeyUp) & Self-Healing Safety Net",
            new List<string>
            {
                "[키 동시 입력 옵션 3종 세분화] 키 입력 액션을 '키다운업(누르고 뗌, 기본값)', '키다운(누름 유지)', '키업(손 뗌)'의 3가지 모드로 완벽 분리하여 Shift/Ctrl 조합키 및 방향키 누른 채 스킬 사용 등의 동시 입력을 완벽하게 지원합니다.",
                "[물리적 키 영구 눌림(Stuck Key) 방지 자가 치유 안전망] 매크로 실행 중 눌려 있는 모든 키를 전역 추적하여, 매크로 중지·일시정지·긴급 정지(ESC) 또는 예외 발생 시 물리적으로 눌려 있던 모든 키를 자동으로 즉시 해제합니다.",
                "[스마트 UI 반응형 편집 지원] 키 편집 다이얼로그에서 '키다운' 또는 '키업' 선택 시 불필요한 누름 시간(Hold Time) 필드가 자동으로 비활성화되며, 조합키 구성 팁 가이드를 제공합니다.",
                "[전 뷰 모드 시인성 강화] 클래식 계층 표 뷰, FSM 캔버스 노드 칩, 핀 그래프 노드에 ⬇️⬆️(다운업), ⬇️(키다운), ⬆️(키업) 아이콘과 뱃지가 직관적으로 표시됩니다."
            },
            new List<string>
            {
                "[3-Way Key Input Mode Segmentation] Keyboard actions are now segmented into 'KeyDownUp (Press & Release, Default)', 'KeyDown (Hold Pressed)', and 'KeyUp (Release)', fully enabling simultaneous inputs like combo keys and hold-to-move.",
                "[Stuck Key Self-Healing Safety Net] Tracks all active held keys globally and guarantees immediate physical release upon macro stop, pause, emergency stop (ESC), or exceptions.",
                "[Smart Responsive UI in Action Dialog] Automatically disables redundant hold duration controls when KeyDown or KeyUp is selected, with built-in combo key guidelines.",
                "[Enhanced Visual Representation Across All Views] Clear ⬇️⬆️, ⬇️, and ⬆️ icons and badges across Classic Table, FSM Canvas, and Pin Graph."
            }
        ),
        new VersionChangeEntry(
            "5.0.0",
            "2026-10-07",
            "v5.0.0 차세대 AI 어시스턴트 코어 및 멀티 프로바이더 엔진 탑재",
            "v5.0.0 Next-Gen AI Assistant Engine & Multi-Provider Architecture",
            new List<string>
            {
                "[Bring-Your-Own-Key 멀티 AI 프로바이더 지원] 사용자의 개인 API 키를 직접 사용하여 Google Gemini, OpenAI, Claude, DeepSeek, 로컬 무료 Ollama 간 자유로운 전환을 지원합니다. (서버 미경유, 개발자 토큰 비용 0원)",
                "[자연어 프롬프트 기반 FSM 매크로 프로필 자동 생성] 사용자의 자연어 시나리오 요청을 해석하여 상태, 다중 AND 트리거, 순차 액션이 완비된 매크로 프로필을 전자동으로 기획·생성합니다.",
                "[스마트 위상 정렬 노드 자동 배치] AI가 생성한 프로필을 MacroLayoutEngine과 실시간 연동하여 FSM 노드 및 핀 그래프 상에 100% 비중첩으로 자동 배치합니다.",
                "[오프라인 테스트 하네스 & 자가 치유 검증] MockAiProviderClient 및 단위 테스트 하네스를 통해 네트워크 없이도 100% 무결점 프로필 생성 및 역직렬화 회귀를 검증합니다."
            },
            new List<string>
            {
                "[Bring-Your-Own-Key Multi-Provider Support] Direct client-side integration for Google Gemini, OpenAI, Claude, DeepSeek, and local offline Ollama using the user's personal API keys.",
                "[Natural Language to FSM Macro Profile Generation] Automatically synthesizes complete FSM profiles with states, multi-AND triggers, and sequential actions from natural language prompts.",
                "[Zero-Collision Smart Auto-Layout] Seamlessly connects with MacroLayoutEngine to position nodes across FSM canvas and Pin graph without visual overlaps.",
                "[Offline Test Harness & Self-Healing Verification] Fully verified via MockAiProviderClient and rigorous unit test harnesses for zero-defect serialization."
            }
        ),
        new VersionChangeEntry(
            "4.0.0",
            "2026-10-06",
            "v4.0.0 대규모 메이저 릴리즈 (인터랙션 강화 및 통합 3대 뷰 완성)",
            "v4.0.0 Major Release: Enhanced Visual Interaction & Unified 3-View Architecture",
            new List<string>
            {
                "[핀 호환성 하이라이트 & 자석 결합] 핀 그래프에서 와이어를 끌고 있을 때 연결 가능한 상대방 핀(Flow-In / Data-In)이 테두리 펄스와 네온 컬러로 은은하게 반짝이며 마우스가 자석처럼 흡착 결합됩니다.",
                "[계층 표 드래그 앤 드롭 행 순서 변경] 클래식 계층 표 뷰에서 행을 마우스로 직접 잡고 위아래로 끌어다 놓거나(Drag & Drop), 우클릭 메뉴 및 Ctrl+↑/↓ 단축키로 액션과 이벤트 순서를 자유롭게 재정렬할 수 있습니다.",
                "[노드 반응형 확대] FSM 모드와 핀 모드 모두 노드 확대 시 텍스트와 그래픽 요소가 비례하여 커지며, 충분한 본문 여백을 확보했습니다.",
                "[비활성 노드 회색 타일] 비활성화된 노드 및 이벤트는 타일 배경과 헤더 전체가 회색조로 톤다운되어 시각적 구분이 명확해졌습니다.",
                "[인스펙터 토글 & 공간 확장] 상단 툴바 우측 끝에 상황별 화살표(◀/▶) 버튼을 배치하고, 인스펙터를 닫았을 때 우측 여백을 100% 제거했습니다.",
                "[핀 모드 참/거짓 분기 최적화] 항상 발동 조건은 단순 순차 흐름(Next ▶)만 제공하고, 실제 검사 조건에서만 참/거짓 분기 핀을 활성화합니다.",
                "[표 열 너비 자유 조절] 클래식 계층 표 및 인스펙터 목록의 모든 열 너비를 원하는 만큼 제한 없이 자유롭게 확장할 수 있습니다.",
                "[패치노트 & 단축키 안내 개선] 패치노트 창 하단 버튼 배치 및 시작 가이드의 단축키 표기를 사용자 설정과 언어에 맞게 다듬었습니다."
            },
            new List<string>
            {
                "[Pin Compatibility Glow & Magnetic Snap] Dragging wires highlights compatible target pins (Flow-In / Data-In) with pulsing neon halos and snaps magnetically to sockets.",
                "[Drag & Drop Table Row Reordering] Rows in the Classic Hierarchy Table can now be freely dragged and dropped, or reordered via context menu and Ctrl+Up/Down shortcuts.",
                "[Responsive Node Scaling] In both FSM and Pin modes, enlarged nodes now scale up fonts and graphics proportionately with comfortable margins.",
                "[Grey Inactive Nodes] Inactive nodes and events display grey backgrounds and headers to visually communicate disabled logic clearly.",
                "[Inspector Toggle & Space Optimization] Added a context-aware arrow button (◀/▶) at the top-right toolbar and removed right margins when collapsed.",
                "[Smart Pin Branching] Flow Out (Next ▶) pins are used for Always triggers, activating True/False branches only for conditional checks.",
                "[Unrestricted Column Resizing] Removed all width limitations across the Classic hierarchy table and Inspector lists.",
                "[Dialog & Shortcut Polish] Refined Patch Notes button layout and synchronized Getting Started hotkeys with user settings and language."
            }
        ),
        new VersionChangeEntry(
            "3.1.2",
            "2026-10-06",
            "작업 편의성 및 3대 뷰 통합 인터페이스 전면 개선 패치",
            "Enhanced Usability & Unified 3-View Interface Update",
            new List<string>
            {
                "[키보드 조작 일원화] FSM 카드, 핀 그래프, 클래식 표 어디서나 F2(수정), Delete(삭제), Space(활성 토글), Esc(선택 해제)가 동일하게 작동합니다.",
                "[화면 테마 최적화] 라이트 모드와 다크 모드 전환 시 색상이 즉시 반응하며, 표 우측의 불필요한 흰색 여백 현상을 완전히 해결했습니다.",
                "[클래식 계층 표 뷰] 표와 트리를 하나로 통합하여 계층 구조를 접고 펼칠 수 있으며, 텍스트가 길어도 잘리지 않고 2줄로 편안하게 볼 수 있습니다.",
                "[인스펙터 공간 절약] 우측 인스펙터를 접었을 때 화면을 가리지 않도록 정리하고, 창 크기를 줄여도 스크롤바로 모든 설정을 자유롭게 조작할 수 있습니다.",
                "[시스템 폰트 고정] 모든 화면에서 이모지와 아이콘이 깨짐 없이 선명하게 보이도록 시스템 전역 폰트를 안정화했습니다."
            },
            new List<string>
            {
                "[Unified Controls] Shortcuts (F2: Edit, Delete: Remove, Space: Toggle, Esc: Deselect) now work identically across FSM, Pin, and Classic views.",
                "[Theme Improvements] Seamless real-time switching between Light and Dark modes, eliminating unwanted white margin glitches.",
                "[Classic Hierarchy Table] Merged table and tree into a clear hierarchy with smooth collapse/expand and multi-line text support.",
                "[Compact Inspector] Cleaner collapsed view and responsive vertical scrolling that preserves all settings even on smaller windows.",
                "[Universal Font Fix] Enforced consistent emoji and symbol rendering across all dialogs and controls."
            }
        ),
        new VersionChangeEntry(
            "3.1.1",
            "2026-09-30",
            "새 상태 자동 번호 부여 및 안정성 개선",
            "Smart State Auto-Naming & Stability Fix",
            new List<string>
            {
                "[스마트 상태 네이밍] 새 상태 추가 시 비어있는 번호부터 순서대로 안전하게 자동 부여됩니다.",
                "[이름 충돌 방지] 노드 연결 중 새 상태를 생성해도 기존 상태 이름과 겹치지 않도록 보호됩니다."
            },
            new List<string>
            {
                "[Smart Naming] New states are sequentially and intelligently numbered without skipping numbers.",
                "[Conflict Protection] Prevents duplicate state names when generating states from canvas connections."
            }
        ),
        new VersionChangeEntry(
            "3.1.0",
            "2026-09-30",
            "자유형 핀 그래프 및 계층형 트리 뷰 대규모 업데이트",
            "Visual Pin Graph & Hierarchy Tree Major Release",
            new List<string>
            {
                "[자유형 핀 그래프] 조건과 행동 노드를 마우스 드래그 곡선 와이어로 자유롭게 연결하여 직관적인 로직을 구성할 수 있습니다.",
                "[참/거짓 갈래길 분기] 조건 만족 시와 불만족 시 각기 다른 행동으로 이어지는 갈래길 분기 로직을 지원합니다.",
                "[좌표 자동 전달] 이미지나 게이지 감지 시 찾은 화면 위치를 마우스 클릭 노드로 바로 전달할 수 있습니다.",
                "[계층 표 뷰 도입] 상태별 이벤트와 상세 조건/행동을 한눈에 체계적으로 파악할 수 있는 계층 표를 제공합니다."
            },
            new List<string>
            {
                "[Pin Graph View] Connect condition and action blocks freely using bezier curve wires for intuitive visual scripting.",
                "[Branching Logic] Direct execution paths dynamically based on True/False condition outcomes.",
                "[Coordinate Pipeline] Automatically forward detected image/gauge coordinates directly into mouse click actions.",
                "[Hierarchy Table] Introduced a structured hierarchy view to organize states, events, and action sequences."
            }
        ),
        new VersionChangeEntry(
            "3.0.0",
            "2026-09-30",
            "FSM 캔버스 및 클래식 통합 v3.0 정식 릴리즈",
            "Unified FSM Canvas & Classic View v3.0 Official Release",
            new List<string>
            {
                "[통합 뷰 시스템] 시각적인 캔버스 뷰와 익숙한 클래식 표 뷰를 클릭 한 번으로 자유롭게 넘나들 수 있습니다.",
                "[스마트 노드 정렬] 노드들이 서로 겹치지 않고 자연스럽게 왼쪽에서 오른쪽으로 정렬되는 자동 정렬을 탑재했습니다.",
                "[실시간 다국어 지원] 한국어와 영어를 프로그램 재시작 없이 즉시 변경할 수 있습니다.",
                "[안전한 실행 취소] 실수로 노드를 지우거나 변경해도 언제든 실행 취소(Ctrl+Z)와 다시 실행(Ctrl+Y)으로 복원할 수 있습니다."
            },
            new List<string>
            {
                "[Unified Views] Effortlessly switch between visual canvas and classic table views with complete synchronization.",
                "[Smart Auto-Layout] Cleanly arranges nodes from left to right without collisions or overlapping wires.",
                "[Instant Localization] Switch between Korean and English anytime without restarting the application.",
                "[Reliable Undo/Redo] Safe history management allows undoing (Ctrl+Z) and redoing (Ctrl+Y) anytime."
            }
        )
    };
}

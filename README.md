<p align="center">
  <img src="app_icon.png" width="128" height="128" alt="RPG Macro Studio Logo" />
</p>

<h1 align="center">RPG Macro Studio (RPG 매크로 스튜디오)</h1>

<p align="center">
  <strong>시각적 노드 그래프 & FSM 상태 머신 기반의 고성능 무설치 포터블 RPG 매크로 스튜디오</strong>
</p>

<p align="center">
  <a href="https://github.com/L-Jack-00/RpgMacroStudio/releases/latest"><img src="https://img.shields.io/badge/Release-v5.4.0-blue.svg?style=flat-square" alt="Latest Release" /></a>
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-0078D6.svg?style=flat-square&logo=windows" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-8.0%20Windows-512BD4.svg?style=flat-square&logo=dotnet" alt=".NET 8.0" />
  <img src="https://img.shields.io/badge/Vision-OpenCV%20Sharp%204-green.svg?style=flat-square&logo=opencv" alt="OpenCV" />
  <img src="https://img.shields.io/badge/Tests-33%2F33%20Passing-brightgreen.svg?style=flat-square" alt="Self-Tests" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square" alt="MIT License" /></a>
</p>

<br />

## 🌟 프로젝트 소개 (Overview)

**RPG Macro Studio**는 복잡한 게임 시나리오를 코딩 없이 시각적으로 설계하고, 오차 없는 초정밀 타이밍으로 실행할 수 있도록 설계된 전문 게임 자동화 개발 도구입니다.

단순한 무한 반복 클릭기를 넘어, **FSM(유한 상태 머신)**과 **언리얼 블루프린트 스타일의 비주얼 핀 그래프(Pin Graph)**를 제공하여 게임의 흐름(탐색 ➔ 타겟팅 ➔ 스킬 콤보 ➔ 물약/정비 ➔ 위기 탈출)을 직관적으로 제어할 수 있습니다.

또한 최신 **AI(LLM) 매크로 어시스턴트(BYOK)**를 탑재하여, 원하는 게임 시나리오를 자연어로 입력하면 상태 머신과 이벤트 규칙을 자동으로 설계해 줍니다.

---

## 🚀 빠른 다운로드 및 실행 방법 (Quick Start)

본 프로그램은 **포터블(무설치) 단일 파일 런처** 구조로 제작되어, 복잡한 설치 과정이나 추가 런타임 없이 바로 실행할 수 있습니다.

1. **[최신 릴리즈 다운로드 페이지](https://github.com/L-Jack-00/RpgMacroStudio/releases/latest)**로 이동합니다.
2. `RpgMacroStudio_v5.4.0_win-x64.zip` 파일을 다운로드합니다.
3. 원하는 폴더에 압축을 푼 뒤, **`RpgMacroStudio.exe`**를 더블 클릭하여 실행합니다.

> 💡 **시스템 요구사항**: Windows 10 / 11 (64-bit 전용)

---

## ✨ 핵심 기능 (Key Features)

### 1. 3대 뷰(View) 자유 전환
* **🔀 핀 그래프 모드 (Pin Graph)**: 노드와 와이어로 실행 흐름(Flow)과 연속 키 시퀀스를 직관적으로 연결하는 비주얼 에디터
* **📦 FSM 카드 모드 (State Machine)**: 게임 상태(대기, 사냥, 정비, 버프 등)별 카드와 전이 규칙을 한눈에 관리하는 상태 머신 뷰
* **☰ 클래식 모드 (Classic Tree-Table)**: 숙련자를 위해 이벤트와 행동을 계층형 테이블로 컴팩트하게 일괄 편집하는 표 형태의 뷰

### 2. ⏱️ 고정밀 폐루프 타이머 (PrecisionLoopController)
* 윈도우 OS의 타이머 퀀텀(15.6ms) 지연으로 인한 틱 오차를 매 프레임 실시간 측정하여 감산 보정하는 **자가 치유 폐루프 제어기** 탑재
* CPU 점유율 0%의 대기(Sleep)와 초정밀 SpinWait를 결합한 **하이브리드 대기**로 지연 드리프트 0% 달성 및 설정 주기(FPS) 수학적 수렴

### 3. 🧠 Bring-Your-Own-Key (BYOK) AI 매크로 어시스턴트
* 게임 시나리오를 자연어로 설명하면 FSM 상태와 발동 조건을 자동 추론하여 캔버스에 즉시 배치
* **Google Gemini, OpenAI (GPT-4o), Anthropic Claude, DeepSeek, Ollama(로컬 무료 LLM)** 및 커스텀 프록시 100% 호환
* 사용자 본인의 개인 API 키를 직접 입력하여 통신하므로 외부 서버 수집 0%의 강력한 보안성

### 4. 👁️ OpenCV 기반 고속 실시간 비전 엔진
* 다중 ROI(관심 영역) 지정, 템플릿 이미지 매칭, 픽셀 색상 판별, HP/MP 게이지 비율 계산 지원
* 게임 창이 백그라운드나 일부 가려진 상태에서도 안정적인 화면 감지 지원

### 5. 🛡️ 물리적 입력 자가 치유 및 안전 인터록 (Safety Guard)
* **비상정지(Emergency Stop)**: `ESC` 또는 `F12` 입력 시 모든 매크로 동작 즉각 0ms 하드웨어 차단
* **입력 안전망(Global Release Guard)**: 정지, 일시정지, 비상정지 또는 예외 발생 시 현재 누르고 있던 모든 물리적 키보드 키 및 마우스 버튼(좌/우/휠)을 100% 자동 해제하여 키 씹힘 및 마우스 끼임 원천 방지

### 6. 🌓 완벽한 다크/라이트 테마 & 다국어(i18n) 지원
* OS 타이틀바, 네이티브 컨트롤, 스크롤바까지 100% 딥 다크 모드 렌더링 지원
* 한국어 / 영어 1:1 완벽 대응 (총 159개 다국어 키 지원)

---

## ⌨️ 기본 단축키 (Global Hotkeys)

| 단축키 | 기능 설명 |
| :---: | :--- |
| **`F10`** | **시작 / 일시정지 / 재개 (Start / Pause / Resume)** |
| **`F11`** | **매크로 정지 (Stop)** |
| **`ESC` / `F12`** | **🚨 최우선 긴급 비상정지 (Emergency Stop)** |
| **`Space`** | 선택한 이벤트 활성 / 비활성화 토글 (On/Off) |
| **`F2` / `Enter`** | 선택한 노드 / 이벤트 속성 인스펙터 편집 |
| **`Delete`** | 선택한 노드 / 이벤트 즉시 삭제 |
| **`Ctrl + C / V`** | 노드 / 이벤트 복사 및 붙여넣기 (겹침 방지 자동 스폰) |
| **`Ctrl + Z / Y`** | 실행 취소 / 다시 실행 (Undo / Redo) |

*(※ 시작/정지/비상정지 단축키는 프로그램 상단 `⚙️ 설정` 메뉴에서 원하는 키로 자유롭게 변경할 수 있습니다.)*

---

## 🏗️ 아키텍처 및 무결성 검증 (Engineering Integrity)

본 프로젝트는 **LJack 런타임 하네스 엔지니어링 프로토콜 (v5.4.0)**을 엄격히 준수하여 개발되었습니다.

```text
[비즈니스 코어 & 상태 엔진] ── (I/O 디커플링) ── [OpenCV / Win32 입력 드라이버]
              │
              ├── [고정밀 틱 제어기 (PrecisionLoopController)] ➔ 16ms 폐루프 보정
              ├── [33종 헤드리스 자체 테스트 러너 (SelfTestRunner)] ➔ 33/33 ALL PASS
              └── [13대 종합 감사관 (Inspector Suite)] ➔ 결함 0건 100% 무결성 검증
```

* **33종 무인 헤드리스 자체 테스트 (`--test`)**: GUI 없이 백그라운드에서 전체 런타임, FSM 상태 전이, 템플릿 매칭, 타이머 드리프트 보정을 수학적으로 자동 검증
* **13대 종합 감사 에이전트 (Inspector Suite)**: 버전, 텍스트, UI 레이아웃, 코드 클린업, 다국어 번역, 테마 그래픽, 왓치독, 키보드 인터랙션, Undo/Redo, 리사이즈, 폰트/이모지, 3대 뷰 일관성, 잠재 맹점 레이더(Radar) 전수 검사 통과

---

## 🛠️ 소스 코드 빌드 방법 (Build from Source)

직접 소스 코드를 빌드하고 패키징하려면 .NET 8.0 SDK가 필요합니다.

```bash
# 1. 저장소 클론
git clone https://github.com/L-Jack-00/RpgMacroStudio.git
cd RpgMacroStudio

# 2. 솔루션 빌드
dotnet build RpgMacroStudio.sln -c Release

# 3. 자체 테스트 러너 실행
dotnet run --project src/RpgMacroStudio/RpgMacroStudio.csproj -c Release -- --test

# 4. 배포용 단일 런처 및 ZIP 패키징 빌드 (Python 3 필요)
python scripts/build_release.py
```

빌드가 완료되면 상위 디렉터리에 `RpgMacroStudio_Dist/` 및 `RpgMacroStudio_v5.4.0_win-x64.zip`이 자동으로 생성됩니다.

---

## 📄 라이선스 (License)

본 프로젝트는 [MIT 라이선스](LICENSE) 하에 자유롭게 배포 및 사용될 수 있습니다.

Copyright © 2026 **L-Jack** ([@L-Jack-00](https://github.com/L-Jack-00)). All rights reserved.

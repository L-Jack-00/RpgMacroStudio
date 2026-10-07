namespace RpgMacroHarness.Data;

/// <summary>
/// RpgMacroStudio의 4가지 UI 작업 뷰 모드 정의입니다.
/// </summary>
public enum AppViewMode
{
    /// <summary>
    /// 시각적 FSM 상태 노드 카드 캔버스 뷰 (유니티/언리얼 스테이트 머신 스타일)
    /// </summary>
    CanvasFsm = 0,

    /// <summary>
    /// 완전 자유형 핀 그래프 캔버스 뷰 (ComfyUI / 블루프린트 데이터/실행 핀 그래프 스타일)
    /// </summary>
    CanvasPinGraph = 1,

    /// <summary>
    /// 클래식 선형 목록 뷰 (전통적인 탭/리스트뷰 기반 순차 이벤트 탐색)
    /// </summary>
    ClassicList = 2,

    /// <summary>
    /// 스마트 계층형 트리 뷰 (상태-이벤트-조건-분기 계층 구조 트리뷰)
    /// </summary>
    ClassicTree = 3
}

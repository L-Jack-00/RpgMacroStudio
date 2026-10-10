using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json.Serialization;

namespace RpgMacroHarness.Data;

/// <summary>
/// 핀의 종류 (실행 흐름선 vs 데이터 전달선)
/// </summary>
public enum GraphPinKind
{
    ExecutionFlow, // 실행 순서 제어선 (▶)
    Data           // 데이터 전달선 (● - 좌표, 수치, 불리언 등)
}

/// <summary>
/// 핀의 입출력 방향
/// </summary>
public enum GraphPinDirection
{
    Input,
    Output
}

/// <summary>
/// 핀이 전달하는 데이터 타입
/// </summary>
public enum GraphPinDataType
{
    Flow,    // 실행 흐름
    Point,   // X, Y 화면 좌표 (PointF)
    Boolean, // 참/거짓 (조건 결과)
    Number,  // 수치 (타이머 ms, 변수 값 등)
    String   // 문자열 (상태명, 키보드 키 등)
}

/// <summary>
/// 핀 그래프의 개별 핀(Port) 정의
/// </summary>
public class GraphPin
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public GraphPinKind Kind { get; set; } = GraphPinKind.ExecutionFlow;
    public GraphPinDirection Direction { get; set; } = GraphPinDirection.Output;
    public GraphPinDataType DataType { get; set; } = GraphPinDataType.Flow;

    /// <summary>
    /// 조건 핀 전용: True(참/성공) 핀인지 False(거짓/Else) 핀인지 여부
    /// </summary>
    public bool IsBranchPin { get; set; } = false;
    public bool BranchCondition { get; set; } = true; // true = True/성공, false = False/Else

    public GraphPin() { }

    public GraphPin(string name, GraphPinKind kind, GraphPinDirection dir, GraphPinDataType dataType = GraphPinDataType.Flow)
    {
        Name = name;
        Kind = kind;
        Direction = dir;
        DataType = dataType;
    }

    public static GraphPin CreateFlowIn(string name = "In") =>
        new(name, GraphPinKind.ExecutionFlow, GraphPinDirection.Input, GraphPinDataType.Flow);

    public static GraphPin CreateFlowOut(string name = "Out") =>
        new(name, GraphPinKind.ExecutionFlow, GraphPinDirection.Output, GraphPinDataType.Flow);

    public static GraphPin CreateBranchOut(string name, bool isTrueBranch) =>
        new(name, GraphPinKind.ExecutionFlow, GraphPinDirection.Output, GraphPinDataType.Flow)
        {
            IsBranchPin = true,
            BranchCondition = isTrueBranch
        };

    public static GraphPin CreateDataIn(string name, GraphPinDataType type) =>
        new(name, GraphPinKind.Data, GraphPinDirection.Input, type);

    public static GraphPin CreateDataOut(string name, GraphPinDataType type) =>
        new(name, GraphPinKind.Data, GraphPinDirection.Output, type);
}

/// <summary>
/// 자유형 핀 그래프의 노드 유형
/// </summary>
public enum GraphNodeType
{
    Start,          // 진입점 노드 (시작 상태 노드)
    Condition,      // 조건 노드 (이미지/색상 감지, 타이머, 변수 조건 등) -> True/False(Else) 출력 지원
    Action,         // 행동 노드 (키보드, 마우스 클릭, 지연 등)
    ChangeState,    // 상태 전환 노드
    VariableControl,// 변수 조작 노드
    Merge,          // 다중 실행 흐름 병합 노드
    Comment,        // 주석 박스
    KeySequence     // 연속 키 입력 묶음 노드 (접기/펼치기 지원)
}

/// <summary>
/// 자유형 핀 그래프의 개별 노드
/// </summary>
public class GraphNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "노드";
    public GraphNodeType NodeType { get; set; } = GraphNodeType.Action;

    // 캔버스 상의 2D 위치 및 크기
    public float X { get; set; } = 100;
    public float Y { get; set; } = 100;
    public float Width { get; set; } = 180;
    public float Height { get; set; } = 100;

    // 소속 상태 태그 (FSM 상태와 매핑)
    public string StateTag { get; set; } = "기본 상태";

    // 활성화 여부 (비활성화 시 회색 렌더링)
    public bool IsEnabled { get; set; } = true;

    // 핀 목록
    public List<GraphPin> InputPins { get; set; } = new();
    public List<GraphPin> OutputPins { get; set; } = new();

    // 노드 내부 상세 데이터 페이로드 (조건 or 액션)
    public MacroTrigger? TriggerData { get; set; }
    public MacroAction? ActionData { get; set; }

    // 연속 키 입력 묶음(KeySequence) 관련 속성
    public List<MacroAction>? SequenceActions { get; set; }
    public bool IsCollapsed { get; set; } = true;
    public int SequenceStartIndex { get; set; } = -1;
    public string? SourceEventId { get; set; }

    // 부가 설명/메모
    public string? Description { get; set; }

    public GraphNode() { }

    public GraphNode(string title, GraphNodeType type, float x, float y)
    {
        Title = title;
        NodeType = type;
        X = x;
        Y = y;
    }

    public GraphPin? FindPin(string pinId) =>
        InputPins.FirstOrDefault(p => p.Id == pinId) ?? OutputPins.FirstOrDefault(p => p.Id == pinId);
}

/// <summary>
/// 핀과 핀 사이를 잇는 베지어 곡선 연결선(와이어)
/// </summary>
public class GraphConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string FromNodeId { get; set; } = "";
    public string FromPinId { get; set; } = "";

    public string ToNodeId { get; set; } = "";
    public string ToPinId { get; set; } = "";

    public GraphConnection() { }

    public GraphConnection(string fromNodeId, string fromPinId, string toNodeId, string toPinId)
    {
        FromNodeId = fromNodeId;
        FromPinId = fromPinId;
        ToNodeId = toNodeId;
        ToPinId = toPinId;
    }
}

/// <summary>
/// ComfyUI / 언리얼 블루프린트 스타일의 완전 자유형 핀-와이어 그래프 전체 모델
/// </summary>
public class PinGraphModel
{
    public List<GraphNode> Nodes { get; set; } = new();
    public List<GraphConnection> Connections { get; set; } = new();

    public GraphNode? FindNode(string nodeId) => Nodes.FirstOrDefault(n => n.Id == nodeId);

    public bool CanConnect(string fromNodeId, string fromPinId, string toNodeId, string toPinId)
    {
        // 1. 자기 자신 노드 내부 연결 방지
        if (string.IsNullOrEmpty(fromNodeId) || string.IsNullOrEmpty(toNodeId)) return false;
        if (fromNodeId == toNodeId) return false;

        // 노드 및 핀 유효성 조회
        var srcNode = FindNode(fromNodeId);
        var dstNode = FindNode(toNodeId);
        if (srcNode == null || dstNode == null) return false;

        var srcPin = srcNode.FindPin(fromPinId);
        var dstPin = dstNode.FindPin(toPinId);
        if (srcPin == null || dstPin == null) return false;

        // 2. 방향 정규화 검증 (하나는 Output, 하나는 Input이어야 함)
        if (srcPin.Direction == dstPin.Direction) return false;

        var sPin = srcPin.Direction == GraphPinDirection.Output ? srcPin : dstPin;
        var dPin = srcPin.Direction == GraphPinDirection.Output ? dstPin : srcPin;
        var sNodeId = srcPin.Direction == GraphPinDirection.Output ? fromNodeId : toNodeId;
        var dNodeId = srcPin.Direction == GraphPinDirection.Output ? toNodeId : fromNodeId;

        // 3. 핀 종류(Kind) 및 데이터 타입(DataType) 호환성 검증
        if (sPin.Kind != dPin.Kind) return false;
        if (sPin.Kind == GraphPinKind.Data && sPin.DataType != dPin.DataType) return false;

        // 4. 동일 와이어 중복 연결 방지
        if (Connections.Any(c => c.FromNodeId == sNodeId && c.FromPinId == sPin.Id &&
                                 c.ToNodeId == dNodeId && c.ToPinId == dPin.Id))
        {
            return false;
        }

        // 5. 직접 2노드 상호 순환(Direct 2-node cycle) 루프 방지
        if (Connections.Any(c => c.FromNodeId == dNodeId && c.ToNodeId == sNodeId))
        {
            return false;
        }

        return true;
    }

    public void AddConnection(string fromNodeId, string fromPinId, string toNodeId, string toPinId)
    {
        if (!CanConnect(fromNodeId, fromPinId, toNodeId, toPinId)) return;

        var srcNode = FindNode(fromNodeId);
        var dstNode = FindNode(toNodeId);
        if (srcNode == null || dstNode == null) return;

        var srcPin = srcNode.FindPin(fromPinId);
        var dstPin = dstNode.FindPin(toPinId);
        if (srcPin == null || dstPin == null) return;

        // 방향 정규화 (Input -> Output 역방향 드래그 시 정규화)
        if (srcPin.Direction == GraphPinDirection.Input && dstPin.Direction == GraphPinDirection.Output)
        {
            var tempNodeId = fromNodeId; fromNodeId = toNodeId; toNodeId = tempNodeId;
            var tempPinId = fromPinId; fromPinId = toPinId; toPinId = tempPinId;
            var tempNode = srcNode; srcNode = dstNode; dstNode = tempNode;
            var tempPin = srcPin; srcPin = dstPin; dstPin = tempPin;
        }

        // 6. 데이터 입력 핀 단일 소스 원칙 (N:1 데이터 충돌 방지)
        if (dstPin.Kind == GraphPinKind.Data && dstPin.Direction == GraphPinDirection.Input)
        {
            Connections.RemoveAll(c => c.ToPinId == toPinId);
        }

        // 7. 1:N 분기 및 N:1 합류 허용 (Flow 와이어는 1:N 분기와 N:1 합류를 100% 안전하게 지원)
        Connections.Add(new GraphConnection(fromNodeId, fromPinId, toNodeId, toPinId));
    }

    public void RemoveConnection(string connectionId)
    {
        Connections.RemoveAll(c => c.Id == connectionId);
    }

    public void RemoveNode(string nodeId)
    {
        Nodes.RemoveAll(n => n.Id == nodeId);
        Connections.RemoveAll(c => c.FromNodeId == nodeId || c.ToNodeId == nodeId);
    }
}

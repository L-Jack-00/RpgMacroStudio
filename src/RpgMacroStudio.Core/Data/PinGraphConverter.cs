using System;
using System.Collections.Generic;
using System.Linq;

namespace RpgMacroHarness.Data;

/// <summary>
/// FSM 상태-이벤트 프로필(MacroEventProfile)과 완전 자유형 핀 그래프(PinGraphModel) 간의 양방향 자동 변환기입니다.
/// 기존 v3.0.0 프로필 데이터를 100% 보존하면서 직관적인 핀 그래프로 펼쳐냅니다.
/// </summary>
public static class PinGraphConverter
{
    public static PinGraphModel FromProfile(MacroEventProfile profile, bool ignoreSavedPositions = false)
    {
        var graph = new PinGraphModel();
        if (profile == null) return graph;

        float currentY = 80;

        // 1. 각 상태(State)별로 시작 노드 및 내부 체인 생성
        var allStates = profile.States.Distinct().ToList();
        if (!allStates.Contains("Global")) allStates.Insert(0, "Global");

        foreach (var state in allStates)
        {
            float stateOriginX = 80;
            float stateOriginY = currentY;

            // FSM 위치를 핀 그래프에 혼용하면 상태 간 노드 겹침이 발생하므로,
            // 핀 그래프 전용 저장 위치가 유효할 때만 사용하고 자동 정렬 시에는 독립 수직 밴드(currentY)로 배치
            if (!ignoreSavedPositions && profile.PinGraphNodePositions.TryGetValue($"start_{state}", out var savedStartPos))
            {
                stateOriginX = savedStartPos.X;
                stateOriginY = savedStartPos.Y;
            }

            // 시작 노드 생성
            bool isInitial = (state == profile.InitialState);
            string startTitle = MacroEventProfile.IsGlobalState(state) ? "🌐 전역 흐름" : $"🏁 상태: {state}";
            var startNode = new GraphNode(startTitle, GraphNodeType.Start, stateOriginX, stateOriginY)
            {
                Id = $"start_{state}",
                StateTag = state,
                Width = 200,
                Height = 72,
                Description = isInitial ? "프로그램 시작 시 진입점" : $"{state} 상태 활성화 시 진입"
            };
            if (!ignoreSavedPositions && profile.PinGraphNodeSizes.TryGetValue(startNode.Id, out var savedStartSize))
            {
                startNode.Width = savedStartSize.Width;
                startNode.Height = savedStartSize.Height;
            }

            var startFlowOut = GraphPin.CreateFlowOut("Flow ▶");
            startNode.OutputPins.Add(startFlowOut);
            graph.Nodes.Add(startNode);

            // 해당 상태에 속한 이벤트 목록 추출
            var eventsInState = profile.Events
                .Where(e => (MacroEventProfile.IsGlobalState(state) && MacroEventProfile.IsGlobalState(e.StateTag)) ||
                            (!MacroEventProfile.IsGlobalState(state) && e.StateTag == state))
                .ToList();

            GraphNode previousTailNode = startNode;
            GraphPin previousFlowPin = startFlowOut;

            float eventOffsetX = stateOriginX + 260;
            float eventOffsetY = stateOriginY;
            float maxEventBottomY = stateOriginY + startNode.Height;

            foreach (var evt in eventsInState)
            {
                // A. 조건 노드들 생성
                GraphNode? lastTriggerNode = null;
                GraphPin? lastTriggerTruePin = null;

                for (int tIdx = 0; tIdx < evt.Triggers.Count; tIdx++)
                {
                    var trg = evt.Triggers[tIdx];
                    string trigTitle = $"◈ 조건: {trg.GetSummary()}";
                    var trigNode = new GraphNode(trigTitle, GraphNodeType.Condition, eventOffsetX, eventOffsetY)
                    {
                        Id = $"trig_{evt.Id}_{tIdx}",
                        StateTag = state,
                        TriggerData = trg,
                        IsEnabled = evt.IsEnabled,
                        Width = 250,
                        Height = 95,
                        Description = $"이벤트 [{evt.Name}]의 조건 #{tIdx + 1}"
                    };
                    if (!ignoreSavedPositions && profile.PinGraphNodePositions.TryGetValue(trigNode.Id, out var savedTrigPos))
                    {
                        trigNode.X = savedTrigPos.X;
                        trigNode.Y = savedTrigPos.Y;
                    }
                    if (!ignoreSavedPositions && profile.PinGraphNodeSizes.TryGetValue(trigNode.Id, out var savedTrigSize))
                    {
                        trigNode.Width = savedTrigSize.Width;
                        trigNode.Height = savedTrigSize.Height;
                    }

                    bool ko = Localization.IsKorean;
                    var flowIn = GraphPin.CreateFlowIn("In ▶");
                    trigNode.InputPins.Add(flowIn);

                    GraphPin exitFlowPin;
                    // 항상 발동(Always) 조건은 참/거짓 분기가 없으므로 단순 Flow Out만 제공
                    if (trg.Type == TriggerType.Always)
                    {
                        exitFlowPin = GraphPin.CreateFlowOut("Next ▶");
                        trigNode.OutputPins.Add(exitFlowPin);
                    }
                    else
                    {
                        var truePin = GraphPin.CreateBranchOut(ko ? "참 ✔" : "True ✔", true);
                        var falsePin = GraphPin.CreateBranchOut(ko ? "거짓 ✖" : "False ✖", false);
                        trigNode.OutputPins.Add(truePin);
                        trigNode.OutputPins.Add(falsePin);
                        exitFlowPin = truePin;
                    }

                    // 비전 감지인 경우 감지 좌표 출력 핀 추가
                    if (trg.Type == TriggerType.VisionDetection || trg.Type == TriggerType.ImageDetection)
                    {
                        var coordPin = GraphPin.CreateDataOut("Coord ●", GraphPinDataType.Point);
                        trigNode.OutputPins.Add(coordPin);
                    }

                    graph.Nodes.Add(trigNode);

                    // 연결: 이전 노드 -> 현재 조건 In
                    graph.AddConnection(previousTailNode.Id, previousFlowPin.Id, trigNode.Id, flowIn.Id);

                    lastTriggerNode = trigNode;
                    lastTriggerTruePin = exitFlowPin;

                    previousTailNode = trigNode;
                    previousFlowPin = exitFlowPin;

                    if (trigNode.Y + trigNode.Height > maxEventBottomY)
                    {
                        maxEventBottomY = trigNode.Y + trigNode.Height;
                    }

                    eventOffsetX += trigNode.Width + 50;
                }

                // B. 행동 노드들 생성
                float actionOffsetX = eventOffsetX;
                float actionOffsetY = eventOffsetY;

                for (int aIdx = 0; aIdx < evt.Actions.Count; aIdx++)
                {
                    var act = evt.Actions[aIdx];
                    string actTitle = act.Type switch
                    {
                        ActionType.KeyPress => act.KeyMode switch
                        {
                            KeyStrokeMode.KeyDown => $"⬇️ 키다운: {act.Key}",
                            KeyStrokeMode.KeyUp => $"⬆️ 키업: {act.Key}",
                            _ => $"⌨️ 키: {act.Key}"
                        },
                        ActionType.Wait => $"⏳ 대기: {act.WaitDurationMs}ms",
                        ActionType.MouseClick => act.TargetMode == MouseTargetMode.TriggerDetected ? "🖱️ 감지 위치 클릭" : $"🖱️ 클릭 ({act.CustomX}, {act.CustomY})",
                        ActionType.VariableControl => $"🔢 변수: {act.VariableName} = {act.VariableValue}",
                        ActionType.ChangeState => $"🔄 상태 전이: [{act.TargetState}]",
                        _ => "⚡ 행동"
                    };

                    var actNode = new GraphNode(actTitle, act.Type == ActionType.ChangeState ? GraphNodeType.ChangeState : GraphNodeType.Action, actionOffsetX, actionOffsetY)
                    {
                        Id = $"act_{evt.Id}_{aIdx}",
                        StateTag = state,
                        ActionData = act,
                        IsEnabled = evt.IsEnabled,
                        Width = 220,
                        Height = 80,
                        Description = act.Description
                    };
                    if (!ignoreSavedPositions && profile.PinGraphNodePositions.TryGetValue(actNode.Id, out var savedActPos))
                    {
                        actNode.X = savedActPos.X;
                        actNode.Y = savedActPos.Y;
                    }
                    if (!ignoreSavedPositions && profile.PinGraphNodeSizes.TryGetValue(actNode.Id, out var savedActSize))
                    {
                        actNode.Width = savedActSize.Width;
                        actNode.Height = savedActSize.Height;
                    }

                    var actFlowIn = GraphPin.CreateFlowIn("In ▶");
                    var actFlowOut = GraphPin.CreateFlowOut("Next ▶");
                    actNode.InputPins.Add(actFlowIn);
                    actNode.OutputPins.Add(actFlowOut);

                    // 마우스 클릭 노드인 경우 좌표 데이터 입력 핀 추가
                    if (act.Type == ActionType.MouseClick)
                    {
                        var inCoordPin = GraphPin.CreateDataIn("Coord ●", GraphPinDataType.Point);
                        actNode.InputPins.Add(inCoordPin);

                        // 앞선 조건 노드에 감지 좌표 핀이 있다면 자동 연결
                        if (lastTriggerNode != null)
                        {
                            var outCoordPin = lastTriggerNode.OutputPins.FirstOrDefault(p => p.Kind == GraphPinKind.Data);
                            if (outCoordPin != null)
                            {
                                graph.AddConnection(lastTriggerNode.Id, outCoordPin.Id, actNode.Id, inCoordPin.Id);
                            }
                        }
                    }

                    graph.Nodes.Add(actNode);

                    // 이전 노드에서 현재 액션 노드로 Flow 연결
                    graph.AddConnection(previousTailNode.Id, previousFlowPin.Id, actNode.Id, actFlowIn.Id);

                    previousTailNode = actNode;
                    previousFlowPin = actFlowOut;

                    if (actNode.Y + actNode.Height > maxEventBottomY)
                    {
                        maxEventBottomY = actNode.Y + actNode.Height;
                    }

                    actionOffsetX += actNode.Width + 50;
                }

                eventOffsetY += 140;
                eventOffsetX = stateOriginX + 260;
                if (eventOffsetY > maxEventBottomY) maxEventBottomY = eventOffsetY;
            }

            currentY = maxEventBottomY + 90;
        }

        return graph;
    }
}

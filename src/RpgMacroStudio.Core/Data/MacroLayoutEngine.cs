using System;
using System.Collections.Generic;
using System.Linq;

namespace RpgMacroHarness.Data;

/// <summary>
/// FSM 상태 노드 그래프의 스마트 위상 정렬 및 충돌 없는 자동 배치(Auto-Layout) 엔진입니다.
/// 전이(Transition) 연결을 바탕으로 왼쪽에서 오른쪽(Left-to-Right)으로 흐름을 형성하며,
/// 각 노드의 동적 높이를 계산하여 어떠한 상태 노드도 겹치지 않도록 보장합니다.
/// </summary>
public static class MacroLayoutEngine
{
    public static void ComputeSmartLayout(
        MacroEventProfile profile,
        Func<string, float>? getNodeHeightFunc = null,
        float startX = 80f,
        float startY = 80f,
        float colSpacingX = 420f,
        float verticalGap = 60f,
        float defaultNodeHeight = 220f)
    {
        getNodeHeightFunc ??= (_ => defaultNodeHeight);

        // A. FSM 전이 관계(Adjacency) 그래프 구축
        var transitions = new Dictionary<string, HashSet<string>>();
        foreach (var st in profile.States)
        {
            transitions[st] = new HashSet<string>();
        }

        foreach (var evt in profile.Events)
        {
            if (string.IsNullOrEmpty(evt.StateTag) || !transitions.ContainsKey(evt.StateTag)) continue;
            foreach (var act in evt.Actions)
            {
                if (act.Type == ActionType.ChangeState && !string.IsNullOrEmpty(act.TargetState) && transitions.ContainsKey(act.TargetState))
                {
                    transitions[evt.StateTag].Add(act.TargetState);
                }
            }
        }

        // B. 각 상태의 열(Column / Depth) 결정
        var columnMap = new Dictionary<string, int>();

        // (1) 전역 상태는 항상 Col 0
        string? globalState = profile.States.FirstOrDefault(MacroEventProfile.IsGlobalState);
        if (globalState != null)
        {
            columnMap[globalState] = 0;
        }

        // (2) 시작 상태는 Col 1에서 시작하여 BFS로 타깃 상태들을 오른쪽 레벨(Col 2, 3...)로 배치
        string initialState = profile.InitialState ?? "";
        if (string.IsNullOrEmpty(initialState) || !profile.States.Contains(initialState))
        {
            initialState = profile.States.FirstOrDefault(s => !MacroEventProfile.IsGlobalState(s)) ?? "";
        }

        if (!string.IsNullOrEmpty(initialState))
        {
            columnMap[initialState] = 1;
            var queue = new Queue<string>();
            queue.Enqueue(initialState);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                int nextCol = columnMap[cur] + 1;

                if (transitions.TryGetValue(cur, out var targets))
                {
                    foreach (var target in targets)
                    {
                        if (MacroEventProfile.IsGlobalState(target)) continue;
                        if (!columnMap.TryGetValue(target, out int existingCol))
                        {
                            columnMap[target] = nextCol;
                            queue.Enqueue(target);
                        }
                        else if (existingCol < nextCol)
                        {
                            columnMap[target] = nextCol;
                            queue.Enqueue(target);
                        }
                    }
                }
            }
        }

        // (3) BFS에 도달하지 않은 고립/일반 상태들은 Col 1부터 순차적으로 배치
        int fallbackCol = 1;
        foreach (var st in profile.States)
        {
            if (!columnMap.ContainsKey(st))
            {
                if (MacroEventProfile.IsGlobalState(st))
                {
                    columnMap[st] = 0;
                }
                else
                {
                    columnMap[st] = fallbackCol;
                }
            }
        }

        // C. 열별로 상태들을 그룹화하고 각 노드의 동적 높이를 계산하여 Y 좌표 배치
        var columns = columnMap
            .GroupBy(kvp => kvp.Value)
            .OrderBy(g => g.Key);

        foreach (var group in columns)
        {
            int colIndex = group.Key;
            float currentX = startX + colIndex * colSpacingX;
            float currentY = startY;

            // 시작 상태가 해당 열에 있다면 최상단에 우선 배치
            var statesInCol = group.Select(g => g.Key).ToList();
            if (statesInCol.Contains(initialState))
            {
                statesInCol.Remove(initialState);
                statesInCol.Insert(0, initialState);
            }

            foreach (var st in statesInCol)
            {
                float nodeHeight = getNodeHeightFunc(st);
                profile.NodePositions[st] = new NodeCanvasPosition(currentX, currentY);
                currentY += nodeHeight + verticalGap;
            }
        }
    }

    /// <summary>
    /// 기존 노드들의 경계 박스(AABB)와 겹치지 않는 가장 가까운 안전 배치 좌표를 탐색하여 반환합니다.
    /// (나선형/격자형 공간 탐색으로 기존 노드와의 겹침 원천 방지)
    /// </summary>
    public static NodeCanvasPosition FindSafeSpawnPosition(
        MacroEventProfile profile,
        float preferredX = 300f,
        float preferredY = 150f,
        float nodeWidth = 340f,
        float nodeHeight = 220f,
        float margin = 40f)
    {
        // 등록된 기존 노드가 없으면 기본 좌표 반환
        if (profile.NodePositions.Count == 0)
        {
            return new NodeCanvasPosition(preferredX, preferredY);
        }

        // 기존 노드들의 AABB(Axis-Aligned Bounding Box) 목록 구축
        var existingBoxes = new List<(float X1, float Y1, float X2, float Y2)>();
        foreach (var kvp in profile.NodePositions)
        {
            existingBoxes.Add((
                kvp.Value.X - margin,
                kvp.Value.Y - margin,
                kvp.Value.X + nodeWidth + margin,
                kvp.Value.Y + nodeHeight + margin
            ));
        }

        bool IntersectsAny(float x, float y)
        {
            float x2 = x + nodeWidth;
            float y2 = y + nodeHeight;
            foreach (var b in existingBoxes)
            {
                if (x < b.X2 && x2 > b.X1 && y < b.Y2 && y2 > b.Y1)
                {
                    return true;
                }
            }
            return false;
        }

        // 1. 선호 위치가 비어있으면 즉시 사용
        if (!IntersectsAny(preferredX, preferredY))
        {
            return new NodeCanvasPosition(preferredX, preferredY);
        }

        // 2. 격자 확장 탐색 (오른쪽 및 아래쪽으로 40px 단위 탐색)
        float stepX = nodeWidth + margin;
        float stepY = nodeHeight + margin;

        // 우선 오른쪽으로 이동하며 빈 슬롯 탐색
        for (int col = 1; col <= 10; col++)
        {
            float candidateX = preferredX + col * stepX;
            if (!IntersectsAny(candidateX, preferredY))
            {
                return new NodeCanvasPosition(candidateX, preferredY);
            }
        }

        // 아래쪽 및 대각선 방향 격자 탐색
        for (int row = 1; row <= 15; row++)
        {
            for (int col = 0; col <= 10; col++)
            {
                float candidateX = preferredX + col * stepX;
                float candidateY = preferredY + row * stepY;
                if (!IntersectsAny(candidateX, candidateY))
                {
                    return new NodeCanvasPosition(candidateX, candidateY);
                }
            }
        }

        // 최후의 수단: 가장 오른쪽 노드의 오른쪽 끝
        float maxX = profile.NodePositions.Values.Max(p => p.X);
        return new NodeCanvasPosition(maxX + nodeWidth + margin, preferredY);
    }
}

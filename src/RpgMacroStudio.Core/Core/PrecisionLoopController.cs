using System;
using System.Diagnostics;
using System.Threading;

namespace RpgMacroHarness.Core;

/// <summary>
/// 연산 소요 시간 및 OS 스케줄러(15.6ms 퀀텀) 타이머 오차로 인한 주기 밀림(Time Drift)을
/// 스스로 측정하고 다음 대기에 반영하는 고정밀 폐루프(Closed-Loop) 틱 제어기입니다.
/// </summary>
public class PrecisionLoopController
{
    private long _lastTickTimestamp = 0;
    private double _accumulatedDriftMs = 0;
    private const double MaxCatchupClampMs = 100.0; // 과도한 캐치업 폭주 방지 클램프

    public double AccumulatedDriftMs => _accumulatedDriftMs;
    public double LastTickDurationMs { get; private set; } = 0;
    public double LastWaitDurationMs { get; private set; } = 0;
    public int TickCount { get; private set; } = 0;

    public PrecisionLoopController()
    {
    }

    /// <summary>
    /// 루프를 시작하거나 일시정지 후 재개할 때 타이머 기준 시각과 오차를 초기화합니다.
    /// </summary>
    public void Reset()
    {
        _accumulatedDriftMs = 0;
        _lastTickTimestamp = Stopwatch.GetTimestamp();
        TickCount = 0;
        LastTickDurationMs = 0;
        LastWaitDurationMs = 0;
    }

    /// <summary>
    /// 이번 틱의 연산 소요 시간 및 이전 오차(Drift)를 계산하여,
    /// 목표 주기(targetIntervalMs)에 정확히 도달할 때까지 자가 치유 피드백 보정 대기를 수행합니다.
    /// </summary>
    /// <param name="targetIntervalMs">목표 틱 주기 (밀리초)</param>
    /// <param name="isCancelled">비상 정지 또는 작업 중단 검사 델리게이트</param>
    /// <returns>이번 틱에 실제로 대기한 시간 (밀리초)</returns>
    public double WaitForNextTick(int targetIntervalMs, Func<bool>? isCancelled = null)
    {
        if (targetIntervalMs <= 0)
        {
            _lastTickTimestamp = Stopwatch.GetTimestamp();
            return 0;
        }

        if (_lastTickTimestamp == 0)
        {
            _lastTickTimestamp = Stopwatch.GetTimestamp();
        }

        long waitStartTimestamp = Stopwatch.GetTimestamp();
        double elapsedSinceLastTickMs = (double)(waitStartTimestamp - _lastTickTimestamp) * 1000.0 / Stopwatch.Frequency;

        // 목표 시간에서 이미 경과된 연산 시간을 뺀 순수 대기 필요 시간 계산
        // 이전 회차에서 밀렸던 누적 오차(_accumulatedDriftMs)를 함께 감산하여 폐루프 수렴 유도
        double targetWaitMs = targetIntervalMs - elapsedSinceLastTickMs - _accumulatedDriftMs;

        if (targetWaitMs > 0)
        {
            // 하이브리드 대기 (Hybrid Sleep & Precision Spin):
            // 1) 4ms 이상의 긴 대기는 (targetWaitMs - 2ms) 만큼 Thread.Sleep을 주어 CPU 점유율 0% 유지
            // 2) 잔여 1~2ms는 정밀 SpinWait를 사용하여 Windows OS의 15.6ms 타이머 퀀텀 오차 극복
            if (targetWaitMs > 4.0)
            {
                int sleepSlice = (int)(targetWaitMs - 2.0);
                if (sleepSlice > 0)
                {
                    if (isCancelled != null && isCancelled()) return 0;
                    Thread.Sleep(sleepSlice);
                }
            }

            // 미세 잔여 시간 고정밀 수렴
            long targetEndTimestamp = _lastTickTimestamp + (long)((targetIntervalMs - _accumulatedDriftMs) * Stopwatch.Frequency / 1000.0);
            while (Stopwatch.GetTimestamp() < targetEndTimestamp)
            {
                if (isCancelled != null && isCancelled()) break;
                Thread.SpinWait(10);
            }
        }

        long tickEndTimestamp = Stopwatch.GetTimestamp();
        LastWaitDurationMs = (double)(tickEndTimestamp - waitStartTimestamp) * 1000.0 / Stopwatch.Frequency;
        LastTickDurationMs = (double)(tickEndTimestamp - _lastTickTimestamp) * 1000.0 / Stopwatch.Frequency;
        TickCount++;

        // 이번 틱에서 발생한 오차 (실제 총 소요 시간 - 목표 주기)
        double currentError = LastTickDurationMs - targetIntervalMs;

        // 누적 드리프트 갱신 및 과도한 폭주/적체 방지 클램핑
        _accumulatedDriftMs = Math.Clamp(_accumulatedDriftMs + currentError, -MaxCatchupClampMs, MaxCatchupClampMs);

        _lastTickTimestamp = tickEndTimestamp;
        return LastWaitDurationMs;
    }
}

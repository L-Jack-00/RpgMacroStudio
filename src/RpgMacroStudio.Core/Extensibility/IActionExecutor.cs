using System.Collections.Concurrent;
using OpenCvSharp;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.Extensibility;

/// <summary>
/// 액션 실행 시 제공되는 런타임 실행 컨텍스트 정보
/// </summary>
public class ActionExecutionContext
{
    public MacroEvent Event { get; set; } = null!;
    public OpenCvSharp.Point DetectedPoint { get; set; }
    public CancellationToken CancellationToken { get; set; }
    public ManualResetEventSlim PauseEvent { get; set; } = null!;
    public ConcurrentDictionary<string, double> Variables { get; set; } = null!;
    public IDictionary<string, Mat> TemplateCache { get; set; } = null!;
    public Action<string> LogAction { get; set; } = null!;
    public Action<string> ChangeStateAction { get; set; } = null!;
    public Func<bool> IsRunningFunc { get; set; } = null!;
    public Func<bool> IsPausedFunc { get; set; } = null!;
}

/// <summary>
/// OCP(개방-폐쇄 원칙) 기반의 행동 실행 인터페이스.
/// 향후 웹훅, 디스코드 알림, 사운드 재생, 외부 프로세스 트리거 등 새로운 행동을 기존 하네스 수정 없이 확장할 수 있습니다.
/// </summary>
public interface IActionExecutor
{
    ActionType SupportedType { get; }
    void Execute(MacroAction action, ActionExecutionContext context);
}

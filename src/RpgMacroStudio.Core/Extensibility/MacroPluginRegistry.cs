using System.Collections.Concurrent;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.Extensibility;

/// <summary>
/// 트리거 평가자 및 액션 실행자를 관리하는 중앙 확장 레지스트리 (개방-폐쇄 원칙 OCP 구현)
/// </summary>
public static class MacroPluginRegistry
{
    private static readonly ConcurrentDictionary<TriggerType, ITriggerEvaluator> _evaluators = new();
    private static readonly ConcurrentDictionary<ActionType, IActionExecutor> _executors = new();

    public static void RegisterEvaluator(ITriggerEvaluator evaluator)
    {
        _evaluators[evaluator.SupportedType] = evaluator;
    }

    public static void RegisterExecutor(IActionExecutor executor)
    {
        _executors[executor.SupportedType] = executor;
    }

    public static ITriggerEvaluator? GetEvaluator(TriggerType type)
    {
        _evaluators.TryGetValue(type, out var ev);
        return ev;
    }

    public static IActionExecutor? GetExecutor(ActionType type)
    {
        _executors.TryGetValue(type, out var ex);
        return ex;
    }

    public static bool HasEvaluator(TriggerType type) => _evaluators.ContainsKey(type);
    public static bool HasExecutor(ActionType type) => _executors.ContainsKey(type);
}

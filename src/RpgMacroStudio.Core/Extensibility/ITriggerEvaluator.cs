using System.Collections.Concurrent;
using OpenCvSharp;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.Extensibility;

/// <summary>
/// 트리거 평가 시 제공되는 런타임 컨텍스트 정보
/// </summary>
public class TriggerEvaluationContext
{
    public string EventId { get; set; } = "";
    public Mat? Screen { get; set; }
    public ConcurrentDictionary<string, double> Variables { get; set; } = null!;
    public IDictionary<string, DateTime> LastTriggeredMap { get; set; } = null!;
    public IDictionary<string, Mat> TemplateCache { get; set; } = null!;
}

/// <summary>
/// OCP(개방-폐쇄 원칙) 기반의 조건 평가 인터페이스.
/// 향후 OCR 인식, 오디오 감지, 네트워크 신호 등 새로운 트리거 유형을 기존 하네스 수정 없이 확장할 수 있습니다.
/// </summary>
public interface ITriggerEvaluator
{
    TriggerType SupportedType { get; }
    bool Evaluate(MacroTrigger trigger, TriggerEvaluationContext context, out OpenCvSharp.Point detectedPoint);
}

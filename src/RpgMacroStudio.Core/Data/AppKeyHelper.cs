using System;

namespace RpgMacroHarness.Data;

/// <summary>
/// 키보드 키 및 마우스 버튼의 표준 표시 문자열을 제공하는 전역 통합 헬퍼입니다.
/// </summary>
public static class AppKeyHelper
{
    /// <summary>
    /// 마우스 버튼을 자연스러운 로컬라이즈 문자열(전체 명칭)로 변환합니다.
    /// </summary>
    public static string FormatMouseButton(MouseButtonType button, bool isKorean)
    {
        return button switch
        {
            MouseButtonType.Left => isKorean ? "좌클릭" : "Left Click",
            MouseButtonType.Right => isKorean ? "우클릭" : "Right Click",
            MouseButtonType.Middle => isKorean ? "휠클릭" : "Wheel Click",
            MouseButtonType.XButton1 => isKorean ? "측면 뒤로(X1)" : "Side Back (X1)",
            MouseButtonType.XButton2 => isKorean ? "측면 앞으로(X2)" : "Side Forward (X2)",
            _ => isKorean ? "클릭" : "Click"
        };
    }

    /// <summary>
    /// 노드 캔버스 및 칩 뱃지에 적합한 마우스 버튼 단축 문자열을 반환합니다.
    /// </summary>
    public static string FormatMouseButtonShort(MouseButtonType button, bool isKorean)
    {
        return button switch
        {
            MouseButtonType.Left => isKorean ? "좌클릭" : "Left",
            MouseButtonType.Right => isKorean ? "우클릭" : "Right",
            MouseButtonType.Middle => isKorean ? "휠클릭" : "Middle",
            MouseButtonType.XButton1 => isKorean ? "측면1(뒤로)" : "X1(Back)",
            MouseButtonType.XButton2 => isKorean ? "측면2(앞으로)" : "X2(Fwd)",
            _ => isKorean ? "클릭" : "Click"
        };
    }

    /// <summary>
    /// 키보드 가상 키 코드를 직관적인 표준 키캡 텍스트로 정제합니다.
    /// </summary>
    public static string FormatKey(string? rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey)) return "KEY";
        string k = rawKey.Trim();

        // D0 ~ D9 숫자키
        if (k.StartsWith("D") && k.Length == 2 && char.IsDigit(k[1]))
            return k[1].ToString();

        // NumPad0 ~ NumPad9
        if (k.StartsWith("NumPad") && k.Length == 7 && char.IsDigit(k[6]))
            return $"Num {k[6]}";

        return k switch
        {
            "Return" => "Enter",
            "Back" => "Backspace",
            "Capital" => "CapsLock",
            "Next" => "PageDown",
            "Prior" => "PageUp",
            "Space" => "Space",
            "ControlKey" or "LControlKey" or "RControlKey" => "Ctrl",
            "Menu" or "LMenu" or "RMenu" => "Alt",
            "ShiftKey" or "LShiftKey" or "RShiftKey" => "Shift",
            "Oemtilde" => "~",
            "OemMinus" => "-",
            "Oemplus" => "+",
            "OemOpenBrackets" => "[",
            "OemCloseBrackets" => "]",
            "Oem1" or "OemSemicolon" => ";",
            "Oem7" or "OemQuotes" => "'",
            "Oemcomma" => ",",
            "OemPeriod" => ".",
            "OemQuestion" => "/",
            "Oem5" or "OemBackslash" => "\\",
            "Multiply" => "Num *",
            "Add" => "Num +",
            "Subtract" => "Num -",
            "Decimal" => "Num .",
            "Divide" => "Num /",
            _ => k
        };
    }
}

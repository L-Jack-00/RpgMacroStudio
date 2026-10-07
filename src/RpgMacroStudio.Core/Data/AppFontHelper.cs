using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;

namespace RpgMacroHarness.Data;

/// <summary>
/// 윈도우 환경 및 언어팩 차이로 인한 폰트/이모지 깨짐 현상을 방지하는 전역 안전 폰트 관리 헬퍼입니다.
/// </summary>
public static class AppFontHelper
{
    private static readonly Lazy<HashSet<string>> _installedFonts = new(() =>
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var ifc = new InstalledFontCollection();
            foreach (var family in ifc.Families)
            {
                set.Add(family.Name);
            }
        }
        catch
        {
            // 폰트 컬렉션 접근 실패 시 기본 동작 유지
        }
        return set;
    });

    public static bool IsFontInstalled(string name) => _installedFonts.Value.Contains(name);

    /// <summary>
    /// 일반 텍스트 및 UI 기본 폰트 (Segoe UI -> 맑은 고딕 -> Segoe UI Emoji -> Segoe UI Symbol 우선 고정)
    /// </summary>
    public static string DefaultFontFamilyName
    {
        get
        {
            if (IsFontInstalled("Segoe UI")) return "Segoe UI";
            if (IsFontInstalled("맑은 고딕")) return "맑은 고딕";
            if (IsFontInstalled("Segoe UI Emoji")) return "Segoe UI Emoji";
            if (IsFontInstalled("Segoe UI Symbol")) return "Segoe UI Symbol";
            if (IsFontInstalled("Tahoma")) return "Tahoma";
            if (IsFontInstalled("Arial")) return "Arial";
            return FontFamily.GenericSansSerif.Name;
        }
    }

    /// <summary>
    /// 이모지가 포함된 UI용 폰트 (Segoe UI -> Segoe UI Emoji -> Segoe UI Symbol -> 맑은 고딕 -> GenericSansSerif)
    /// Segoe UI 폰트를 기반으로 설정하면 일반 텍스트(한글/영문)는 선명하고 굵은 글씨(Bold)로 렌더링되고,
    /// 이모지는 Windows 시스템 FontLink를 통해 Segoe UI Emoji 서체의 본래 Regular 스타일로 자동 렌더링되어 뭉개짐 없이 완벽하게 조화됩니다.
    /// </summary>
    public static string EmojiFontFamilyName
    {
        get
        {
            if (IsFontInstalled("Segoe UI Emoji")) return "Segoe UI Emoji";
            if (IsFontInstalled("Segoe UI Symbol")) return "Segoe UI Symbol";
            if (IsFontInstalled("Segoe UI")) return "Segoe UI";
            if (IsFontInstalled("맑은 고딕")) return "맑은 고딕";
            return FontFamily.GenericSansSerif.Name;
        }
    }

    /// <summary>
    /// 심볼 기호(되돌리기/다시실행 화살표 등)용 폰트 (Segoe UI Symbol -> Segoe UI Emoji -> Segoe UI -> GenericSansSerif)
    /// </summary>
    public static string SymbolFontFamilyName
    {
        get
        {
            if (IsFontInstalled("Segoe UI Symbol")) return "Segoe UI Symbol";
            if (IsFontInstalled("Segoe UI Emoji")) return "Segoe UI Emoji";
            if (IsFontInstalled("Segoe UI")) return "Segoe UI";
            return FontFamily.GenericSansSerif.Name;
        }
    }

    /// <summary>
    /// 안전한 기본 UI 폰트를 생성합니다.
    /// </summary>
    public static Font GetDefaultFont(float size, FontStyle style = FontStyle.Regular)
    {
        try
        {
            return new Font(DefaultFontFamilyName, size, style);
        }
        catch
        {
            return new Font(FontFamily.GenericSansSerif, size, style);
        }
    }

    /// <summary>
    /// 이모지가 깨지지 않는 Segoe UI Emoji 기반 폰트를 생성합니다.
    /// </summary>
    public static Font GetEmojiFont(float size, FontStyle style = FontStyle.Regular)
    {
        try
        {
            string family = EmojiFontFamilyName;
            // Segoe UI Emoji는 시스템에 Bold 서체가 별도 존재하지 않으므로 GDI+ 폴백(굴림 치환/깨짐)을 방지하기 위해 Regular로 안전 생성
            FontStyle safeStyle = (family.Equals("Segoe UI Emoji", StringComparison.OrdinalIgnoreCase) && style.HasFlag(FontStyle.Bold))
                ? (style & ~FontStyle.Bold)
                : style;
            return new Font(family, size, safeStyle);
        }
        catch
        {
            return new Font(FontFamily.GenericSansSerif, size, style);
        }
    }

    /// <summary>
    /// 심볼/기호 전용 Segoe UI Symbol 기반 폰트를 생성합니다.
    /// </summary>
    public static Font GetSymbolFont(float size, FontStyle style = FontStyle.Regular)
    {
        try
        {
            return new Font(SymbolFontFamilyName, size, style);
        }
        catch
        {
            return new Font(FontFamily.GenericSansSerif, size, style);
        }
    }

    /// <summary>
    /// 문자열 조각의 텍스트와 이모지 여부를 나타내는 레코드입니다.
    /// </summary>
    public readonly record struct TextSegment(string Text, bool IsEmoji);

    /// <summary>
    /// 지정된 유니코드 코드포인트가 이모지(Emoji) 또는 특수 심볼/기호인지 여부를 안전하게 판별합니다.
    /// </summary>
    public static bool IsEmojiOrSymbol(int codePoint)
    {
        // 1. Supplementary Multilingual Plane (SMP) 이모지 및 픽토그램 (U+1F000 ~ U+1FAFF)
        // 📦, 🔀, 🔍, 📖, 🔄, 🗑, 🌙, 🌐, 🏁, 🖱, 🔢, 🤖, 🧠, 🎯, 🚀 등
        if (codePoint >= 0x1F000 && codePoint <= 0x1FAFF) return true;

        // 2. BMP 기호 및 심볼 주요 블록
        // 화살표 블록 (U+2190 ~ U+21FF: ←, →, ↶, ↷, ⇄ 등)
        if (codePoint >= 0x2190 && codePoint <= 0x21FF) return true;

        // 기타 기술 기호 (U+2300 ~ U+23FF: ⌨ U+2328, ⏳ U+23F3, ⏱, ⏰, ⌚ 등)
        if (codePoint >= 0x2300 && codePoint <= 0x23FF) return true;

        // 기하학적 도형 (U+25A0 ~ U+25FF: ■, □, ▲, ▼, ▶ U+25B6, ◀ U+25C0, ◈ U+25C8, ● U+25CF, ○ U+25CB, ◆, ◊ 등)
        if (codePoint >= 0x25A0 && codePoint <= 0x25FF) return true;

        // 기타 기호 및 딩뱃 (U+2600 ~ U+27BF: ⚡ U+26A1, ⚙ U+2699, ☀️, 🌙, ✨ U+2728, ✔ U+2714, ✖ U+2716, ➕ U+2795, ❌, ❓, ❗ 등)
        if (codePoint >= 0x2600 && codePoint <= 0x27BF) return true;

        // 보완 화살표 및 추가 기호 (U+2900 ~ U+2BFF: ⏹ U+23F9, ⏺, ⏸ 등)
        if (codePoint >= 0x2900 && codePoint <= 0x2BFF) return true;

        // 3. 유니코드 카테고리 기반 보완 판별 (단, ASCII 문자열 < 128 제외)
        if (codePoint >= 128 && codePoint <= 0xFFFF)
        {
            char c = (char)codePoint;
            var cat = char.GetUnicodeCategory(c);
            if (cat == System.Globalization.UnicodeCategory.OtherSymbol ||
                cat == System.Globalization.UnicodeCategory.ModifierSymbol)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 문자열의 특정 인덱스에서 시작하는 이모지/심볼 클러스터의 길이(Char 수)를 계산합니다.
    /// 이모지가 아니면 0을 반환합니다.
    /// </summary>
    public static int GetEmojiClusterLength(string text, int startIndex)
    {
        if (string.IsNullOrEmpty(text) || startIndex < 0 || startIndex >= text.Length) return 0;

        int i = startIndex;

        // 0. 국가 국기 이모지 (Regional Indicator Pair: 2개의 서로게이트 = 총 4 chars)
        if (i + 3 < text.Length &&
            char.IsHighSurrogate(text[i]) && char.IsLowSurrogate(text[i + 1]) &&
            char.IsHighSurrogate(text[i + 2]) && char.IsLowSurrogate(text[i + 3]))
        {
            int cp1 = char.ConvertToUtf32(text, i);
            int cp2 = char.ConvertToUtf32(text, i + 2);
            if (cp1 >= 0x1F1E6 && cp1 <= 0x1F1FF && cp2 >= 0x1F1E6 && cp2 <= 0x1F1FF)
            {
                return 4;
            }
        }

        int len;

        // 1. 단일 이모지 코드포인트 파싱
        if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
        {
            int cp = char.ConvertToUtf32(text, i);
            if (IsEmojiOrSymbol(cp))
            {
                len = 2;
                i += 2;
            }
            else
            {
                return 0;
            }
        }
        else
        {
            int cp = text[i];
            if (IsEmojiOrSymbol(cp))
            {
                len = 1;
                i += 1;
            }
            else
            {
                return 0;
            }
        }

        // 2. 후속 결합 문자 (Variation Selector, Fitzpatrick Modifier, ZWJ 체인) 흡수
        while (i < text.Length)
        {
            // Variation Selector (U+FE00 ~ U+FE0F)
            if (text[i] >= '\uFE00' && text[i] <= '\uFE0F')
            {
                len++;
                i++;
                continue;
            }

            // Skin Tone Modifier (서로게이트 페어 U+1F3FB ~ U+1F3FF)
            if (i + 1 < text.Length && char.IsHighSurrogate(text[i]) && char.IsLowSurrogate(text[i + 1]))
            {
                int modCp = char.ConvertToUtf32(text, i);
                if (modCp >= 0x1F3FB && modCp <= 0x1F3FF)
                {
                    len += 2;
                    i += 2;
                    continue;
                }
            }

            // ZWJ (Zero-Width Joiner \u200D) + 후속 이모지
            if (text[i] == '\u200D' && i + 1 < text.Length)
            {
                int nextClusterLen = GetEmojiClusterLength(text, i + 1);
                if (nextClusterLen > 0)
                {
                    len += 1 + nextClusterLen;
                    i += 1 + nextClusterLen;
                    continue;
                }
            }

            break;
        }

        return len;
    }

    /// <summary>
    /// 문자열에서 앞부분의 이모지/심볼 아이콘 토큰과 뒷부분의 일반 텍스트를 분리합니다.
    /// 복합 이모지(Variation Selector, ZWJ, BMP 특수기호 ◈, ⌨️, 🖱️ 등)를 100% 안전하게 처리합니다.
    /// </summary>
    public static (string iconPart, string textPart) SplitEmojiAndText(string fullText)
    {
        if (string.IsNullOrEmpty(fullText)) return ("", "");

        int clusterLen = GetEmojiClusterLength(fullText, 0);
        if (clusterLen > 0)
        {
            string icon = fullText.Substring(0, clusterLen);
            string rest = (clusterLen < fullText.Length) ? fullText.Substring(clusterLen).TrimStart() : "";
            return (icon, rest);
        }

        return ("", fullText);
    }

    /// <summary>
    /// 임의의 문자열을 이모지/심볼 조각과 일반 텍스트 조각들의 연속 시퀀스로 분할합니다.
    /// 문자열의 앞/뒤/중간 어디에 이모지가 위치하더라도 정확하게 분리합니다.
    /// </summary>
    public static List<TextSegment> TokenizeEmojiAndText(string fullText)
    {
        var result = new List<TextSegment>();
        if (string.IsNullOrEmpty(fullText)) return result;

        int i = 0;
        int textStart = 0;

        while (i < fullText.Length)
        {
            int clusterLen = GetEmojiClusterLength(fullText, i);
            if (clusterLen > 0)
            {
                // 앞서 누적된 일반 텍스트가 있다면 세그먼트로 추가
                if (i > textStart)
                {
                    result.Add(new TextSegment(fullText.Substring(textStart, i - textStart), false));
                }

                // 이모지 세그먼트 추가
                result.Add(new TextSegment(fullText.Substring(i, clusterLen), true));
                i += clusterLen;
                textStart = i;
            }
            else
            {
                i++;
            }
        }

        // 끝에 남은 일반 텍스트 추가
        if (textStart < fullText.Length)
        {
            result.Add(new TextSegment(fullText.Substring(textStart), false));
        }

        return result;
    }

    /// <summary>
    /// 이모지/심볼 기호는 Segoe UI Emoji(Regular)로 글리프 깨짐 없이 그리고,
    /// 본문 텍스트는 Segoe UI(Bold/Regular)로 굵고 선명하게 한 줄에 합성하여 렌더링합니다.
    /// 접두사(Prefix), 접미사(Suffix), 중간 이모지 모두 완벽하게 분리되어 왜곡 없이 렌더링됩니다.
    /// </summary>
    public static void DrawMixedString(
        Graphics g,
        string fullText,
        float size,
        Brush brush,
        RectangleF bounds,
        StringAlignment alignX = StringAlignment.Near,
        StringAlignment alignY = StringAlignment.Center,
        FontStyle textStyle = FontStyle.Bold)
    {
        if (string.IsNullOrEmpty(fullText) || bounds.Width <= 0 || bounds.Height <= 0) return;

        var segments = TokenizeEmojiAndText(fullText);
        if (segments.Count == 0) return;

        using var iconFont = GetEmojiFont(size, FontStyle.Regular);
        using var textFont = GetDefaultFont(size, textStyle);
        using var sfTypo = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces
        };

        // 1. 단일 비-이모지 텍스트 고속 패스
        if (segments.Count == 1 && !segments[0].IsEmoji)
        {
            using var sfDirect = new StringFormat
            {
                LineAlignment = alignY,
                Alignment = alignX,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            };
            g.DrawString(segments[0].Text, textFont, brush, bounds, sfDirect);
            return;
        }

        // 2. 각 세그먼트의 너비 측정 및 전체 너비 합산
        var widths = new float[segments.Count];
        float totalWidth = 0f;

        for (int idx = 0; idx < segments.Count; idx++)
        {
            var seg = segments[idx];
            var f = seg.IsEmoji ? iconFont : textFont;
            var sz = g.MeasureString(seg.Text, f, PointF.Empty, sfTypo);
            // 이모지 심볼은 시스템 렌더링 시 살짝 여유 너비 부여
            float w = seg.IsEmoji ? Math.Max(size + 2f, sz.Width) : sz.Width;
            widths[idx] = w;
            totalWidth += w;
        }

        // 3. 정렬에 따른 시작 X 좌표
        float curX = alignX switch
        {
            StringAlignment.Center => bounds.X + Math.Max(0, (bounds.Width - totalWidth) / 2f),
            StringAlignment.Far => bounds.Right - totalWidth,
            _ => bounds.X
        };

        using var sfSeg = new StringFormat
        {
            LineAlignment = alignY,
            Alignment = StringAlignment.Near,
            FormatFlags = StringFormatFlags.NoWrap
        };

        // 4. 각 세그먼트 연속 렌더링
        for (int idx = 0; idx < segments.Count; idx++)
        {
            var seg = segments[idx];
            var f = seg.IsEmoji ? iconFont : textFont;
            float w = widths[idx];

            float renderW = Math.Max(w + 4f, bounds.Right - curX);
            var segRect = new RectangleF(curX, bounds.Y, renderW, bounds.Height);

            g.DrawString(seg.Text, f, brush, segRect, sfSeg);
            curX += w;

            if (curX >= bounds.Right) break;
        }
    }
}

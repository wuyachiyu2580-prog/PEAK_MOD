using System;
using System.Collections.Generic;
using System.Text;

namespace PeakWorkshopCNScanner
{
    internal sealed class Finding
    {
        internal string Kind;
        internal string Evidence;
    }

    internal sealed class TextClassifier
    {
        private readonly IDictionary<string, string> _literals;
        private readonly HashSet<string> _ignore;
        private static readonly HashSet<string> Technical = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PEAK", "Workshop", "PeakWorkshop", "PeakMapForge", "BepInEx", "Steam", "Discord",
            "NPC", "NPCs", "FPS", "CPU", "GPU", "RAM", "MB", "GB", "KB", "Hz", "kHz", "ms",
            "px", "cm", "mm", "km", "kg", "m", "s", "x", "y", "z", "ID", "GUID", "UUID",
            "RGB", "RGBA", "HEX", "UV", "UI", "HUD", "TMP", "URL", "PNG", "JPG", "JPEG", "JSON",
            "EN", "DE", "CN", "ZH", "Ctrl", "Strg", "Shift", "Alt", "Tab", "Esc", "Escape",
            "WASD", "LMB", "RMB", "MMB"
        };

        internal TextClassifier(IDictionary<string, string> literals, IEnumerable<string> ignore = null)
        {
            _literals = literals;
            _ignore = new HashSet<string>(ignore ?? new string[0], StringComparer.Ordinal);
        }

        internal Finding Classify(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || _ignore.Contains(raw)) return null;
            string text = PlainText(raw).Trim();
            if (text.Length == 0 || _ignore.Contains(text)) return null;
            if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("\\\\", StringComparison.Ordinal) ||
                (text.Length > 2 && text[1] == ':' && (text[2] == '/' || text[2] == '\\'))) return null;
            bool remainingWord = false;
            for (int i = 0; i < text.Length;)
            {
                if (!Latin(text[i])) { i++; continue; }
                int start = i++;
                while (i < text.Length && (Latin(text[i]) || char.IsDigit(text[i]))) i++;
                string word = text.Substring(start, i - start);
                if (word.Length < 2 || Technical.Contains(word) || FunctionKey(word)) continue;
                // Hex IDs and scientific numeric readouts are machine data.
                if (IsHex(word) && word.Length >= 8) continue;
                remainingWord = true;
                break;
            }
            if (!remainingWord) return null;
            if (_literals.TryGetValue(raw, out var origin) || _literals.TryGetValue(text, out origin))
                return new Finding { Kind = "builtin-candidate", Evidence = "Exact original Loc.Tr literal: " + origin + "; author text with the same spelling remains possible." };
            if (LooksLikeResourceKey(text))
                return new Finding { Kind = "resource-candidate", Evidence = "Resource-like spelling only; official/author ownership is not established." };
            return new Finding { Kind = "review", Evidence = "Remaining Latin word(s); may be a dynamic label, resource display name, author content or a proper noun." };
        }

        internal static bool LooksLikeResourceKey(string text)
        {
            if (text.IndexOf('_') < 1 || text.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0) return false;
            return text.StartsWith("M_", StringComparison.Ordinal) || text.StartsWith("SM_", StringComparison.Ordinal) ||
                   text.StartsWith("T_", StringComparison.Ordinal) || text.IndexOf("_Rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("_Tree", StringComparison.OrdinalIgnoreCase) >= 0 || char.IsDigit(text[text.Length - 1]);
        }

        private static bool Latin(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '\u00c0' && c <= '\u024f');
        private static bool FunctionKey(string word) => (word[0] == 'F' || word[0] == 'f') &&
            int.TryParse(word.Substring(1), out var n) && n >= 1 && n <= 24;
        private static bool IsHex(string word)
        {
            foreach (char c in word) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        internal static string PlainText(string raw)
        {
            if (raw.IndexOf('<') < 0) return raw;
            var result = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == '<')
                {
                    int end = raw.IndexOf('>', i + 1);
                    if (end < 0) { result.Append(raw, i, raw.Length - i); break; }
                    if (end >= 0)
                    {
                        string tag = raw.Substring(i + 1, end - i - 1).TrimStart('/');
                        int sep = tag.IndexOfAny(new[] { '=', ' ' });
                        string name = sep < 0 ? tag : tag.Substring(0, sep);
                        switch (name.ToLowerInvariant())
                        {
                            case "b": case "i": case "u": case "s": case "color": case "size":
                            case "material": case "quad": case "sprite": case "font": case "alpha":
                            case "align": case "voffset": case "cspace": case "mspace": case "link":
                                i = end;
                                continue;
                        }
                        // Unknown markup is literal. Consume the span once to keep malformed input linear.
                        result.Append(raw, i, end - i + 1);
                        i = end;
                        continue;
                    }
                }
                result.Append(raw[i]);
            }
            return result.ToString();
        }
    }
}

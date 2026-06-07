using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WhySoLaggy
{
    /// <summary>
    /// 极简 JSON 解析器（1.0.3 FieldProbe 配置专用）。
    /// - 仅支持解析（不做序列化）；
    /// - 返回类型：null / bool / long / double / string / List&lt;object&gt; / Dictionary&lt;string,object&gt;。
    /// - 零依赖，避免耦合游戏自带 Newtonsoft.Json 版本。
    /// - 容错：遇格式错误抛 <see cref="FormatException"/> 并带上字符偏移。
    /// </summary>
    internal static class MiniJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int pos = 0;
            SkipWs(json, ref pos);
            var v = ParseValue(json, ref pos);
            SkipWs(json, ref pos);
            if (pos != json.Length)
                throw new FormatException($"MiniJson: unexpected trailing chars at {pos}");
            return v;
        }

        public static bool TryParse(string json, out object value, out string error)
        {
            try { value = Parse(json); error = null; return true; }
            catch (Exception ex) { value = null; error = ex.Message; return false; }
        }

        // ── Getter 辅助 ──
        public static Dictionary<string, object> AsObject(object v)
            => v as Dictionary<string, object>;
        public static List<object> AsArray(object v)
            => v as List<object>;

        public static string GetString(Dictionary<string, object> obj, string key, string defVal = null)
        {
            if (obj == null || !obj.TryGetValue(key, out var v) || v == null) return defVal;
            return v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static bool GetBool(Dictionary<string, object> obj, string key, bool defVal)
        {
            if (obj == null || !obj.TryGetValue(key, out var v) || v == null) return defVal;
            if (v is bool b) return b;
            if (v is string s) return bool.TryParse(s, out var r) ? r : defVal;
            return defVal;
        }

        public static int GetInt(Dictionary<string, object> obj, string key, int defVal)
        {
            if (obj == null || !obj.TryGetValue(key, out var v) || v == null) return defVal;
            if (v is long l) return (int)l;
            if (v is double d) return (int)d;
            if (v is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)) return r;
            return defVal;
        }

        // ── 解析核心 ──
        private static object ParseValue(string s, ref int pos)
        {
            SkipWs(s, ref pos);
            if (pos >= s.Length) throw new FormatException($"MiniJson: unexpected EOF at {pos}");
            char c = s[pos];
            if (c == '{') return ParseObject(s, ref pos);
            if (c == '[') return ParseArray(s, ref pos);
            if (c == '"') return ParseString(s, ref pos);
            if (c == 't' || c == 'f') return ParseBool(s, ref pos);
            if (c == 'n') return ParseNull(s, ref pos);
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(s, ref pos);
            throw new FormatException($"MiniJson: unexpected '{c}' at {pos}");
        }

        private static Dictionary<string, object> ParseObject(string s, ref int pos)
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            pos++; // '{'
            SkipWs(s, ref pos);
            if (pos < s.Length && s[pos] == '}') { pos++; return dict; }
            while (true)
            {
                SkipWs(s, ref pos);
                if (pos >= s.Length || s[pos] != '"')
                    throw new FormatException($"MiniJson: expected string key at {pos}");
                string key = ParseString(s, ref pos);
                SkipWs(s, ref pos);
                if (pos >= s.Length || s[pos] != ':')
                    throw new FormatException($"MiniJson: expected ':' at {pos}");
                pos++;
                var v = ParseValue(s, ref pos);
                dict[key] = v;
                SkipWs(s, ref pos);
                if (pos >= s.Length) throw new FormatException("MiniJson: unexpected EOF in object");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == '}') { pos++; return dict; }
                throw new FormatException($"MiniJson: expected ',' or '}}' at {pos}");
            }
        }

        private static List<object> ParseArray(string s, ref int pos)
        {
            var list = new List<object>();
            pos++; // '['
            SkipWs(s, ref pos);
            if (pos < s.Length && s[pos] == ']') { pos++; return list; }
            while (true)
            {
                var v = ParseValue(s, ref pos);
                list.Add(v);
                SkipWs(s, ref pos);
                if (pos >= s.Length) throw new FormatException("MiniJson: unexpected EOF in array");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == ']') { pos++; return list; }
                throw new FormatException($"MiniJson: expected ',' or ']' at {pos}");
            }
        }

        private static string ParseString(string s, ref int pos)
        {
            if (s[pos] != '"') throw new FormatException($"MiniJson: expected '\"' at {pos}");
            pos++;
            var sb = new StringBuilder();
            while (pos < s.Length)
            {
                char c = s[pos++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    if (pos >= s.Length) throw new FormatException("MiniJson: bad escape at EOF");
                    char esc = s[pos++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (pos + 4 > s.Length) throw new FormatException("MiniJson: bad \\u at EOF");
                            string hex = s.Substring(pos, 4);
                            pos += 4;
                            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                                throw new FormatException($"MiniJson: bad \\u hex '{hex}'");
                            sb.Append((char)code);
                            break;
                        default:
                            throw new FormatException($"MiniJson: unknown escape '\\{esc}' at {pos - 1}");
                    }
                }
                else sb.Append(c);
            }
            throw new FormatException("MiniJson: unterminated string");
        }

        private static object ParseNumber(string s, ref int pos)
        {
            int start = pos;
            if (s[pos] == '-') pos++;
            bool isFloat = false;
            while (pos < s.Length)
            {
                char c = s[pos];
                if (c >= '0' && c <= '9') { pos++; continue; }
                if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
                {
                    isFloat = true; pos++; continue;
                }
                break;
            }
            string num = s.Substring(start, pos - start);
            if (isFloat)
            {
                if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            }
            else
            {
                if (long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
            }
            throw new FormatException($"MiniJson: bad number '{num}' at {start}");
        }

        private static bool ParseBool(string s, ref int pos)
        {
            if (pos + 4 <= s.Length && s.Substring(pos, 4) == "true") { pos += 4; return true; }
            if (pos + 5 <= s.Length && s.Substring(pos, 5) == "false") { pos += 5; return false; }
            throw new FormatException($"MiniJson: bad bool at {pos}");
        }

        private static object ParseNull(string s, ref int pos)
        {
            if (pos + 4 <= s.Length && s.Substring(pos, 4) == "null") { pos += 4; return null; }
            throw new FormatException($"MiniJson: bad null at {pos}");
        }

        private static void SkipWs(string s, ref int pos)
        {
            while (pos < s.Length)
            {
                char c = s[pos];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { pos++; continue; }
                // 行注释 "// ..."（非 JSON 标准，但 FieldProbe 配置允许）
                if (c == '/' && pos + 1 < s.Length && s[pos + 1] == '/')
                {
                    pos += 2;
                    while (pos < s.Length && s[pos] != '\n') pos++;
                    continue;
                }
                break;
            }
        }
    }
}

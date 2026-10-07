using System;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace PeakWorkshopCNScanner
{
    public sealed partial class Plugin
    {
        private GUIStyle _numberStyle, _statusStyle;
        private Font _statusFont;
        private readonly GUIContent _numberContent = new GUIContent();
        private readonly GUIContent _statusContent = new GUIContent();
        private float _nextStatusRefresh;

        internal void Observe(Rect rect, GUIContent content)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint || content == null ||
                string.IsNullOrEmpty(content.text) || rect.width <= 0 || rect.height <= 0 || GUI.color.a <= 0.01f) return;
            long started = Stopwatch.GetTimestamp();
            try
            {
                if (!_scopes.TryGetValue(ScopeState.Current, out var scope)) return;
                if (_visibleRect != null && !rect.Overlaps(_visibleRect())) return;
                _draws++;
                _visited.Add(scope);
                long analyzeStarted = Stopwatch.GetTimestamp();
                bool analyze = _sample && _sampleTicks < Stopwatch.Frequency / ScanTickBudgetDivisor;
                var record = _store.Observe(scope, content.text, analyze);
                if (record != null && analyze)
                {
                    _store.Touch(record, _frame, _utc, _scene, _chineseState, rect.x, rect.y, rect.width, rect.height);
                    if (!record.HasStack && _stackBudget > 0)
                    {
                        _stackBudget--;
                        _stacks++;
                        record.DrawStack = CaptureStack();
                        record.HasStack = true;
                    }
                }
                if (_sample) _sampleTicks += Stopwatch.GetTimestamp() - analyzeStarted;
                if (record != null && _markers.Value && _markerBudget > 0)
                {
                    _markerBudget--;
                    Mark(rect, record);
                }
            }
            catch (Exception ex) { Fail(ex); }
            finally { _observerTicks += Stopwatch.GetTimestamp() - started; }
        }

        private string CaptureStack()
        {
            var result = new StringBuilder();
            int count = 0;
            // Only first evidence for a record, <=4 per sampled frame. Never capture file paths or argument values.
            foreach (var frame in new StackTrace(false).GetFrames() ?? new StackFrame[0])
            {
                var method = frame.GetMethod();
                if (method == null) continue;
                string type = method.DeclaringType?.FullName ?? "";
                if (method.DeclaringType?.Assembly != _workshop && type.IndexOf("PeakMapForge", StringComparison.Ordinal) < 0 &&
                    method.Name.IndexOf("PeakMapForge", StringComparison.Ordinal) < 0) continue;
                if (count++ > 0) result.Append('\n');
                result.Append(type).Append('.').Append(method.Name).Append(" @IL ").Append(frame.GetILOffset());
                if (count == 6) break;
            }
            return count == 0 ? "Runtime stack has no original methods (inlined/detoured); use the recorded OnGUI scope." : result.ToString();
        }

        private void Mark(Rect rect, ScanRecord record)
        {
            Color saved = GUI.color;
            DrawingMarker = true;
            try
            {
                var color = record.Kind == "builtin-candidate" ? new Color(1f, 0.48f, 0.15f, 0.9f) :
                    record.Kind == "resource-candidate" ? new Color(0.25f, 0.82f, 1f, 0.9f) : new Color(1f, 0.85f, 0.3f, 0.85f);
                GUI.color = color;
                Texture texture = Texture2D.whiteTexture;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), texture);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), texture);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), texture);
                GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), texture);
                if (rect.width < 28f || rect.height < 12f) return;
                if (_numberStyle == null)
                {
                    _numberStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.UpperRight,
                        padding = new RectOffset(0, 0, 0, 0), clipping = TextClipping.Clip, wordWrap = false };
                    _numberStyle.normal.textColor = Color.black;
                    _numberStyle.normal.background = null;
                }
                var badge = new Rect(Mathf.Max(rect.x, rect.xMax - 38f), rect.y, Mathf.Min(rect.width, 38f), 12f);
                GUI.DrawTexture(badge, texture);
                GUI.color = Color.white;
                _numberContent.text = Badge(record.Id);
                _numberStyle.Draw(badge, _numberContent, false, false, false, false);
            }
            finally { GUI.color = saved; DrawingMarker = false; }
        }

        // Bounded by MaximumRecords, not dynamic text or numbers from authors.
        private readonly System.Collections.Generic.Dictionary<int, string> _badges = new System.Collections.Generic.Dictionary<int, string>();
        private string Badge(int id)
        {
            if (!_badges.TryGetValue(id, out string text)) { text = "#" + id; _badges.Add(id, text); }
            return text;
        }

        private void OnGUI()
        {
            if (_showStatus == null || !_showStatus.Value || Event.current.type != EventType.Repaint) return;
            Color savedColor = GUI.color;
            Matrix4x4 savedMatrix = GUI.matrix;
            int savedDepth = GUI.depth;
            DrawingMarker = true;
            try
            {
                if (_statusStyle == null)
                {
                    _statusFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 13);
                    _statusStyle = new GUIStyle(GUI.skin.label) { font = _statusFont, fontSize = 13, richText = false,
                        padding = new RectOffset(8, 8, 5, 5), wordWrap = false };
                    _statusStyle.normal.textColor = Color.white;
                }
                if (Time.realtimeSinceStartup >= _nextStatusRefresh)
                {
                    _nextStatusRefresh = Time.realtimeSinceStartup + 0.5f;
                    string status = _failure != null ? "出错已停用（见日志）" : Scanning ? "扫描中" : "已暂停";
                    _statusContent.text = "漏译扫描：" + status + " · 疑似 " + (_store?.Count ?? 0) +
                        (_store != null && _store.LimitReached ? " · 已到容量上限" : "") +
                        "\nCtrl+F12 暂停  |  +Shift 导出  |  +Alt 标注";
                }
                GUI.depth = -32000;
                GUI.matrix = Matrix4x4.identity;
                var box = new Rect(8, Screen.height - 60, Mathf.Min(580, Screen.width - 16), 52);
                GUI.color = new Color(0.04f, 0.06f, 0.09f, 0.9f);
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                GUI.color = Color.white;
                _statusStyle.Draw(box, _statusContent, false, false, false, false);
            }
            catch (Exception ex) { _showStatus.Value = false; Fail(ex); }
            finally { GUI.color = savedColor; GUI.matrix = savedMatrix; GUI.depth = savedDepth; DrawingMarker = false; }
        }
    }
}

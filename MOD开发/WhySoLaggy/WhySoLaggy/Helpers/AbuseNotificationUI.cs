using System.Collections.Generic;
using UnityEngine;

namespace WhySoLaggy
{
    /// <summary>
    /// 炸房告警屏幕通知 UI（左上角半透明叠层）。
    /// 使用 Unity IMGUI（OnGUI）实现，无需额外 DLL 引用。
    /// 仅用于告知房主/玩家发生了异常事件，不做任何防御。
    /// </summary>
    internal static class AbuseNotificationUI
    {
        // ── 配置 ──
        private const float DisplayDuration = 12f;    // 单条通知显示秒数
        private const int MaxVisibleMessages = 8;     // 同时显示上限
        private const float BoxWidth = 520f;
        private const float LineHeight = 22f;
        private const float Padding = 8f;
        private const float TopMargin = 10f;
        private const float LeftMargin = 10f;

        // ── 通知队列 ──
        private struct Notification
        {
            public string Message;
            public float ExpireTime;
        }

        private static readonly List<Notification> _notifications = new List<Notification>();
        private static GUIStyle _boxStyle;
        private static GUIStyle _textStyle;

        /// <summary>
        /// 显示一条通知（自动过期）。
        /// </summary>
        public static void Show(string message)
        {
            _notifications.Add(new Notification
            {
                Message = message,
                ExpireTime = Time.unscaledTime + DisplayDuration
            });

            // 超过上限移除最旧的
            while (_notifications.Count > MaxVisibleMessages)
                _notifications.RemoveAt(0);
        }

        /// <summary>
        /// 由 Plugin.OnGUI 调用，负责渲染当前通知。
        /// </summary>
        public static void DrawGUI()
        {
            // 清除过期通知
            float now = Time.unscaledTime;
            _notifications.RemoveAll(n => now >= n.ExpireTime);

            if (_notifications.Count == 0) return;

            EnsureStyles();

            float totalHeight = _notifications.Count * LineHeight + Padding * 2f;
            Rect boxRect = new Rect(LeftMargin, TopMargin, BoxWidth, totalHeight);

            GUI.Box(boxRect, GUIContent.none, _boxStyle);

            float y = TopMargin + Padding;
            foreach (var notif in _notifications)
            {
                // 最后 3 秒淡出
                float remaining = notif.ExpireTime - now;
                float alpha = remaining < 3f ? remaining / 3f : 1f;
                Color c = _textStyle.normal.textColor;
                c.a = alpha;
                _textStyle.normal.textColor = c;

                Rect textRect = new Rect(LeftMargin + Padding, y, BoxWidth - Padding * 2f, LineHeight);
                GUI.Label(textRect, notif.Message, _textStyle);
                y += LineHeight;
            }
        }

        private static void EnsureStyles()
        {
            if (_boxStyle != null) return;

            // 半透明黑色背景
            Texture2D bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.75f));
            bgTex.Apply();

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = bgTex;
            _boxStyle.border = new RectOffset(0, 0, 0, 0);

            // 红色警告文字
            _textStyle = new GUIStyle(GUI.skin.label);
            _textStyle.fontSize = 14;
            _textStyle.normal.textColor = new Color(1f, 0.35f, 0.3f, 1f);
            _textStyle.fontStyle = FontStyle.Bold;
            _textStyle.wordWrap = true;
        }
    }
}

using TMPro;
using UnityEngine;

namespace PlayersInfo.Helpers
{
    /// <summary>
    /// 拿游戏自带含中文字形的 TMP_FontAsset，避免 AddComponent&lt;TextMeshProUGUI&gt;
    /// 默认走 TMP_Settings.defaultFontAsset（只有 ASCII）导致中文变豆腐块。
    ///
    /// 兜底顺序：
    ///   1) GUIManager 下 AscentUI.text.font —— 原版主 UI 中文文本组件
    ///   2) GUIManager.heroDayText.font     —— "第 N 天"字样，含完整中文
    ///   3) 全场 FindAnyObjectByType&lt;TextMeshProUGUI&gt; 抓一个
    ///   4) TMP_Settings.defaultFontAsset   —— 最后兜底，宁肯没中文也别返 null
    ///
    /// 为什么不返回 null：返 null 会让 TMP fallback 到系统字体，描边会失效且字变粗。
    /// </summary>
    internal static class FontHelper
    {
        private static TMP_FontAsset _cached;

        public static TMP_FontAsset GetChineseCapable()
        {
            if (_cached != null) return _cached;
            try
            {
                if (GUIManager.instance != null)
                {
                    var ascent = GUIManager.instance.GetComponentInChildren<AscentUI>(true);
                    if (ascent != null && ascent.text != null && ascent.text.font != null)
                    {
                        _cached = ascent.text.font;
                        return _cached;
                    }
                    if (GUIManager.instance.heroDayText != null && GUIManager.instance.heroDayText.font != null)
                    {
                        _cached = GUIManager.instance.heroDayText.font;
                        return _cached;
                    }
                }

                var anyTmp = Object.FindAnyObjectByType<TextMeshProUGUI>();
                if (anyTmp != null && anyTmp.font != null)
                {
                    _cached = anyTmp.font;
                    return _cached;
                }

                try
                {
                    var def = TMP_Settings.defaultFontAsset;
                    if (def != null)
                    {
                        _cached = def;
                        return _cached;
                    }
                }
                catch { }
            }
            catch { }
            return null;
        }

        /// <summary>场景重载等需要重新取字体时调用。</summary>
        public static void InvalidateCache()
        {
            _cached = null;
        }
    }
}

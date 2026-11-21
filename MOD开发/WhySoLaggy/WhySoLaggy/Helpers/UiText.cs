using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
namespace WhySoLaggy
{
    internal static class UiText
    {
        private static FieldInfo language;
        private static double next;
        private static string current="English";
        internal static string Language
        {
            get{if(DiagnosticClock.Now>=next){next=DiagnosticClock.Now+.25;try{language=language??AccessTools.TypeByName("LocalizedText")?.GetField("CURRENT_LANGUAGE",BindingFlags.Public|BindingFlags.Static);current=language?.GetValue(null)?.ToString()??"English";}catch{}}return current;}
        }
        internal static string Get(string english,string chinese)=>Language=="TraditionalChinese"?Traditional(chinese):Language=="SimplifiedChinese"?chinese:english;
        internal static string LocalizeChinese(string value)=>Language=="TraditionalChinese"?Traditional(value):value;
        internal static string Traditional(string value)
        {
            if(value==null)return null;
            // Generated and length-checked from Windows zh-TW mapping.
            const string simplified="与专业丢两严个为举义仅从优会传体储关内册写冲决况净准减击则刚创别务动区单却压参双发变叠台号听启员响围场坏块声处备复头夺实审对导将层属带帧帮库应开异弃强归当录径态总戏户执扫抛抢护报拥拦挂挤换据掷数断无旧时显暂机权条来极构标栈样档检残毁汇没测涨满滤滥点热状独环现监盖盘码础确种积签简类红约级纯线组细终经绑结绕给络绝统继续维缓编网联节获补装见规视览触计订认让记许设访证识诊译话该详语误请读谁调负责败质贴费赖踪转轮软载较辅辑输达过运还这进远连适选递逻释针钩链销锁错键长闭问间闻阅阈队险随隐静项顺须顿预频题额风验骤髅齐龙";
            const string traditional="與專業丟兩嚴個為舉義僅從優會傳體儲關內冊寫沖決況凈準減擊則剛創別務動區單卻壓參雙發變疊臺號聽啟員響圍場壞塊聲處備復頭奪實審對導將層屬帶幀幫庫應開異棄強歸當錄徑態總戲戶執掃拋搶護報擁攔掛擠換據擲數斷無舊時顯暫機權條來極構標棧樣檔檢殘毀匯沒測漲滿濾濫點熱狀獨環現監蓋盤碼礎確種積簽簡類紅約級純線組細終經綁結繞給絡絕統繼續維緩編網聯節獲補裝見規視覽觸計訂認讓記許設訪證識診譯話該詳語誤請讀誰調負責敗質貼費賴蹤轉輪軟載較輔輯輸達過運還這進遠連適選遞邏釋針鉤鏈銷鎖錯鍵長閉問間聞閱閾隊險隨隱靜項順須頓預頻題額風驗驟髏齊龍";
            var b=new StringBuilder(value.Length);foreach(char c in value){int i=simplified.IndexOf(c);b.Append(i>=0&&i<traditional.Length?traditional[i]:c);}return b.ToString().Replace("錄制","錄製").Replace("采集","採集").Replace("采樣","採樣").Replace("字節","位元組").Replace("內存","記憶體").Replace("范圍","範圍");
        }
        internal static void Invalidate(){next=0;}
        internal static string State(string state)
        {
            switch(state){case "Active":return Get("Active","已启用");case "Partial":return Get("Partial","部分覆盖");case "Off":return Get("Off","已关闭");case "Standby":return Get("Standby","待命");case "Plugins":return Get("Plugin recording","插件采集");case "Targeted":return Get("Targeted recording","定向采集");case "Manual":return Get("Manual recording","手动采集");case "Saving":return Get("Saving","保存中");case "Cooldown":return Get("Cooldown","冷却");case "Paused":return Get("Paused","已暂停");case "Fault":return Get("Fault","故障");case "Loading":return Get("Loading","加载中");case "Unfocused":return Get("Unfocused","失去焦点");case "Transition":return Get("Hook transition","补丁切换");case "Gameplay":return Get("Gameplay","游玩");case "Airport":return Get("Airport","机场");case "Menu":return Get("Menu","菜单");default:return state;}
        }
        internal static string Runtime(string text)
        {
            if(string.IsNullOrEmpty(text))return "";
            var items=new Dictionary<string,string>{{"Applied; trace/probe rule changes require Reload rules","已应用；追踪和探针规则需点击重新加载规则"},{"Rules reloaded","规则已重新加载"},{"Rule validation failed; existing hooks retained","规则验证失败；保留现有补丁"},{"Exporting","正在导出"},{"Export already running","导出正在进行"},{"Writer queue full","写入队列已满"},{"Automatic targets removed: self-overhead budget exceeded","自动定向采集已停止：自身开销超过预算"},{"No verified target; retaining plugin, network and error evidence","未发现可信定向目标，继续保留插件、网络和异常证据"},{"Completed","已完成"},{"User stopped","用户停止"},{"Session changed","会话已变化"},{"Paused by user","用户暂停"}};
            items["Automatic expansion stopped after a hook failure or long installation"]="自动深入已停止：补丁失败或单次安装耗时过长";
            items["Shutdown incomplete; background writes pending"]="退出刷盘未完成，仍有后台写入";
            if(items.TryGetValue(text,out string cn))return Get(text,cn);
            if(text.EndsWith("pre-trigger methods may not have been instrumented",StringComparison.Ordinal))return Get(text,"已开始录制；触发前未安装计时的方法无法补录");
            foreach(var p in new[]{Tuple.Create("Flush failed:","写盘失败："),Tuple.Create("Export failed:","导出失败："),Tuple.Create("Apply failed:","应用失败："),Tuple.Create("Rule reload failed:","规则加载失败：")})if(text.StartsWith(p.Item1,StringComparison.Ordinal))return Get(p.Item1,p.Item2)+text.Substring(p.Item1.Length);
            return text;
        }
    }
}

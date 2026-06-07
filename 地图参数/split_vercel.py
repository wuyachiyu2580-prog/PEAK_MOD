#!/usr/bin/env python3
"""将单体HTML拆分为Vercel可部署的碎片化版本"""

import re, os, shutil

HTML_FILE = r"c:\Users\Administrator\Desktop\MOD\PEAK\地图参数\PEAK地图参数手册.html"
OUT_DIR = r"c:\Users\Administrator\Desktop\MOD\PEAK\地图参数\vercel-deploy"
SEGMENTS_DIR = os.path.join(OUT_DIR, "segments")

SEG_IDS = ["Beach_Segment", "Jungle_Segment", "Roots_Segment",
           "Snow_Segment", "Desert_Segment", "Caldera_Segment", "Volcano_Segment"]
SEG_FILES = {
    "Beach_Segment": "beach", "Jungle_Segment": "jungle",
    "Roots_Segment": "roots", "Snow_Segment": "snow",
    "Desert_Segment": "desert", "Caldera_Segment": "caldera",
    "Volcano_Segment": "volcano",
}

def extract_css(html):
    m = re.search(r'<style>(.*?)</style>', html, re.DOTALL)
    return m.group(1).strip(), m.span()

def extract_js(html):
    m = re.search(r'<script>(.*?)</script>', html, re.DOTALL)
    return m.group(1).strip(), m.span()

def extract_segments(html):
    """按tab-content边界拆出每个关卡的内部HTML"""
    ca_start = html.find('<div class="content" id="contentArea">')
    ca_end_html = html.find('<button class="back-to-top"', ca_start)
    ca_section = html[ca_start:ca_end_html]

    # 按 <div class="tab-content" 切分
    marker = '<div class="tab-content"'
    parts = ca_section.split(marker)
    # parts[0] = '<div class="content" id="contentArea">'
    # parts[1..N] = ' id="tab-XXX">...content... '
    #   末尾多余的空白/换行/</div> 要裁剪

    segments = {}
    for part in parts[1:]:
        # part 以 ' id="tab-Beach_Segment">' 开头
        id_m = re.match(r'\s+id="tab-([^"]+)">', part)
        if not id_m:
            continue
        seg_id = id_m.group(1)
        inner = part[id_m.end():]

        # 去除末尾一个 </div> 和空白
        inner = inner.rstrip()
        if inner.endswith('</div>'):
            inner = inner[:-6].rstrip()
        segments[seg_id] = inner
    return segments

def modify_js(js_body):
    """将 showTab() 改为 fetch 动态加载，其余JS不变"""
    # 在 JS 开头插入 segment → 文件名映射 + 缓存
    seg_map_js = "const SEG_FILES={" + ",".join(
        f'"{k}":"{v}"' for k, v in SEG_FILES.items()) + "};\n"
    cache_js = "const _loadedSegments={};\n"
    prefix = seg_map_js + cache_js

    new_show_tab = '''function showTab(id){
document.querySelectorAll('.tab-btn').forEach(b=>b.classList.remove('active'));
document.querySelectorAll('.tab-btn').forEach(b=>{if(b.textContent.includes(id.replace(/_/g,' '))||b.textContent.includes(id.replace(/_/g,'')))b.classList.add('active');});
document.querySelectorAll('.tab-content').forEach(t=>t.classList.remove('active'));
const c=document.getElementById('tab-'+id);
if(!c)return;
c.classList.add('active');currentTab=id;currentSegId=id;
if(!_loadedSegments[id]){
c.innerHTML='<div class="loading" style="text-align:center;padding:60px;color:#888;">⏳ 加载中…</div>';
const segFile=SEG_FILES[id];
if(!segFile){c.innerHTML='<div class="loading">未知关卡: '+id+'</div>';return;}
fetch('segments/'+segFile+'.html').then(r=>{if(!r.ok)throw new Error(r.status);return r.text();}).then(html=>{
c.innerHTML=html;
_loadedSegments[id]=true;
applyCurrentSettings();
if(lastQuery)doSearch();
}).catch(err=>{c.innerHTML='<div class="loading" style="color:#e94560;">❌ 加载失败: '+err.message+'</div>';});
}else{
applyCurrentSettings();
if(lastQuery)doSearch();
}
updateBackToTop();
}'''

    old_pattern = r'function showTab\(id\)\{.*?\n\}'
    modified = re.sub(old_pattern, new_show_tab, js_body, count=1, flags=re.DOTALL)
    return prefix + modified

def build_index(html, css_span, js_span):
    """用切分-拼接方式重建index.html：前段+空tab+后段"""
    # 关键位置
    ca_start_end = html.find('<div class="content" id="contentArea">') + len('<div class="content" id="contentArea">')
    ca_end = html.find('<button class="back-to-top"', ca_start_end)

    # 前段：从文件头到 content 区起始标签（含）
    pre_part = html[:ca_start_end]
    # 中段（原 tab-content 内容区，将被替换）
    # 后段：从 back-to-top 按钮到文件尾
    post_part = html[ca_end:]

    # 在前段中：把 <style>...</style> 换为外部引用
    style_open = css_span[0]
    style_close = css_span[1]
    pre_part = pre_part[:style_open] + '<link rel="stylesheet" href="style.css">' + pre_part[style_close:]

    # 在后段中：把 <script>...</script> 换为外部引用
    # 后段中 script 的偏移需要重新计算
    script_start_in_post = post_part.find('<script>')
    script_end_in_post = post_part.find('</script>', script_start_in_post) + len('</script>')
    post_part = post_part[:script_start_in_post] + '<script src="script.js"></script>' + post_part[script_end_in_post:]

    # 空 tab-content 占位
    tabs_html = ''.join(f'<div class="tab-content" id="tab-{sid}"></div>' for sid in SEG_IDS)

    # 拼接：前段 + 空标签 + </div>（关 content 区）+ 后段
    index = pre_part + tabs_html + '</div>' + post_part
    return index

def main():
    os.makedirs(SEGMENTS_DIR, exist_ok=True)

    with open(HTML_FILE, 'r', encoding='utf-8') as f:
        html = f.read()

    print("拆分 PEAK地图参数手册.html → vercel-deploy/")
    print("=" * 50)

    # 提取 CSS
    css_body, css_span = extract_css(html)
    css_path = os.path.join(OUT_DIR, "style.css")
    with open(css_path, 'w', encoding='utf-8') as f:
        f.write(css_body)
    print(f"  ✓ style.css ({len(css_body)} 字符)")

    # 提取 JS
    js_body, js_span = extract_js(html)
    js_modified = modify_js(js_body)
    js_path = os.path.join(OUT_DIR, "script.js")
    with open(js_path, 'w', encoding='utf-8') as f:
        f.write(js_modified)
    print(f"  ✓ script.js ({len(js_modified)} 字符)")

    # 提取关卡片段
    segments = extract_segments(html)
    for seg_id in SEG_IDS:
        if seg_id in segments:
            seg_file = SEG_FILES[seg_id]
            seg_path = os.path.join(SEGMENTS_DIR, f"{seg_file}.html")
            with open(seg_path, 'w', encoding='utf-8') as f:
                f.write(segments[seg_id])
            print(f"  ✓ segments/{seg_file}.html ({len(segments[seg_id])} 字符)")
        else:
            print(f"  ⚠ 未找到 {seg_id}")

    # 生成 index.html 主壳
    index_html = build_index(html, css_span, js_span)
    index_path = os.path.join(OUT_DIR, "index.html")
    with open(index_path, 'w', encoding='utf-8') as f:
        f.write(index_html)
    print(f"  ✓ index.html ({len(index_html)} 字符)")

    # 复制完整单体文件
    full_path = os.path.join(OUT_DIR, "PEAK地图参数手册.html")
    shutil.copy2(HTML_FILE, full_path)
    print(f"  ✓ PEAK地图参数手册.html (完整单体，{os.path.getsize(full_path)/1024:.0f} KB)")

    print("=" * 50)
    print("完成! 输出目录:", OUT_DIR)

if __name__ == "__main__":
    main()

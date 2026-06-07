const SEG_FILES={"Beach_Segment":"beach","Jungle_Segment":"jungle","Roots_Segment":"roots","Snow_Segment":"snow","Desert_Segment":"desert","Caldera_Segment":"caldera","Volcano_Segment":"volcano"};
const _loadedSegments={};
let currentTab=null,lastQuery='',currentSegId=null;
let compareList=[];
let paramSettings={};

function showTab(id){
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
}

function clearSearch(){
document.getElementById('searchInput').value='';lastQuery='';
clearHighlights();
document.getElementById('searchInfo').textContent='';
document.getElementById('noResults').style.display='none';
}

function clearHighlights(){
document.querySelectorAll('.highlight').forEach(el=>el.classList.remove('highlight'));
}

function toggleAll(segId,expand){
const tc=document.getElementById('tab-'+segId);
if(!tc)return;
tc.querySelectorAll('.variant-detail,.step-detail').forEach(d=>{d.open=expand;});
}

function toggleMuteRows(segId){
const tc=document.getElementById('tab-'+segId);
if(!tc)return;
const btn=document.getElementById('btnMute_'+segId);
const isShowing=tc.classList.contains('show-mute');
if(isShowing){tc.classList.remove('show-mute');if(btn){btn.classList.remove('mute-visible');btn.innerHTML='🔇 显示静音参数';}}
else{tc.classList.add('show-mute');if(btn){btn.classList.add('mute-visible');btn.innerHTML='🔊 隐藏静音参数';}}
}

function doSearch(){
const q=document.getElementById('searchInput').value.trim();
lastQuery=q;clearHighlights();
document.getElementById('noResults').style.display='none';
document.getElementById('searchInfo').textContent='';
if(!q)return;
const ac=document.querySelector('.tab-content.active');
if(!ac)return;
const lq=q.toLowerCase();let fc=0;
ac.querySelectorAll('.variant-detail,.step-detail').forEach(d=>{if(!d.open)d.open=true;});
const fm={el:null,top:Infinity};
ac.querySelectorAll('tr,h4,summary,.seg-desc').forEach(row=>{
if(row.textContent.toLowerCase().includes(lq)){row.classList.add('highlight');fc++;
const r=row.getBoundingClientRect();if(r.top<fm.top&&r.top>0){fm.el=row;fm.top=r.top;}}
});
ac.querySelectorAll('.prop-val,.prop-name,.vn,.special').forEach(el=>{
if(el.textContent.toLowerCase().includes(lq)){el.classList.add('highlight');
const row=el.closest('tr');if(row&&!row.classList.contains('highlight')){row.classList.add('highlight');fc++;}}
});
if(fc===0){document.getElementById('noResults').style.display='block';document.getElementById('searchInfo').textContent='0 个结果';}
else{document.getElementById('searchInfo').textContent='找到 '+fc+' 个结果';if(fm.el)fm.el.scrollIntoView({behavior:'smooth',block:'center'});}
}

function scrollToTop(){window.scrollTo({top:0,behavior:'smooth'});}
function updateBackToTop(){
const btn=document.getElementById('backToTop');
if(!btn)return;
btn.classList.toggle('show',window.scrollY>400);
}
window.addEventListener('scroll',updateBackToTop,{passive:true});

// ====== 对比功能 ======
function addToCompare(btn){
const stepDetail=btn.closest('.step-detail');
if(!stepDetail)return;
const table=stepDetail.querySelector('.prop-table');
if(!table)return;
const summary=stepDetail.querySelector('summary');
const label=summary.querySelector('.step-label')?.textContent.trim()||'未知';
const variant=stepDetail.closest('.variant-detail')?.querySelector('summary strong')?.textContent.trim()||'';
const idx=compareList.findIndex(c=>c.btn===btn);
if(idx>=0){compareList.splice(idx,1);btn.classList.remove('added');}
else{
if(compareList.length>=2){const old=compareList.shift();if(old.btn)old.btn.classList.remove('added');}
compareList.push({btn,label,variant,tableHtml:table.outerHTML});
btn.classList.add('added');
}
updateCmpIndicator();
if(compareList.length===2)renderCompare();
}

function updateCmpIndicator(){
const ind=document.getElementById('cmpIndicator');
const n=compareList.length;
ind.textContent='⊕ 对比 ('+n+'/2)';
ind.classList.toggle('show',n>0);
}

function showComparePanel(){
document.getElementById('comparePanel').classList.add('show');
const btt=document.getElementById('backToTop');
if(btt)btt.style.bottom='50vh';
if(compareList.length>=1)renderCompare();
}

function closeComparePanel(){
document.getElementById('comparePanel').classList.remove('show');
const btt=document.getElementById('backToTop');
if(btt)btt.style.bottom='';
}

function renderCompare(){
const body=document.getElementById('compareBody');
if(compareList.length===0){body.innerHTML='<div style="color:#888;text-align:center;padding:20px;">点击步骤旁的 ⊕ 按钮添加对比项</div>';return;}
if(compareList.length===1){
body.innerHTML='<div class="compare-col"><h4>'+escapeHtml(compareList[0].label)+' <small style="color:#888">('+escapeHtml(compareList[0].variant)+')</small></h4>'+compareList[0].tableHtml+'</div><div class="compare-col" style="color:#888;text-align:center;padding:40px;">再选一个步骤即可对比</div>';
return;
}
const [a,b]=compareList;
const rowsA=parseRows(a.tableHtml);
const rowsB=parseRows(b.tableHtml);
const allKeys=new Set([...Object.keys(rowsA),...Object.keys(rowsB)]);
let html='';
html+='<div class="compare-col"><h4>'+escapeHtml(a.label)+' <small style="color:#888">('+escapeHtml(a.variant)+')</small></h4><table>';
for(const k of allKeys){
const va=rowsA[k],vb=rowsB[k];
const diff=va!==undefined&&vb!==undefined&&va!==vb;
html+='<tr'+(diff?' class="diff"':'')+'><td class="pn">'+escapeHtml(k)+'</td><td class="pv">'+(va!==undefined?va:'—')+'</td></tr>';
}
html+='</table></div>';
html+='<div class="compare-col"><h4>'+escapeHtml(b.label)+' <small style="color:#888">('+escapeHtml(b.variant)+')</small></h4><table>';
for(const k of allKeys){
const va=rowsA[k],vb=rowsB[k];
const diff=va!==undefined&&vb!==undefined&&va!==vb;
html+='<tr'+(diff?' class="diff"':'')+'><td class="pn">'+escapeHtml(k)+'</td><td class="pv">'+(vb!==undefined?vb:'—')+'</td></tr>';
}
html+='</table></div>';
body.innerHTML=html;
document.getElementById('comparePanel').classList.add('show');
}

function parseRows(tableHtml){
const div=document.createElement('div');div.innerHTML=tableHtml;
const rows={};
div.querySelectorAll('tr').forEach(tr=>{
const nameEl=tr.querySelector('.prop-name');
const valEl=tr.querySelector('.prop-val');
if(nameEl&&valEl)rows[nameEl.textContent.trim()]=valEl.textContent.trim();
});
return rows;
}

function clearCompare(){
compareList.forEach(c=>{if(c.btn)c.btn.classList.remove('added');});
compareList=[];
updateCmpIndicator();
closeComparePanel();
}

// ====== 参数设置功能 ======
function openSettings(segId){
currentSegId=segId;
document.getElementById('settingsOverlay').classList.add('show');
const cbs=document.querySelectorAll('#paramGrid input[type=checkbox]');
const hasSettings=Object.keys(paramSettings).length>0;
if(!hasSettings){
const core=['area','nrOfSpawns','chanceToUseSpawner','layerType'];
cbs.forEach(cb=>{cb.checked=core.includes(cb.value);});
}else{
cbs.forEach(cb=>{if(paramSettings.hasOwnProperty(cb.value))cb.checked=paramSettings[cb.value];});
}
document.querySelectorAll('.presets button').forEach(b=>b.classList.remove('active'));
const arr=Array.from(cbs);
if(arr.every(cb=>cb.checked))document.querySelector('.presets button:nth-child(2)').classList.add('active');
else if(arr.every(cb=>!cb.checked))document.querySelector('.presets button:nth-child(3)').classList.add('active');
else document.querySelector('.presets button:nth-child(1)').classList.add('active');
}

function closeSettings(){document.getElementById('settingsOverlay').classList.remove('show');}

function settingsPreset(type){
const cbs=document.querySelectorAll('#paramGrid input[type=checkbox]');
document.querySelectorAll('.presets button').forEach(b=>b.classList.remove('active'));
if(type==='core'){
const core=['area','nrOfSpawns','chanceToUseSpawner','layerType'];
cbs.forEach(cb=>{cb.checked=core.includes(cb.value);});
document.querySelector('.presets button:nth-child(1)').classList.add('active');
}else if(type==='all'){
cbs.forEach(cb=>{cb.checked=true;});
document.querySelector('.presets button:nth-child(2)').classList.add('active');
}else{
cbs.forEach(cb=>{cb.checked=false;});
document.querySelector('.presets button:nth-child(3)').classList.add('active');
}
}

function applySettings(){
const cbs=document.querySelectorAll('#paramGrid input[type=checkbox]');
paramSettings={};
cbs.forEach(cb=>{paramSettings[cb.value]=cb.checked;});
applyCurrentSettings();
closeSettings();
}

function applyCurrentSettings(){
if(!currentSegId)return;
const tc=document.getElementById('tab-'+currentSegId);
if(!tc)return;
const keys=Object.keys(paramSettings);
if(keys.length===0){tc.classList.remove('is-streamlined');return;}
const allOn=keys.every(k=>paramSettings[k]);
if(allOn){tc.classList.remove('is-streamlined');return;}
tc.classList.add('is-streamlined');
tc.querySelectorAll('tr[data-param]').forEach(tr=>{
const pk=tr.getAttribute('data-param');
if(paramSettings[pk])tr.classList.add('param-visible');
else tr.classList.remove('param-visible');
});
}

function escapeHtml(s){return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');}

function jumpStep(e, anchor){
e.preventDefault();
const el=document.getElementById(anchor);
if(!el)return;
let p=el.parentElement;
while(p){
if(p.tagName==='DETAILS')p.open=true;
p=p.parentElement;
}
el.scrollIntoView({behavior:'smooth',block:'center'});
el.style.background='rgba(233,69,96,0.12)';
setTimeout(()=>el.style.background='',1800);
window.location.hash=anchor;
}

document.addEventListener('DOMContentLoaded',()=>{
const fb=document.querySelector('.tab-btn');
if(fb)fb.click();
updateBackToTop();
});

document.addEventListener('keydown',(e)=>{
if((e.ctrlKey||e.metaKey)&&e.key==='f'){e.preventDefault();document.getElementById('searchInput').focus();document.getElementById('searchInput').select();}
if(e.key==='Escape'){closeSettings();closeComparePanel();}
});
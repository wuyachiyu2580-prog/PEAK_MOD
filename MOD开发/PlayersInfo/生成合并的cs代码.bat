@echo off
chcp 65001 >nul
cd /d "%~dp0"

if exist "合并输出.txt" del "合并输出.txt"

for /r %%f in (*.cs) do (
    echo // ======== 文件: %%f ======== >> "合并输出.txt"
    type "%%f" >> "合并输出.txt"
    echo. >> "合并输出.txt"
)

echo 合并完成！输出文件：合并输出.txt
pause
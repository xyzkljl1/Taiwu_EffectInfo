//go.bat $(SolutionName) $(TargetPath)
set TAIWUDIR=G:\\Steam\\steamapps\\common\\The Scroll Of Taiwu
set Project=%1%
set Plugin=%2%
::echo start
::echo %1%
::echo %2%
xcopy /y "..\\config.lua" "%TAIWUDIR%\\Mod\\%Project%\\"
::xcopy /y "..\\settings.lua" "%TAIWUDIR%\\Mod\\%Project%\\"
xcopy /y "%Plugin%" "%TAIWUDIR%\\Mod\\%Project%\\Plugins\\"
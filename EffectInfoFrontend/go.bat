//go.bat $(SolutionName) $(TargetPath)
set TAIWUDIR=G:\\Steam\\steamapps\\workshop\\content\\838350\\2871662978
set Project=%1%
set Plugin=%2%
::echo start
::echo %1%
::echo %2%
xcopy /y "..\\config.lua" "%TAIWUDIR%\\"
::xcopy /y "..\\settings.lua" "%TAIWUDIR%\\Mod\\%Project%\\"
xcopy /y "%Plugin%" "%TAIWUDIR%\\Plugins\\"
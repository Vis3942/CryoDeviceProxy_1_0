set custdir=publish_release
set appdir=CryoDeviceProxy
set publishdir=%appdir%\bin\Release\netcoreapp3.1\linux-x64\publish\
echo "Cleaning publish directory %appdir%\%custdir%\"
del /F /Q %appdir%\%custdir%\
del /F /Q %publishdir%

timout /T 5 > NUL
echo "Publishing to directory %publishdir%"
REM dotnet publish -c release -f netcoreapp3.1 -r linux-arm -o %destdir% --self-contained=false
dotnet publish -c release -f netcoreapp3.1 -r linux-x64 -o %publishdir% --self-contained=false
copy %appdir%\VersionInfo.txt %publishdir%

tar -a -c -f %appdir%\%custdir%\CryoDeviceProxy.zip -C %publishdir% *.*



REM dotnet publish -c release -r linux-x64 -f netcoreapp3.1 --self-contained=false
REM copy CryoDeviceProxy\VersionInfo.txt CryoDeviceProxy\bin\Release\netcoreapp3.1\linux-x64\publish\

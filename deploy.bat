dotnet publish -c release -r linux-x64 -f netcoreapp3.1 --self-contained=false
copy CryoDeviceProxy\VersionInfo.txt CryoDeviceProxy\bin\Release\netcoreapp3.1\linux-x64\publish\

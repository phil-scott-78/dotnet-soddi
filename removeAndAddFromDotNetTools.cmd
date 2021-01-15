dotnet tool uninstall dotnet-soddi -g
dotnet build -c Release
dotnet tool install --global --add-source .\src\Soddi\nupkg\ dotnet-soddi
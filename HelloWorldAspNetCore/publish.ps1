$scriptDir = Split-Path $script:MyInvocation.MyCommand.Path

dotnet publish $scriptDir\HelloWorldAspNetCore.csproj --runtime linux-arm --configuration Release --self-contained false -p:PublishSingleFile=true
dotnet publish $scriptDir\HelloWorldAspNetCore.csproj --runtime linux-arm64 --configuration Release --self-contained false -p:PublishSingleFile=true
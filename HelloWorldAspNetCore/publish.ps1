$scriptDir = Split-Path $script:MyInvocation.MyCommand.Path

dotnet publish $scriptDir\HelloWorldAspNetCore.csproj --configuration Release --runtime linux-arm --self-contained false -p:PublishSingleFile=true
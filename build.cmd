
set version=%1
set key=%2

cd %~dp0

dotnet build magic.endpoint/magic.endpoint.contracts/magic.endpoint.contracts.csproj --configuration Release --source https://api.nuget.org/v3/index.json
dotnet nuget push magic.endpoint/magic.endpoint.contracts/bin/Release/magic.endpoint.contracts.%version%.nupkg -k %key% -s https://api.nuget.org/v3/index.json

dotnet build magic.endpoint/magic.endpoint.services/magic.endpoint.services.csproj --configuration Release --source https://api.nuget.org/v3/index.json
dotnet nuget push magic.endpoint/magic.endpoint.services/bin/Release/magic.endpoint.services.%version%.nupkg -k %key% -s https://api.nuget.org/v3/index.json

dotnet build magic.endpoint/magic.endpoint.controller/magic.endpoint.controller.csproj --configuration Release --source https://api.nuget.org/v3/index.json
dotnet nuget push magic.endpoint/magic.endpoint.controller/bin/Release/magic.endpoint.%version%.nupkg -k %key% -s https://api.nuget.org/v3/index.json


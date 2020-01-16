cd %~dp0
dotnet build magic.endpoint/magic.endpoint.contracts/magic.endpoint.contracts.csproj --configuration Release --source https://api.nuget.org/v3/index.json
dotnet build magic.endpoint/magic.endpoint.services/magic.endpoint.services.csproj --configuration Release --source https://api.nuget.org/v3/index.json
dotnet build magic.endpoint/magic.endpoint.controller/magic.endpoint.controller.csproj --configuration Release --source https://api.nuget.org/v3/index.json

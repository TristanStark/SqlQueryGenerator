$ErrorActionPreference = "Stop"
dotnet restore .\SqlQueryGenerator.sln
dotnet build .\SqlQueryGenerator.sln -c Release

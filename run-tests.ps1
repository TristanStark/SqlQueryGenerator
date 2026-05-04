$ErrorActionPreference = "Stop"
dotnet test .\SqlQueryGenerator.sln -c Release --logger "console;verbosity=detailed"

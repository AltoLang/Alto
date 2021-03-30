@echo off

cd Alto.Tests
dotnet build 
cd ..
dotnet test .\Alto.Tests\Alto.Tests.csproj
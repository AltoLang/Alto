@echo off

cd Accel.Tests
dotnet build 
cd ..
dotnet test .\Accel.Tests\Accel.Tests.csproj
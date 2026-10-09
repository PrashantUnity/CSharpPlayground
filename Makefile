.DEFAULT_GOAL := run

run:
	dotnet run --project Runner/CSharpEditorPlugin.Runner.csproj

run-release:
	dotnet run --project Runner/CSharpEditorPlugin.Runner.csproj -c Release

build:
	dotnet build CSharpEditorPlugin.slnx

test:
ifeq ($(time),1)
	TIME=1 dotnet test Tests/CSharpEditorPlugin.Tests.csproj -c Release
else
	dotnet test Tests/CSharpEditorPlugin.Tests.csproj -c Release
endif

test-time:
	TIME=1 dotnet test Tests/CSharpEditorPlugin.Tests.csproj -c Release

.PHONY: run run-release build test test-time

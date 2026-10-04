.DEFAULT_GOAL := run

run:
	dotnet run --project Runner/CSharpEditorPlugin.Runner.csproj

run-release:
	dotnet run --project Runner/CSharpEditorPlugin.Runner.csproj -c Release

build:
	dotnet build CSharpEditorPlugin.slnx

test:
	dotnet test Tests/CSharpEditorPlugin.Tests.csproj -c Release

.PHONY: run run-release build test

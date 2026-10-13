.DEFAULT_GOAL := run

run:
	dotnet run --project Runner/CSharpEditorPlugin.Runner.csproj

run-release:
	dotnet run --project Runner/CSharpEditorPlugin.Runner.csproj -c Release

# The headless sidecar that powers the VS Code extension over stdio JSON-RPC.
sidecar:
	dotnet run --project FrySharp.Sidecar -- $(ARGS)

cli:
	dotnet run --project FrySharp.Cli -- $(ARGS)

build:
	dotnet build CSharpEditorPlugin.slnx

ext-install:
	cd vscode-frysharp && npm install

ext-build:
	cd vscode-frysharp && npm run compile

ext-package:
	cd vscode-frysharp && npx @vscode/vsce package

test:
ifeq ($(time),1)
	TIME=1 dotnet test Tests/CSharpEditorPlugin.Tests.csproj -c Release
else
	dotnet test Tests/CSharpEditorPlugin.Tests.csproj -c Release
endif

test-time:
	TIME=1 dotnet test Tests/CSharpEditorPlugin.Tests.csproj -c Release

# Headless core tests (Roslyn script runner, kernels, notebook file formats, storage).
test-core:
	dotnet test Tests.Core/FrySharp.Core.Tests.csproj

# Headless JSON-RPC sidecar tests (stdio protocol, notebooks, script execution, diagnostics).
test-sidecar:
	dotnet test Tests.Sidecar/FrySharp.Sidecar.Tests.csproj

test-all: test test-core test-sidecar

# FrySharp.Core's build fails if an Avalonia package reaches it (its AssertNoAvalonia target).
verify-core:
	dotnet build FrySharp.Core

clean:
	dotnet clean CSharpEditorPlugin.slnx
	rm -rf vscode-frysharp/out vscode-frysharp/*.vsix

.PHONY: run run-release sidecar cli build ext-install ext-build ext-package test test-time test-core test-sidecar test-all verify-core clean

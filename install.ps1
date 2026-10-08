<#
.SYNOPSIS
    Automated environment and polyglot toolchains setup script for C# Code Studio (FrySharp).

.DESCRIPTION
    Installs, configures, and verifies all supported language toolchains and runtimes:
      - .NET 10 SDK (C#, F#, FrySharp runtime)
      - Python 3.13 + pip
      - Node.js LTS + npm (JavaScript)
      - Microsoft OpenJDK 21 (Java javac / java)
      - LLVM Clang (C / C++ clang / clang++)
      - Go (Golang go)
      - Rust (Rustup / Cargo / rustc)
      - SQLite 3 (SQL sqlite3 CLI)

    Also updates the current PowerShell session PATH, builds the FrySharp runner project,
    and optionally launches the application.

.PARAMETER Run
    Launches FrySharp (C# Code Studio) after installation and build.

.PARAMETER SkipBuild
    Skips compiling the FrySharp project.

.PARAMETER SkipLanguages
    Installs only the .NET SDK without the external polyglot toolchains.

.EXAMPLE
    .\install.ps1
    .\install.ps1 -Run
#>
[CmdletBinding()]
param(
    [switch]$Run,
    [switch]$SkipBuild,
    [switch]$SkipLanguages
)

$ErrorActionPreference = "Continue"
$ScriptRoot = $PSScriptRoot
if (-not $ScriptRoot) { $ScriptRoot = (Get-Location).Path }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   C# Code Studio (FrySharp) Toolchains & Environment Setup" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Architecture : $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)" -ForegroundColor Gray
Write-Host "Root Path    : $ScriptRoot" -ForegroundColor Gray
Write-Host ""

function Update-SessionEnvironment {
    $machinePath = [System.Environment]::GetEnvironmentVariable("Path", "Machine")
    $userPath    = [System.Environment]::GetEnvironmentVariable("Path", "User")
    $env:PATH    = "$machinePath;$userPath"

    foreach ($scope in @("Machine", "User")) {
        foreach ($var in @("JAVA_HOME", "DOTNET_ROOT", "CARGO_HOME", "RUSTUP_HOME", "GOROOT", "GOPATH", "SQLITE3_PATH")) {
            $val = [System.Environment]::GetEnvironmentVariable($var, $scope)
            if ($val) { Set-Item "env:$var" $val }
        }
    }

    # Ensure standard installation directories are reachable immediately
    $candidateDirs = @(
        "$env:USERPROFILE\.dotnet",
        "C:\Program Files\dotnet",
        "C:\Program Files (x86)\dotnet",
        "$env:LOCALAPPDATA\Programs\LLVM\LLVM\bin",
        "C:\Program Files\LLVM\bin",
        "$env:LOCALAPPDATA\Programs\Go\bin",
        "$env:USERPROFILE\go\bin",
        "C:\Program Files\Go\bin",
        "C:\Program Files\SQLite",
        "C:\sqlite",
        "$env:USERPROFILE\.cargo\bin",
        "$env:LOCALAPPDATA\Programs\Python\Python313-arm64",
        "$env:LOCALAPPDATA\Programs\Python\Python313-arm64\Scripts",
        "$env:LOCALAPPDATA\Programs\Python\Python313",
        "$env:LOCALAPPDATA\Programs\Python\Python313\Scripts",
        "$env:LOCALAPPDATA\Programs\Python\Python312",
        "$env:LOCALAPPDATA\Programs\Python\Python312\Scripts",
        "C:\Program Files\nodejs"
    )

    # Detect Java Home if missing
    if (-not $env:JAVA_HOME) {
        $jdkDirs = @(
            "$env:LOCALAPPDATA\Programs\Microsoft\jdk-21*",
            "C:\Program Files\Microsoft\jdk-21*",
            "C:\Program Files\Eclipse Adoptium\jdk-21*"
        ) | ForEach-Object { Get-ChildItem $_ -ErrorAction SilentlyContinue } | Select-Object -First 1
        if ($jdkDirs) {
            $env:JAVA_HOME = $jdkDirs.FullName
            $candidateDirs += "$($jdkDirs.FullName)\bin"
        }
    } else {
        $candidateDirs += "$env:JAVA_HOME\bin"
    }

    foreach ($dir in $candidateDirs) {
        if ((Test-Path $dir) -and ($env:PATH -split ';' -notcontains $dir)) {
            $env:PATH = "$dir;$env:PATH"
        }
    }
}

function Add-UserPath {
    param([string]$Directory)
    if (-not (Test-Path $Directory)) { return }
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    if ($userPath -split ';' -notcontains $Directory) {
        [Environment]::SetEnvironmentVariable("Path", "$Directory;$userPath", "User")
    }
    if ($env:PATH -split ';' -notcontains $Directory) {
        $env:PATH = "$Directory;$env:PATH"
    }
}

function Ensure-Tool {
    param(
        [string]$Name,
        [scriptblock]$CheckBlock,
        [scriptblock]$InstallBlock
    )

    Write-Host "[*] Checking $Name... " -NoNewline
    Update-SessionEnvironment

    $isInstalled = $false
    try {
        $isInstalled = & $CheckBlock
    } catch {
        $isInstalled = $false
    }

    if ($isInstalled) {
        Write-Host "[INSTALLED]" -ForegroundColor Green
        return $true
    }

    Write-Host "[MISSING]" -ForegroundColor Yellow
    Write-Host "    Installing $Name..." -ForegroundColor Cyan

    try {
        & $InstallBlock
    } catch {
        Write-Host "    [ERROR] Installation script failed: $($_.Exception.Message)" -ForegroundColor Red
    }

    Update-SessionEnvironment
    $verified = $false
    try {
        $verified = & $CheckBlock
    } catch {
        $verified = $false
    }

    if ($verified) {
        Write-Host "    [OK] $Name installed and verified successfully." -ForegroundColor Green
        return $true
    } else {
        Write-Host "    [WARNING] $Name verification check failed." -ForegroundColor Yellow
        return $false
    }
}

# 1. Initialize PATH
Update-SessionEnvironment

# 2. Install .NET 10 SDK (Foundational for Studio and C# / F#)
Ensure-Tool `
    -Name ".NET 10 SDK (C# / F# / Studio Runtime)" `
    -CheckBlock {
        $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($cmd) {
            $ver = & dotnet --version 2>$null
            return ($ver -match "^10\.")
        }
        return $false
    } `
    -InstallBlock {
        $dotnetInstallScript = "$env:TEMP\dotnet-install.ps1"
        Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $dotnetInstallScript -UseBasicParsing
        $dotnetDir = "$env:USERPROFILE\.dotnet"
        & $dotnetInstallScript -Channel 10.0 -InstallDir $dotnetDir
        [Environment]::SetEnvironmentVariable("DOTNET_ROOT", $dotnetDir, "User")
        Add-UserPath $dotnetDir
    }

if (-not $SkipLanguages) {
    # 3. Python 3.13
    Ensure-Tool `
        -Name "Python 3.13 (Python Interactive & Scripts)" `
        -CheckBlock {
            $py = Get-Command python -ErrorAction SilentlyContinue
            if ($py -and $py.Source -notmatch "WindowsApps") {
                $ver = & python --version 2>$null
                return ($ver -match "Python 3\.(9|10|11|12|13|14)")
            }
            return $false
        } `
        -InstallBlock {
            winget install --id Python.Python.3.13 --scope user --silent --accept-source-agreements --accept-package-agreements --disable-interactivity
            $pyDir = Get-ChildItem "$env:LOCALAPPDATA\Programs\Python" -Directory -Filter "Python3*" -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($pyDir) {
                Add-UserPath $pyDir.FullName
                Add-UserPath "$($pyDir.FullName)\Scripts"
            }
        }

    # 4. Node.js LTS (JavaScript)
    Ensure-Tool `
        -Name "Node.js LTS (JavaScript / NPM)" `
        -CheckBlock {
            $node = Get-Command node -ErrorAction SilentlyContinue
            return ($node -ne $null)
        } `
        -InstallBlock {
            winget install --id OpenJS.NodeJS.LTS --scope user --silent --accept-source-agreements --accept-package-agreements --disable-interactivity
            Update-SessionEnvironment
        }

    # 5. Microsoft OpenJDK 21 (Java)
    Ensure-Tool `
        -Name "OpenJDK 21 (Java JDK / JShell)" `
        -CheckBlock {
            $javac = Get-Command javac -ErrorAction SilentlyContinue
            return ($javac -ne $null)
        } `
        -InstallBlock {
            $jdkBase = "$env:LOCALAPPDATA\Programs\Microsoft"
            New-Item -ItemType Directory -Path $jdkBase -Force | Out-Null
            $jdkZip = "$env:TEMP\microsoft-jdk21.zip"
            curl.exe -L "https://download.visualstudio.microsoft.com/download/pr/f1e5f23f-9d50-4b9f-8ed3-80522ae82bb5/4e646a00c68fa5d260ae8fed6e870d21/microsoft-jdk-21.0.12.1-windows-aarch64.zip" -o $jdkZip
            tar.exe -xf $jdkZip -C $jdkBase
            $installedJdk = Get-ChildItem $jdkBase -Directory -Filter "jdk-21*" | Select-Object -First 1
            if ($installedJdk) {
                [Environment]::SetEnvironmentVariable("JAVA_HOME", $installedJdk.FullName, "User")
                $env:JAVA_HOME = $installedJdk.FullName
                Add-UserPath "$($installedJdk.FullName)\bin"
            }
        }

    # 6. LLVM Clang (C / C++)
    Ensure-Tool `
        -Name "LLVM Clang (C / C++ Compiler)" `
        -CheckBlock {
            $clang = Get-Command clang++ -ErrorAction SilentlyContinue
            return ($clang -ne $null)
        } `
        -InstallBlock {
            $llvmBase = "$env:LOCALAPPDATA\Programs\LLVM"
            New-Item -ItemType Directory -Path $llvmBase -Force | Out-Null
            $llvmMsi = "$env:TEMP\llvm-woa64.msi"
            curl.exe -L "https://github.com/llvm/llvm-project/releases/download/llvmorg-23.1.3/LLVM-23.1.3-woa64.msi" -o $llvmMsi
            msiexec.exe /a $llvmMsi /qn TARGETDIR="$llvmBase"
            $clangExe = Get-ChildItem $llvmBase -Recurse -Filter "clang++.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($clangExe) {
                Add-UserPath $clangExe.DirectoryName
            }
        }

    # 7. Go (Golang)
    Ensure-Tool `
        -Name "Go Programming Language (Go)" `
        -CheckBlock {
            $go = Get-Command go -ErrorAction SilentlyContinue
            return ($go -ne $null)
        } `
        -InstallBlock {
            $goBase = "$env:LOCALAPPDATA\Programs"
            New-Item -ItemType Directory -Path $goBase -Force | Out-Null
            $goZip = "$env:TEMP\go-arm64.zip"
            curl.exe -L "https://go.dev/dl/go1.27.0.windows-arm64.zip" -o $goZip
            tar.exe -xf $goZip -C $goBase
            $goRoot = "$goBase\Go"
            [Environment]::SetEnvironmentVariable("GOROOT", $goRoot, "User")
            $env:GOROOT = $goRoot
            Add-UserPath "$goRoot\bin"
        }

    # 8. Rustup / Cargo (Rust)
    Ensure-Tool `
        -Name "Rust Toolchain (rustup / cargo / rustc)" `
        -CheckBlock {
            $cargo = Get-Command cargo -ErrorAction SilentlyContinue
            return ($cargo -ne $null)
        } `
        -InstallBlock {
            $rustupInit = "$env:TEMP\rustup-init.exe"
            curl.exe -L "https://static.rust-lang.org/rustup/dist/aarch64-pc-windows-msvc/rustup-init.exe" -o $rustupInit
            & $rustupInit -y
            Add-UserPath "$env:USERPROFILE\.cargo\bin"
        }

    # 9. SQLite 3 (SQL)
    Ensure-Tool `
        -Name "SQLite 3 CLI (SQL Database Tools)" `
        -CheckBlock {
            $sqlite = Get-Command sqlite3 -ErrorAction SilentlyContinue
            return ($sqlite -ne $null)
        } `
        -InstallBlock {
            winget install --id SQLite.SQLite --scope user --silent --accept-source-agreements --accept-package-agreements --disable-interactivity
            Update-SessionEnvironment
        }
}

# Final PATH refresh
Update-SessionEnvironment

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "              Toolchains Verification Summary             " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$tools = @(
    @{ Name = ".NET SDK"; Command = "dotnet"; Args = "--version" },
    @{ Name = "Python";   Command = "python"; Args = "--version" },
    @{ Name = "Node.js";  Command = "node";   Args = "--version" },
    @{ Name = "Java";     Command = "javac";  Args = "-version" },
    @{ Name = "Clang++";  Command = "clang++";Args = "--version" },
    @{ Name = "Go";       Command = "go";     Args = "version" },
    @{ Name = "Rust";     Command = "rustc";  Args = "--version" },
    @{ Name = "SQLite3";  Command = "sqlite3";Args = "--version" }
)

foreach ($t in $tools) {
    $cmd = Get-Command $t.Command -ErrorAction SilentlyContinue
    if ($cmd) {
        $rawOutput = & $t.Command $t.Args 2>&1 | Out-String
        $firstLine = ($rawOutput.Trim() -split "`n")[0]
        Write-Host ("{0,-12} : [OK] {1} ({2})" -f $t.Name, $firstLine, $cmd.Source) -ForegroundColor Green
    } else {
        Write-Host ("{0,-12} : [NOT FOUND]" -f $t.Name) -ForegroundColor Red
    }
}
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# Build project if requested
if (-not $SkipBuild) {
    Write-Host "[*] Building C# Code Studio (CSharpEditorPlugin.Runner)..." -ForegroundColor Cyan
    $projectPath = Join-Path $ScriptRoot "Runner/CSharpEditorPlugin.Runner.csproj"
    if (Test-Path $projectPath) {
        $buildProc = Start-Process -FilePath "dotnet" -ArgumentList @("build", $projectPath, "-c", "Debug") -Wait -PassThru -NoNewWindow
        if ($buildProc.ExitCode -eq 0) {
            Write-Host "[OK] Build succeeded!" -ForegroundColor Green
        } else {
            Write-Host "[FAIL] Build failed with exit code $($buildProc.ExitCode)." -ForegroundColor Red
            exit $buildProc.ExitCode
        }
    } else {
        Write-Host "[FAIL] Runner project not found at $projectPath" -ForegroundColor Red
    }
}

# Run application if requested
if ($Run) {
    Write-Host "[*] Launching C# Code Studio (FrySharp)..." -ForegroundColor Cyan
    $projectPath = Join-Path $ScriptRoot "Runner/CSharpEditorPlugin.Runner.csproj"
    Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", $projectPath)
    Write-Host "[OK] Application started." -ForegroundColor Green
}

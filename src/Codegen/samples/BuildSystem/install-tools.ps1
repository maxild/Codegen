#!/usr/bin/env pwsh

$SCRIPT_ROOT = split-path -parent $MyInvocation.MyCommand.Definition

$ARTIFACTS_DIR = Join-Path $SCRIPT_ROOT ".." | Join-Path -ChildPath ".." | Join-Path -ChildPath "artifacts"

function Resolve-Version() {
    $TOOLS_DIR = Join-Path $SCRIPT_ROOT ".." | Join-Path -ChildPath ".." | Join-Path -ChildPath "tools"
    $output = & "$TOOLS_DIR/dotnet-gitversion" /output json
    if ($LASTEXITCODE -ne 0) {
        if ($output -is [array]) {
            Write-Error ($output -join [System.Environment]::NewLine)
        }
        else {
            Write-Error $output
        }
        throw "GitVersion Exit Code: $LASTEXITCODE"
    }
    $versionInfoJson = $output -join "`n"
    $versionInfo = $versionInfoJson | ConvertFrom-Json

    return $versionInfo.NuGetVersion
}

$VERSION = Resolve-Version

# Only create the tool-manifest once
if (-not (Test-Path ".config")) {
    dotnet new tool-manifest
}

# Uninstall the tools
dotnet tool uninstall dotnet-cgdata
dotnet tool uninstall dotnet-cgcsharp

# Install the tools locally ('dotnet new tool-manifest' executed in curr dir)

# 'dotnet tool run cgdata' or 'dotnet cgdata' to invoke
dotnet tool install dotnet-cgdata --add-source $ARTIFACTS_DIR --version $VERSION
# 'dotnet tool run cgcsharp' or 'dotnet cgcsharp' to invoke
dotnet tool install dotnet-cgcsharp --add-source $ARTIFACTS_DIR --version $VERSION

# Test installation
dotnet cgdata --info
dotnet cgcsharp --info

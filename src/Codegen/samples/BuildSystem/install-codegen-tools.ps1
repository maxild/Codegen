#!/usr/bin/env pwsh

$SCRIPT_ROOT = split-path -parent $MyInvocation.MyCommand.Definition

$NUGET_DIR = Join-Path $SCRIPT_ROOT ".." | `
             Join-Path -ChildPath ".." | `
             Join-Path -ChildPath ".." | `
             Join-Path -ChildPath ".." | `
             Join-Path -ChildPath ".nuget"

# TODO: Should be removed soon...
function Test-Assertion {
  [CmdletBinding()]
  Param(
    [Parameter(Mandatory = $true, ValueFromPipeline = $true, Position = 0)]
    [AllowNull()]
    [AllowEmptyCollection()]
    [System.Object]
    $InputObject
  )

  Begin {
    $info = '{0}, file {1}, line {2}' -f @(
      $MyInvocation.Line.Trim(),
      $MyInvocation.ScriptName,
      $MyInvocation.ScriptLineNumber
    )
    $inputCount = 0
    $inputFromPipeline = -not $PSBoundParameters.ContainsKey('InputObject')
  }

  Process {
    $inputCount++
    if ($inputCount -gt 1) {
      $message = "Assertion failed (more than one object piped to Test-Assertion): $info"
      Write-Debug -Message $message
      throw $message
    }
    if ($null -eq $InputObject) {
      $message = "Assertion failed (`$InputObject is `$null): $info"
      Write-Debug -Message $message
      throw  $message
    }
    if ($InputObject -isnot [System.Boolean]) {
      $type = $InputObject.GetType().FullName
      $value = if ($InputObject -is [System.String]) { "'$InputObject'" } else { "{$InputObject}" }
      $message = "Assertion failed (`$InputObject is of type $type with value $value): $info"
      Write-Debug -Message $message
      throw $message
    }
    if (-not $InputObject) {
      $message = "Assertion failed (`$InputObject is `$false): $info"
      Write-Debug -Message $message
      throw $message
    }
    Write-Verbose -Message "Assertion passed: $info"
  }

  End {
    if ($inputFromPipeline -and $inputCount -lt 1) {
      $message = "Assertion failed (no objects piped to Test-Assertion): $info"
      Write-Debug -Message $message
      throw $message
    }
  }
}

# TODO: Can be removed
function Resolve-Version() {
    $TOOLS_DIR = Join-Path $SCRIPT_ROOT ".." | `
                 Join-Path -ChildPath ".." | `
                 Join-Path -ChildPath ".." | `
                 Join-Path -ChildPath ".." | `
                 Join-Path -ChildPath "tools"
    if (Test-Path "$TOOLS_DIR/dotnet-gitversion.exe") {
      test-assertion (($PSVersionTable.PSEdition -eq "Desktop") -or $IsWindows)
      $output = & "$TOOLS_DIR/dotnet-gitversion.exe" /output json
    }
    else {
      test-assertion (($PSVersionTable.PSEdition -eq "Core") -and (-not $IsWindows))
      Test-Path "$TOOLS_DIR/dotnet-gitversion" | test-assertion
      $output = & "$TOOLS_DIR/dotnet-gitversion" /output json
    }
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

# NOTE: We use publish to /.nuget folder strategy
# $VERSION = Resolve-Version

# Only create the tool-manifest once
if (-not (Test-Path ".config")) {
    dotnet new tool-manifest
}

# This is not needed, because we do _not_ check `.config/dotnet-tools.json` into GIT
#    dotnet tool restore 2>&1>$null

# Uninstall the tools (NOTE: '2>&1>$null' and '2>&1 | out-null' are equivalent)
dotnet tool uninstall dotnet-cgdata 2>&1>$null
dotnet tool uninstall dotnet-cgcsharp 2>&1>$null
dotnet tool uninstall dotnet-format 2>&1>$null

# Install the tools locally ('dotnet new tool-manifest' executed in curr dir)
# NOTE: You need to clear the cache before invoking: dotnet nuget locals all --clear,
#       if the version have not been bumped in a feature branch

# 'dotnet tool run cgdata' or 'dotnet cgdata' to invoke
dotnet tool install dotnet-cgdata --add-source $NUGET_DIR --version 0.1.*-*
# 'dotnet tool run cgcsharp' or 'dotnet cgcsharp' to invoke
dotnet tool install dotnet-cgcsharp --add-source $NUGET_DIR --version 0.1.*-*
# Install development build v5.x
# TODO: Change when v5 is published to nuget.org
# https://dev.azure.com/dnceng/public/_packaging?_a=package&feed=dotnet-tools&package=dotnet-format&version=5.0.207201&protocolType=NuGet
# dotnet tool install dotnet-format -version 5.0.207201
dotnet tool install dotnet-format --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json --version 5.0.*

# Test installation
Write-Host "Cogegen toolchain versions:"
Write-Host "  cgdata:    $(dotnet cgdata --version)"
Write-Host "  cgcsharp:  $(dotnet cgcsharp --version)"
Write-Host "  format:    $(dotnet format --version)"

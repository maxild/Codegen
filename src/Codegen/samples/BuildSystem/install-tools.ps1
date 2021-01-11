#!/usr/bin/env pwsh

$SCRIPT_ROOT = split-path -parent $MyInvocation.MyCommand.Definition

$ARTIFACTS_DIR = Join-Path $SCRIPT_ROOT ".." | `
                 Join-Path -ChildPath ".." | `
                 Join-Path -ChildPath ".." | `
                 Join-Path -ChildPath ".." | `
                 Join-Path -ChildPath "artifacts"

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

$VERSION = Resolve-Version

# Only create the tool-manifest once
if (-not (Test-Path ".config")) {
    dotnet new tool-manifest
}

# Uninstall the tools (NOTE: '2>&1>$null' and '2>&1 | out-null' are equivalent)
dotnet tool uninstall dotnet-cgdata 2>&1 | out-null
dotnet tool uninstall dotnet-cgcsharp 2>&1>$null

# Install the tools locally ('dotnet new tool-manifest' executed in curr dir)

# 'dotnet tool run cgdata' or 'dotnet cgdata' to invoke
dotnet tool install dotnet-cgdata --add-source $ARTIFACTS_DIR --version $VERSION
# 'dotnet tool run cgcsharp' or 'dotnet cgcsharp' to invoke
dotnet tool install dotnet-cgcsharp --add-source $ARTIFACTS_DIR --version $VERSION

# Test installation
dotnet cgdata --info
dotnet cgcsharp --info

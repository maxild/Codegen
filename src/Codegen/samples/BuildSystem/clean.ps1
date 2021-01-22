#! /usr/bin/env pwsh

[CmdletBinding()]
Param()

if ($PSCmdlet.MyInvocation.BoundParameters["Verbose"].IsPresent) {
    $HasVerboseFlag = $true
}

$SCRIPT_ROOT = split-path -parent $MyInvocation.MyCommand.Definition

# The following directories are in .gitignore
$DATA_DIR = Join-Path $SCRIPT_ROOT "data"
$BUILD_DIR = Join-Path $SCRIPT_ROOT "build"

if ($HasVerboseFlag) {
    Write-Host "Remove-Item -Recurse -Force $DATA_DIR -ErrorAction SilentlyContinue"
}
Remove-Item -Recurse -Force $DATA_DIR -ErrorAction SilentlyContinue
if ($HasVerboseFlag) {
    Write-Host "Remove-Item -Recurse -Force $BUILD_DIR -ErrorAction SilentlyContinue"
}
Remove-Item -Recurse -Force $BUILD_DIR -ErrorAction SilentlyContinue

# *.generated.cs files are in .gitignore
if ($HasVerboseFlag) {
    Write-Host "Get-ChildItem -Recurse *.generated.cs | Remove-Item -Force"
}
Push-Location $SCRIPT_ROOT
Get-ChildItem -Recurse *.generated.cs | Remove-Item -Force
Pop-Location

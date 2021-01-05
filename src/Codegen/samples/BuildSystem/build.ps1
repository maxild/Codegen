#! /usr/bin/env pwsh

[CmdletBinding()]
Param()

if ($PSCmdlet.MyInvocation.BoundParameters["Verbose"].IsPresent) {
    $HasVerboseFlag = $true
}

$SCRIPT_ROOT = split-path -parent $MyInvocation.MyCommand.Definition

# Step 1: Create intermediate data files
$SQL_FOLDER = Join-Path $SCRIPT_ROOT "sql" # cssql specifications are found here
$DATA_DIR = Join-Path $SCRIPT_ROOT "data"  # json data is saved here (MetadataModel)
# Step 2: Transform data files into c# types
$TEMPLATE_DIR = Join-Path $SCRIPT_ROOT "templates"  # Razor templates are found here
$OUT_DIR = Join-Path $SCRIPT_ROOT "src" | Join-Path -ChildPath "Brf.Domus.SampleModels"
$BUILD_DIR = Join-Path $SCRIPT_ROOT "build" # diagnostic files (g.cs) are saved here

$table = @( `
    @{ Name = "betalingstype"; Template = "dataenum.cshtml" } `
  , @{ Name = "saldotype"; Template = "dataenum.cshtml" } `
)

$table | ForEach-Object {

    #
    # Data
    #

    $name = $_.Name
    $template = $_.Template

    if ($HasVerboseFlag) {
        write-host -ForegroundColor DarkGreen "dotnet cgdata --name $name --sqlDir $SQL_FOLDER --outDir $DATA_DIR -v"
        dotnet cgdata --name $name --sqlDir $SQL_FOLDER --outDir $DATA_DIR -v
    }
    else {
        dotnet cgdata --name $name --sqlDir $SQL_FOLDER --outDir $DATA_DIR
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Error "ERROR: cgdata returned a non-zero exit code = $LASTEXITCODE. Terminating script..."
        exit $LastExitCode
    }

    #
    # CSharp
    #

    # ....men de benytter samme template
    if ($HasVerboseFlag) {
        write-host -ForegroundColor DarkGreen "dotnet cgcsharp --name $name --dataDir $DATA_DIR --template $(Join-Path $TEMPLATE_DIR $template) --outDir $OUT_DIR --diagDir $BUILD_DIR"
        dotnet cgcsharp --name $name `
            --dataDir $DATA_DIR `
            --template $(Join-Path $TEMPLATE_DIR $template) `
            --outDir $OUT_DIR `
            --diagDir $BUILD_DIR `
            -v
    }
    else {
        dotnet cgcsharp --name $name `
            --dataDir $DATA_DIR `
            --template $(Join-Path $TEMPLATE_DIR $template) `
            --outDir $OUT_DIR `
            --diagDir $BUILD_DIR
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ERROR: cgcsharp returned a non-zero exit code = $LASTEXITCODE. Terminating script..."
        exit $LastExitCode
    }
}

# Building all generated code
if ($HasVerboseFlag) {
    write-host -ForegroundColor DarkGreen "dotnet build"
}
Push-Location $SCRIPT_ROOT
dotnet build
Pop-Location

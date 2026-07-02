#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$BuildArguments
)

$ErrorActionPreference = 'Stop'
$buildProject = Join-Path $PSScriptRoot '_build\_build.csproj'

dotnet run --project $buildProject -- @BuildArguments
exit $LASTEXITCODE

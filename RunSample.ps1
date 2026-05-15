#requires -Version 7.5

[CmdletBinding()]
param (
  [Parameter()]
  [string]$Name = 'SampleApp1'
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

dotnet publish "./samples/$Name.cs" -p:PublishAot=false

if ($IsWindows) {
  $exe = Get-ChildItem -Path "./samples/artifacts/$Name/$Name.exe"
}
else {
  $exe = Get-ChildItem -Path "./samples/artifacts/$Name/$Name"
}

dotnet run --project ./src/AppleDust.Cli -- $exe.FullName

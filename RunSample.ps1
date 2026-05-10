#requires -Version 7.5

[CmdletBinding()]
param (
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

dotnet publish ./samples/SampleApp1.cs -p:PublishAot=false

if ($IsWindows) {
  $exe = Get-ChildItem -Path ./samples/artifacts/SampleApp1/SampleApp1.exe
}
else {
  $exe = Get-ChildItem -Path ./samples/artifacts/SampleApp1/SampleApp1
}

dotnet run --project ./src/AppleDust.Cli -- $exe.FullName

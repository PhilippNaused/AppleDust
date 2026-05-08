Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

dotnet publish ./samples/SampleApp1.cs

if ($IsWindows) {
  $exe = Get-ChildItem -Path ./samples/artifacts/SampleApp1/SampleApp1.exe
}
else {
  $exe = Get-ChildItem -Path ./samples/artifacts/SampleApp1/SampleApp1
}

dotnet run --project ./src/AppleDust.Cli -- $exe.FullName

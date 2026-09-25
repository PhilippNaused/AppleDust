#requires -Version 7.5

[CmdletBinding()]
param (
  [Parameter()]
  [string]$Name = 'sample1'
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

cargo build --manifest-path .\rust\apple-dust\Cargo.toml --profile bench --example $Name

if ($IsWindows) {
  $exe = Get-ChildItem -Path ".\target\release\examples\$Name.exe"
}
else {
  $exe = Get-ChildItem -Path "./target/release/examples/$Name"
}

dotnet run --project ./src/AppleDust.Cli -- $exe.FullName

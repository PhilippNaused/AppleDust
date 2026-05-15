#requires -Version 7.5

[CmdletBinding()]
param (
  [Parameter(Mandatory)]
  [System.IO.FileInfo]$File,

  [Parameter(Mandatory)]
  [string]$Property,

  [Parameter(Mandatory)]
  [AllowEmptyString()]
  [AllowNull()]
  [string]$Value
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

[xml]$xml = Get-Content $File.FullName
$node = $xml.SelectSingleNode("/Project/PropertyGroup/$Property")
if (-not $node) {
  $node = $xml.CreateElement($Property)
  $xml.Project.PropertyGroup.AppendChild($node) | Out-Null
}
$node.InnerText = $Value
$xml.Save($File.FullName)

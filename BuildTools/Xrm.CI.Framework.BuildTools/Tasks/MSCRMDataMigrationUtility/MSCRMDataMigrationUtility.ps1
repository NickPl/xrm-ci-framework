[CmdletBinding()]

param()

$ErrorActionPreference = "Stop"

Write-Verbose 'Entering MSCRMDataMigrationUtility.ps1'

#Get Parameters
$crmConnectionString = Get-VstsInput -Name crmConnectionString -Require
$crmConnectionTimeout = Get-VstsInput -Name crmConnectionTimeout -AsInt
$zipPath = Get-VstsInput -Name zipPath -Require

#Print Verbose
Write-Verbose "crmConnectionString = $crmConnectionString"
Write-Verbose "crmConnectionTimeout = $crmConnectionTimeout"
Write-Verbose "zipPath = $zipPath"

#Script Location
$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition
Write-Verbose "Script Path: $scriptPath"

& "$scriptPath\Lib\xRMCIFramework\9.0.0\DataMigrationUtility.ps1" -CrmConnectionString $crmConnectionString -Timeout $crmConnectionTimeout -ZipPath $zipPath

Write-Verbose 'Leaving MSCRMDataMigrationUtility.ps1'
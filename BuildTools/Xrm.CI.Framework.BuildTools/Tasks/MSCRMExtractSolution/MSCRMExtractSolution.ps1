[CmdletBinding()]

param()

$ErrorActionPreference = "Stop"

Write-Verbose 'Entering MSCRMExtractSolution.ps1'

#Get Parameters
$unpackedFilesFolder = Get-VstsInput -Name unpackedFilesFolder -Require
$mappingFile = Get-VstsInput -Name mappingFile
$packageType = Get-VstsInput -Name packageType -Require
$solutionFile = Get-VstsInput -Name solutionFile -Require
$treatUnpackWarningsAsErrors = Get-VstsInput -Name treatUnpackWarningsAsErrors -AsBool
$crmSdkVersion = Get-VstsInput -Name crmSdkVersion -Require

#TFS Build Parameters
$buildNumber = $env:BUILD_BUILDNUMBER
$sourcesDirectory = $env:BUILD_SOURCESDIRECTORY
$binariesDirectory = $env:BUILD_BINARIESDIRECTORY

#Print Verbose
Write-Verbose "unpackedFilesFolder = $unpackedFilesFolder"
Write-Verbose "mappingFile = $mappingFile"
Write-Verbose "packageType = $packageType"
Write-Verbose "solutionFile = $solutionFile"
Write-Verbose "treatUnpackWarningsAsErrors = $treatUnpackWarningsAsErrors"
Write-Verbose "buildNumber = $buildNumber"
Write-Verbose "sourcesDirectory = $sourcesDirectory"
Write-Verbose "binariesDirectory = $binariesDirectory"
Write-Verbose "crmSdkVersion = $crmSdkVersion"

#Script Location
$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition
Write-Verbose "Script Path: $scriptPath"

$CoreToolsPath = "$scriptPath\Lib\CoreTools\$crmSdkVersion"

if ($mappingFile -eq $sourcesDirectory)
{
	$mappingFile = $null
}

& "$scriptPath\Lib\xRMCIFramework\$crmSdkVersion\ExtractSolution.ps1" -UnpackedFilesFolder $unpackedFilesFolder -MappingFile $mappingFile -PackageType $packageType -solutionFile $solutionFile $includeVersionInSolutionFile -OutputPath -TreatUnpackWarningsAsErrors $treatUnpackWarningsAsErrors -CoreToolsPath $CoreToolsPath

Write-Verbose 'Leaving MSCRMExtractSolution.ps1'

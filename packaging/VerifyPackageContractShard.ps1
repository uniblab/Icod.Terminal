param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactDirectory,

    [Parameter(Mandatory = $true)]
    [ValidateSet('foundation', 'presentation', 'semantic', 'release')]
    [string]$Shard,

    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Release',

    [string]$ExpectedVersion = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Contract {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptName
    )

    $parameters = @{
        ArtifactDirectory = $ArtifactDirectory
        Configuration = $Configuration
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion)) {
        $parameters.ExpectedVersion = $ExpectedVersion
    }

    & (Join-Path $PSScriptRoot $ScriptName) @parameters
}

$scripts = switch ($Shard) {
    'foundation' {
        @(
            'VerifyPackageArtifact.ps1',
            'VerifyCursorStylePackage.ps1',
            'VerifySynchronizedOutputPackage.ps1',
            'VerifyProgressPackage.ps1'
        )
    }
    'presentation' {
        @(
            'VerifyPointerShapePackage.ps1',
            'VerifySemanticPromptPackage.ps1',
            'VerifyColorPackage.ps1',
            'VerifyColorOwnershipPackage.ps1'
        )
    }
    'semantic' {
        @(
            'VerifySemanticMetadataPackage.ps1',
            'VerifySafeOsc9Package.ps1',
            'VerifyOsc777Package.ps1',
            'VerifyOsc633Package.ps1',
            'VerifyModernKeyboardPackage.ps1',
            'VerifyHardeningPackage.ps1'
        )
    }
    'release' {
        @(
            'VerifyReleaseLinePackage.ps1',
            'VerifyDCursesPackage.ps1'
        )
    }
}

Write-Host ''
Write-Host "=== Package contract shard: $Shard ($Configuration) ==="
foreach ($script in $scripts) {
    Invoke-Contract -ScriptName $script
}

Write-Host ''
Write-Host "Package contract shard '$Shard' completed successfully ($Configuration)."

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host "> dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if (0 -ne $LASTEXITCODE) {
        throw "dotnet exited with status $LASTEXITCODE."
    }
}

function Get-RepositorySolution {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [switch]$AllowMissing
    )

    $solutions = @(
        Get-ChildItem -LiteralPath $RepositoryRoot -File |
            Where-Object { $_.Extension -in @('.sln', '.slnx') }
    )

    if (0 -eq $solutions.Count -and $AllowMissing) {
        return $null
    }
    if (1 -ne $solutions.Count) {
        throw "Expected exactly one root .sln or .slnx file; found $($solutions.Count)."
    }

    return $solutions[0].FullName
}

function Get-SolutionProjects {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SolutionPath,

        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    $output = @(& dotnet sln $SolutionPath list)
    if (0 -ne $LASTEXITCODE) {
        throw "Unable to list projects in '$SolutionPath'."
    }

    $projects = @()
    foreach ($line in $output) {
        $candidate = $line.Trim()
        if (-not $candidate.EndsWith('.csproj', [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $fullPath = if ([System.IO.Path]::IsPathRooted($candidate)) {
            $candidate
        } else {
            Join-Path $RepositoryRoot $candidate
        }
        $fullPath = [System.IO.Path]::GetFullPath($fullPath)
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Solution project '$candidate' does not exist at '$fullPath'."
        }
        $projects += $fullPath
    }

    return $projects
}

function Get-MSBuildProperty {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [string]$Configuration = 'Release'
    )

    $value = @(
        & dotnet msbuild $ProjectPath -nologo "-property:Configuration=$Configuration" "-getProperty:$Name"
    ) -join "`n"
    if (0 -ne $LASTEXITCODE) {
        throw "Unable to read MSBuild property '$Name' from '$ProjectPath'."
    }

    return $value.Trim()
}

function Get-PackageMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackagePath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $nuspecEntries = @(
            $archive.Entries |
                Where-Object { $_.FullName.EndsWith('.nuspec', [System.StringComparison]::OrdinalIgnoreCase) }
        )
        if (1 -ne $nuspecEntries.Count) {
            throw "Package '$PackagePath' contains $($nuspecEntries.Count) nuspec files; expected exactly one."
        }

        $reader = [System.IO.StreamReader]::new($nuspecEntries[0].Open())
        try {
            [xml]$nuspec = $reader.ReadToEnd()
        } finally {
            $reader.Dispose()
        }

        $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        if ($null -eq $metadata) {
            throw "Package '$PackagePath' does not contain nuspec metadata."
        }

        $idNode = $metadata.SelectSingleNode("*[local-name()='id']")
        $versionNode = $metadata.SelectSingleNode("*[local-name()='version']")
        if ($null -eq $idNode -or $null -eq $versionNode) {
            throw "Package '$PackagePath' does not declare package ID and version."
        }

        $readmeNode = $metadata.SelectSingleNode("*[local-name()='readme']")
        return [pscustomobject]@{
            Id = $idNode.InnerText.Trim()
            Version = $versionNode.InnerText.Trim()
            Readme = if ($null -eq $readmeNode) { '' } else { $readmeNode.InnerText.Trim().Replace('\\', '/') }
        }
    } finally {
        $archive.Dispose()
    }
}

Export-ModuleMember -Function @(
    'Invoke-DotNet',
    'Get-RepositorySolution',
    'Get-SolutionProjects',
    'Get-MSBuildProperty',
    'Get-PackageMetadata'
)

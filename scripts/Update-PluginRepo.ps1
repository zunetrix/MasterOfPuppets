param(
    [Parameter(Mandatory = $true)]
    [string] $PluginZip,

    [Parameter(Mandatory = $true)]
    [string] $DownloadUrl,

    [Parameter(Mandatory = $true)]
    [string] $LastUpdate,

    [string] $OutputPath = (Join-Path $PSScriptRoot '..\repo.json')
)

$ErrorActionPreference = 'Stop'

$resolvedZip = (Resolve-Path -LiteralPath $PluginZip).Path
$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("mop-plugin-repo-{0}" -f [Guid]::NewGuid())

try {
    New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
    Expand-Archive -LiteralPath $resolvedZip -DestinationPath $temporaryDirectory

    $manifestPath = Join-Path $temporaryDirectory 'MasterOfPuppets.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "The plugin archive does not contain MasterOfPuppets.json at its root."
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $requiredProperties = @(
        'Author',
        'Name',
        'InternalName',
        'AssemblyVersion',
        'Description',
        'ApplicableVersion',
        'DalamudApiLevel',
        'Punchline'
    )

    foreach ($property in $requiredProperties) {
        if ($null -eq $manifest.$property -or [string]::IsNullOrWhiteSpace([string] $manifest.$property)) {
            throw "The plugin manifest is missing required property '$property'."
        }
    }

    $entry = [ordered]@{
        Author = $manifest.Author
        Name = $manifest.Name
        InternalName = $manifest.InternalName
        AssemblyVersion = $manifest.AssemblyVersion
        Description = $manifest.Description
        ApplicableVersion = $manifest.ApplicableVersion
        RepoUrl = $manifest.RepoUrl
        Tags = @($manifest.Tags)
        DalamudApiLevel = [int] $manifest.DalamudApiLevel
        LoadRequiredState = [int] $manifest.LoadRequiredState
        LoadSync = [bool] $manifest.LoadSync
        CanUnloadAsync = [bool] $manifest.CanUnloadAsync
        LoadPriority = [int] $manifest.LoadPriority
        IconUrl = $manifest.IconUrl
        Punchline = $manifest.Punchline
        AcceptsFeedback = [bool] $manifest.AcceptsFeedback
        IsHide = $false
        IsTestingExclusive = $false
        DownloadLinkInstall = $DownloadUrl
        DownloadLinkUpdate = $DownloadUrl
        LastUpdate = $LastUpdate
    }

    $json = ConvertTo-Json -InputObject @($entry) -Depth 10
    $utf8WithoutBom = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($resolvedOutput, $json + [Environment]::NewLine, $utf8WithoutBom)
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}

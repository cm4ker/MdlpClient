param(
    [Parameter(Mandatory=$false)]
    [string]$DotnetX64Path = $env:MDLP_DOTNET_X64_PATH,

    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$ConsumerConfiguration = "Debug",

    [Parameter(Mandatory=$false)]
    [string]$RuntimeIdentifier = "win-x64",

    [Parameter(Mandatory=$false)]
    [string]$OutputDirectory = "MdlpApiClient.StdioProxy/aot/win-x64",

    [Parameter(Mandatory=$false)]
    [switch]$CopyNextToConsumerBins
)

function Resolve-DotnetCommand([string]$PreferredX64Path)
{
    if (-not [string]::IsNullOrWhiteSpace($PreferredX64Path))
    {
        $candidate = $PreferredX64Path
        if ((Test-Path $candidate) -and (Get-Item $candidate).PSIsContainer)
        {
            $candidate = Join-Path $candidate "dotnet.exe"
        }

        if (Test-Path $candidate)
        {
            return $candidate
        }
    }

    $x64Dotnet = "C:\Program Files\dotnet\x64\dotnet.exe"
    if (Test-Path $x64Dotnet)
    {
        return $x64Dotnet
    }

    return "dotnet"
}

$ErrorActionPreference = "Stop"

$workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$stdioProxyProjectPath = Join-Path $workspaceRoot "MdlpApiClient.StdioProxy\MdlpApiClient.StdioProxy.csproj"
if (-not (Test-Path $stdioProxyProjectPath))
{
    throw "Stdio proxy project file was not found: '$stdioProxyProjectPath'."
}

$dotnetCommand = Resolve-DotnetCommand -PreferredX64Path $DotnetX64Path
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $workspaceRoot $OutputDirectory))

if (-not (Test-Path $outputPath))
{
    New-Item -Path $outputPath -ItemType Directory | Out-Null
}

Write-Host "Publishing AOT stdio proxy"
Write-Host "  dotnet=$dotnetCommand"
Write-Host "  rid=$RuntimeIdentifier"
Write-Host "  output=$outputPath"

$publishArgs = @(
    "publish",
    $stdioProxyProjectPath,
    "-c", "Release",
    "-r", $RuntimeIdentifier,
    "-p:PublishAot=true",
    "-p:SelfContained=true",
    "-o", $outputPath
)

# On Windows ARM64, ILCompiler host defaults to OS architecture and may require cross-host package lookup.
# We force x64 host because publish is executed using x64 dotnet.
if ($IsWindows)
{
    $publishArgs += "-p:_hostArchitecture=x64"
}

$oldPath = $env:Path
try
{
    # Remove quote wrappers from PATH segments to avoid linker invocation parsing issues.
    $env:Path = (($env:Path -split ';' |
        ForEach-Object { $_.Trim().Trim("'", '"') } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ';')

    & $dotnetCommand @publishArgs
}
finally
{
    $env:Path = $oldPath
}

if ($LASTEXITCODE -ne 0)
{
    throw "AOT publish failed with exit code $LASTEXITCODE"
}

$exeName = if ($RuntimeIdentifier.StartsWith("win")) { "MdlpApiClient.StdioProxy.exe" } else { "MdlpApiClient.StdioProxy" }
$proxyPath = Join-Path $outputPath $exeName
if (-not (Test-Path $proxyPath))
{
    throw "AOT proxy executable was not found at '$proxyPath'."
}

if ($CopyNextToConsumerBins)
{
    $consumerDirs = @(
        (Join-Path $workspaceRoot "MdlpApiClient/bin/$ConsumerConfiguration/net8.0"),
        (Join-Path $workspaceRoot "MdlpApiClient.Tests/bin/$ConsumerConfiguration/net8.0")
    )

    foreach ($dir in $consumerDirs)
    {
        if (-not (Test-Path $dir))
        {
            continue
        }

        Copy-Item -Path $proxyPath -Destination (Join-Path $dir $exeName) -Force
        Write-Host "Copied: $(Join-Path $dir $exeName)"
    }
}

$env:MDLP_USE_CRYPTOPRO_STDIO_PROXY = "true"
$env:MDLP_CRYPTOPRO_STDIO_PROXY_PATH = $proxyPath
$env:MDLP_CRYPTOPRO_STDIO_DOTNET_PATH = ""

Write-Host "AOT stdio proxy is ready: $proxyPath"
Write-Host "Current process env set: MDLP_USE_CRYPTOPRO_STDIO_PROXY=true"
Write-Host "Current process env set: MDLP_CRYPTOPRO_STDIO_PROXY_PATH=$proxyPath"
Write-Host "Current process env set: MDLP_CRYPTOPRO_STDIO_DOTNET_PATH=(empty)"
Write-Host "If you started this script with 'pwsh ...', set env in your shell explicitly:"
Write-Host "  `$env:MDLP_USE_CRYPTOPRO_STDIO_PROXY='true'"
Write-Host "  `$env:MDLP_CRYPTOPRO_STDIO_PROXY_PATH='$proxyPath'"
Write-Host "  `$env:MDLP_CRYPTOPRO_STDIO_DOTNET_PATH=''"

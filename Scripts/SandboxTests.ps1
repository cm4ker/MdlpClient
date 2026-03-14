param(
    [Parameter(Mandatory=$false)]
    [string]$Thumbprint1 = $env:MDLP_SANDBOX_USER_THUMBPRINT_1,

    [Parameter(Mandatory=$false)]
    [string]$Thumbprint2 = $env:MDLP_SANDBOX_USER_THUMBPRINT_2,

    [Parameter(Mandatory=$false)]
    [string]$ApiBaseUrl = $(if ([string]::IsNullOrWhiteSpace($env:MDLP_TEST_API_BASE_URL)) { "https://sb.mdlp.crpt.ru/api/v1/" } else { $env:MDLP_TEST_API_BASE_URL }),

    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [Parameter(Mandatory=$false)]
    [string]$TestFilter = "FullyQualifiedName~SandboxTests",

    [Parameter(Mandatory=$false)]
    [switch]$NoBuild,

    [Parameter(Mandatory=$false)]
    [switch]$AllowSkipWhenUnavailable,

    [Parameter(Mandatory=$false)]
    [switch]$SkipCryptoProSspiPreflight,

    [Parameter(Mandatory=$false)]
    [switch]$DisableCryptoProHttpHandler,

    [Parameter(Mandatory=$false)]
    [switch]$DisableCryptoProStdioProxy,

    [Parameter(Mandatory=$false)]
    [switch]$StrictTlsValidation,

    [Parameter(Mandatory=$false)]
    [switch]$DoNotForceTls12,

    [Parameter(Mandatory=$false)]
    [string]$DotnetX64Path = $env:MDLP_DOTNET_X64_PATH
)

function Normalize-LegacySandboxBaseUrl([string]$value)
{
    if ([string]::IsNullOrWhiteSpace($value))
    {
        return $value
    }

    $normalized = $value.Replace("https://api.sb.mdlp.crpt.ru/api/v1/", "https://sb.mdlp.crpt.ru/api/v1/")
    $normalized = $normalized.Replace("http://api.sb.mdlp.crpt.ru/api/v1/", "http://sb.mdlp.crpt.ru/api/v1/")
    $normalized = $normalized.Replace("https://api.sb.mdlp.crpt.ru/api/v1", "https://sb.mdlp.crpt.ru/api/v1")
    $normalized = $normalized.Replace("http://api.sb.mdlp.crpt.ru/api/v1", "http://sb.mdlp.crpt.ru/api/v1")
    return $normalized
}

function Resolve-CsptestPath()
{
    $candidates = @(
        $env:MDLP_CRYPTOPRO_CSPTEST_PATH,
        "C:\Program Files\Crypto Pro\CSP\csptest.exe",
        "C:\Program Files (x86)\Crypto Pro\CSP\csptest.exe"
    )

    foreach ($candidate in $candidates)
    {
        if ([string]::IsNullOrWhiteSpace($candidate))
        {
            continue
        }

        if (Test-Path $candidate)
        {
            return $candidate
        }
    }

    return $null
}

function Invoke-CryptoProSspiPreflight([string]$apiBaseUrl)
{
    if (-not $IsWindows)
    {
        return
    }

    $csptestPath = Resolve-CsptestPath
    if ([string]::IsNullOrWhiteSpace($csptestPath))
    {
        Write-Host "CryptoPro preflight skipped: csptest.exe is not found."
        return
    }

    $uri = $null
    try
    {
        $uri = [Uri]$apiBaseUrl
    }
    catch
    {
        Write-Host "CryptoPro preflight skipped: invalid MDLP_TEST_API_BASE_URL '$apiBaseUrl'."
        return
    }

    if ($uri.Scheme -ne 'https')
    {
        Write-Host "CryptoPro preflight skipped: only HTTPS endpoint requires this check."
        return
    }

    $hostName = $uri.Host
    $port = if ($uri.IsDefaultPort) { 443 } else { $uri.Port }
    $probePath = ($uri.AbsolutePath.TrimEnd('/') + '/documents/doc_size')
    if (-not $probePath.StartsWith('/'))
    {
        $probePath = '/' + $probePath
    }

    Write-Host "CryptoPro preflight: checking ${hostName}:$port via CryptoPro SSP"

    & $csptestPath -tlsc -server $hostName -port $port -file $probePath -exchange 3 -cpsspi -forcecheck -nosave | Out-Null
    if ($LASTEXITCODE -eq 0)
    {
        Write-Host "CryptoPro preflight: OK"
        return
    }

    Write-Host "CryptoPro preflight failed with code $LASTEXITCODE"
    Write-Host "If dotnet test fails with 0x80090304, run elevated script: ./Scripts/EnableCryptoProSspiX64.ps1"
}

function Resolve-DotnetCommand([switch]$PreferX64, [string]$PreferredX64Path)
{
    if (-not $PreferX64)
    {
        return "dotnet"
    }

    if (-not [string]::IsNullOrWhiteSpace($PreferredX64Path))
    {
        $candidate = $PreferredX64Path
        if ((Test-Path $candidate) -and (Get-Item $candidate).PSIsContainer)
        {
            $candidate = Join-Path $candidate "dotnet.exe"
        }

        if (Test-Path $candidate)
        {
            $sdkList = & $candidate --list-sdks 2>$null | Out-String
            if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($sdkList))
            {
                return $candidate
            }

            Write-Host "Provided x64 dotnet path does not contain SDKs: '$candidate'."
        }
        else
        {
            Write-Host "Provided x64 dotnet path was not found: '$candidate'."
        }
    }

    $x64Dotnet = "C:\Program Files\dotnet\x64\dotnet.exe"
    if (-not (Test-Path $x64Dotnet))
    {
        return "dotnet"
    }

    $sdkList = & $x64Dotnet --list-sdks 2>$null | Out-String
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($sdkList))
    {
        return $x64Dotnet
    }

    Write-Host "x64 dotnet SDK was not found at '$x64Dotnet'; using default dotnet command."
    return "dotnet"
}

if ([string]::IsNullOrWhiteSpace($Thumbprint1) -or [string]::IsNullOrWhiteSpace($Thumbprint2))
{
    Write-Error "Both thumbprints are required. Set MDLP_SANDBOX_USER_THUMBPRINT_1/2 or pass -Thumbprint1/-Thumbprint2."
    exit 2
}

if ([string]::IsNullOrWhiteSpace($TestFilter))
{
    Write-Error "Test filter must not be empty."
    exit 2
}

$ApiBaseUrl = Normalize-LegacySandboxBaseUrl $ApiBaseUrl

$env:MDLP_SANDBOX_USER_THUMBPRINT_1 = $Thumbprint1
$env:MDLP_SANDBOX_USER_THUMBPRINT_2 = $Thumbprint2
$env:MDLP_TEST_API_BASE_URL = $ApiBaseUrl
$env:MDLP_SKIP_SANDBOX_TESTS_WHEN_UNAVAILABLE = if ($AllowSkipWhenUnavailable) { "true" } else { "false" }

$preferCryptoProHttpHandler = -not $DisableCryptoProHttpHandler
$dotnetCommand = Resolve-DotnetCommand -PreferX64:$preferCryptoProHttpHandler -PreferredX64Path $DotnetX64Path
$dotnetHostArchitecture = if ($dotnetCommand -eq "dotnet")
{
    [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
}
else
{
    "x64"
}

$canUseCryptoProHttpHandler = $preferCryptoProHttpHandler -and ($dotnetHostArchitecture -eq "x64")
if ($preferCryptoProHttpHandler -and -not $canUseCryptoProHttpHandler)
{
    Write-Host "CryptoPro CpHttpHandler disabled: libcore runtime assets are available for x64 only."
    Write-Host "Install x64 dotnet SDK and run tests through x64 dotnet to enable this transport."
}

$stdioDotnetCommand = Resolve-DotnetCommand -PreferX64:$true -PreferredX64Path $DotnetX64Path
$stdioDotnetArchitecture = if ($stdioDotnetCommand -eq "dotnet")
{
    [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
}
else
{
    "x64"
}

$canUseCryptoProStdioProxy = $canUseCryptoProHttpHandler -and (-not $DisableCryptoProStdioProxy) -and ($stdioDotnetArchitecture -eq "x64")
if ($canUseCryptoProHttpHandler -and -not $DisableCryptoProStdioProxy -and -not $canUseCryptoProStdioProxy)
{
    Write-Host "CryptoPro stdio proxy disabled: x64 dotnet host for proxy is not available."
}

$stdioProxyAotOutputDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\MdlpApiClient.StdioProxy\aot\win-x64"))
$stdioProxyAotExePath = Join-Path $stdioProxyAotOutputDirectory "MdlpApiClient.StdioProxy.exe"
$stdioProxyDllPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\MdlpApiClient.StdioProxy\bin\$Configuration\net8.0\MdlpApiClient.StdioProxy.dll"))
$stdioProxyPath = $null
$stdioProxyRequiresDotnetHost = $true

if ($canUseCryptoProStdioProxy -and -not $NoBuild)
{
    if ($IsWindows)
    {
        Write-Host "Publishing AOT stdio proxy helper with dotnet command: $stdioDotnetCommand"
        $publishArgs = @(
            "publish",
            "MdlpApiClient.StdioProxy/MdlpApiClient.StdioProxy.csproj",
            "-c", "Release",
            "-r", "win-x64",
            "-p:PublishAot=true",
            "-p:SelfContained=true",
            "-p:_hostArchitecture=x64",
            "-o", $stdioProxyAotOutputDirectory
        )

        $oldPath = $env:Path
        try
        {
            $env:Path = (($env:Path -split ';' |
                ForEach-Object { $_.Trim().Trim("'", '"') } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ';')

            & $stdioDotnetCommand @publishArgs
        }
        finally
        {
            $env:Path = $oldPath
        }

        if ($LASTEXITCODE -ne 0)
        {
            Write-Error "Failed to publish AOT MdlpApiClient.StdioProxy (exit code $LASTEXITCODE)."
            exit $LASTEXITCODE
        }
    }
    else
    {
        Write-Host "Building managed stdio proxy helper with dotnet command: $stdioDotnetCommand"
        & $stdioDotnetCommand build "MdlpApiClient.StdioProxy/MdlpApiClient.StdioProxy.csproj" -c $Configuration
        if ($LASTEXITCODE -ne 0)
        {
            Write-Error "Failed to build MdlpApiClient.StdioProxy (exit code $LASTEXITCODE)."
            exit $LASTEXITCODE
        }
    }
}

if ($canUseCryptoProStdioProxy)
{
    if (Test-Path $stdioProxyAotExePath)
    {
        $stdioProxyPath = $stdioProxyAotExePath
        $stdioProxyRequiresDotnetHost = $false
    }
    elseif (Test-Path $stdioProxyDllPath)
    {
        $stdioProxyPath = $stdioProxyDllPath
        $stdioProxyRequiresDotnetHost = $true
    }
    else
    {
        Write-Host "CryptoPro stdio proxy disabled: helper executable/dll was not found."
        Write-Host "Expected AOT EXE: '$stdioProxyAotExePath'"
        Write-Host "Expected managed DLL: '$stdioProxyDllPath'"
        Write-Host "Run without -NoBuild or publish/build MdlpApiClient.StdioProxy manually."
        $canUseCryptoProStdioProxy = $false
    }
}

$env:MDLP_USE_CRYPTOPRO_HTTP_HANDLER = if ($canUseCryptoProHttpHandler) { "true" } else { "false" }
$env:MDLP_USE_CRYPTOPRO_STDIO_PROXY = if ($canUseCryptoProStdioProxy) { "true" } else { "false" }
$env:MDLP_CRYPTOPRO_STDIO_DOTNET_PATH = if ($canUseCryptoProStdioProxy -and $stdioProxyRequiresDotnetHost) { $stdioDotnetCommand } else { "" }
$env:MDLP_CRYPTOPRO_STDIO_PROXY_PATH = if ($canUseCryptoProStdioProxy) { $stdioProxyPath } else { "" }
$useInsecureValidation = $canUseCryptoProHttpHandler -and (-not $StrictTlsValidation)
$forceTls12 = $canUseCryptoProHttpHandler -and (-not $DoNotForceTls12)
$env:MDLP_CRYPTOPRO_HTTP_HANDLER_INSECURE_SKIP_CERT_VALIDATION = if ($useInsecureValidation) { "true" } else { "false" }
$env:MDLP_CRYPTOPRO_HTTP_HANDLER_FORCE_TLS12 = if ($forceTls12) { "true" } else { "false" }

Write-Host "Running SandboxTests profile"
Write-Host "  dotnet command=$dotnetCommand"
Write-Host "  dotnet host architecture=$dotnetHostArchitecture"
Write-Host "  MDLP_TEST_API_BASE_URL=$($env:MDLP_TEST_API_BASE_URL)"
Write-Host "  MDLP_SKIP_SANDBOX_TESTS_WHEN_UNAVAILABLE=$($env:MDLP_SKIP_SANDBOX_TESTS_WHEN_UNAVAILABLE)"
Write-Host "  MDLP_USE_CRYPTOPRO_HTTP_HANDLER=$($env:MDLP_USE_CRYPTOPRO_HTTP_HANDLER)"
Write-Host "  MDLP_USE_CRYPTOPRO_STDIO_PROXY=$($env:MDLP_USE_CRYPTOPRO_STDIO_PROXY)"
Write-Host "  MDLP_CRYPTOPRO_STDIO_DOTNET_PATH=$($env:MDLP_CRYPTOPRO_STDIO_DOTNET_PATH)"
Write-Host "  MDLP_CRYPTOPRO_STDIO_PROXY_PATH=$($env:MDLP_CRYPTOPRO_STDIO_PROXY_PATH)"
Write-Host "  MDLP_CRYPTOPRO_HTTP_HANDLER_INSECURE_SKIP_CERT_VALIDATION=$($env:MDLP_CRYPTOPRO_HTTP_HANDLER_INSECURE_SKIP_CERT_VALIDATION)"
Write-Host "  MDLP_CRYPTOPRO_HTTP_HANDLER_FORCE_TLS12=$($env:MDLP_CRYPTOPRO_HTTP_HANDLER_FORCE_TLS12)"
Write-Host "  MDLP_SANDBOX_USER_THUMBPRINT_1=$($env:MDLP_SANDBOX_USER_THUMBPRINT_1)"
Write-Host "  MDLP_SANDBOX_USER_THUMBPRINT_2=$($env:MDLP_SANDBOX_USER_THUMBPRINT_2)"
Write-Host "  Test filter=$TestFilter"

if (-not $SkipCryptoProSspiPreflight)
{
    Invoke-CryptoProSspiPreflight -apiBaseUrl $env:MDLP_TEST_API_BASE_URL
}

$dotnetArgs = @(
    "test",
    "MdlpApiClient.Tests/MdlpApiClient.Tests.csproj",
    "-c", $Configuration,
    "--filter", $TestFilter
)

if ($NoBuild)
{
    $dotnetArgs += "--no-build"
}

& $dotnetCommand @dotnetArgs
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0)
{
    Write-Host "SandboxTests profile failed with exit code $exitCode"
    Write-Host "If you see SSL error 0x80090304, run elevated script: ./Scripts/EnableCryptoProSspiX64.ps1"
}
else
{
    Write-Host "SandboxTests profile completed successfully"
}

exit $exitCode

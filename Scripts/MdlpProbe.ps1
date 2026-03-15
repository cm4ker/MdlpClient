param(
    [Parameter(Mandatory=$false)]
    [string]$DotnetX64Path = $env:MDLP_DOTNET_X64_PATH,

    [Parameter(Mandatory=$false)]
    [ValidateSet("resident", "nonresident")]
    [string]$Auth = "resident",

    [Parameter(Mandatory=$false)]
    [ValidateSet("doc-size", "token")]
    [string]$Operation = "doc-size",

    [Parameter(Mandatory=$false)]
    [string]$BaseUrl = $env:MDLP_TEST_API_BASE_URL,

    [Parameter(Mandatory=$false)]
    [string]$ClientId = $(if ([string]::IsNullOrWhiteSpace($env:MDLP_CLIENT_ID_1)) { "22d12250-6cf3-4a87-b439-f698cfddc498" } else { $env:MDLP_CLIENT_ID_1 }),

    [Parameter(Mandatory=$false)]
    [string]$ClientSecret = $(if ([string]::IsNullOrWhiteSpace($env:MDLP_CLIENT_SECRET_1)) { "3deb0ba1-26f2-4516-b652-931fe832e3ff" } else { $env:MDLP_CLIENT_SECRET_1 }),

    [Parameter(Mandatory=$false)]
    [string]$UserId,

    [Parameter(Mandatory=$false)]
    [string]$Password = $(if ([string]::IsNullOrWhiteSpace($env:MDLP_USER_PASSWORD_1)) { "password" } else { $env:MDLP_USER_PASSWORD_1 }),

    [Parameter(Mandatory=$false)]
    [string]$SignThumbprint = $(if ([string]::IsNullOrWhiteSpace($env:MDLP_SIGN_THUMBPRINT)) {
        if ([string]::IsNullOrWhiteSpace($env:MDLP_SANDBOX_USER_THUMBPRINT_1)) { "10E4921908D24A0D1AD94A29BD0EF51696C6D8DA" } else { $env:MDLP_SANDBOX_USER_THUMBPRINT_1 }
    } else {
        $env:MDLP_SIGN_THUMBPRINT
    }),

    [Parameter(Mandatory=$false)]
    [string]$CsptestPath = $(if ([string]::IsNullOrWhiteSpace($env:MDLP_CRYPTOPRO_CSPTEST_PATH)) { "" } else { $env:MDLP_CRYPTOPRO_CSPTEST_PATH }),

    [Parameter(Mandatory=$false)]
    [string]$CryptoproPin = $(if ([string]::IsNullOrWhiteSpace($env:MDLP_CRYPTOPRO_PIN)) { "" } else { $env:MDLP_CRYPTOPRO_PIN })
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

if ([string]::IsNullOrWhiteSpace($UserId))
{
    $UserId = if ($Operation -eq "token")
    {
        if ([string]::IsNullOrWhiteSpace($env:MDLP_USER_ID_1))
        {
            if ([string]::IsNullOrWhiteSpace($env:MDLP_USER_STARTER_1)) { "starter_resident_1" } else { $env:MDLP_USER_STARTER_1 }
        }
        else
        {
            $env:MDLP_USER_ID_1
        }
    }
    elseif ($Auth -eq "resident")
    {
        if ([string]::IsNullOrWhiteSpace($env:MDLP_SANDBOX_USER_THUMBPRINT_1)) { "10E4921908D24A0D1AD94A29BD0EF51696C6D8DA" } else { $env:MDLP_SANDBOX_USER_THUMBPRINT_1 }
    }
    else
    {
        if ([string]::IsNullOrWhiteSpace($env:MDLP_USER_STARTER_1)) { "starter_resident_1" } else { $env:MDLP_USER_STARTER_1 }
    }
}

$workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$probeProjectPath = Join-Path $workspaceRoot "MdlpApiClient.Probe\MdlpApiClient.Probe.csproj"
if (-not (Test-Path $probeProjectPath))
{
    Write-Error "Probe project file was not found: '$probeProjectPath'."
    exit 1
}

$dotnetCommand = Resolve-DotnetCommand -PreferredX64Path $DotnetX64Path

$probeArgs = @(
    "run",
    "--project", $probeProjectPath,
    "--",
    "--auth", $Auth,
    "--operation", $Operation
)

if (-not [string]::IsNullOrWhiteSpace($BaseUrl))
{
    $probeArgs += @("--base-url", $BaseUrl)
}

if (-not [string]::IsNullOrWhiteSpace($ClientId))
{
    $probeArgs += @("--client-id", $ClientId)
}

if (-not [string]::IsNullOrWhiteSpace($ClientSecret))
{
    $probeArgs += @("--client-secret", $ClientSecret)
}

if (-not [string]::IsNullOrWhiteSpace($UserId))
{
    $probeArgs += @("--user-id", $UserId)
}

if ($Auth -eq "nonresident" -and -not [string]::IsNullOrWhiteSpace($Password))
{
    $probeArgs += @("--password", $Password)
}

if (-not [string]::IsNullOrWhiteSpace($SignThumbprint))
{
    $probeArgs += @("--sign-thumbprint", $SignThumbprint)
}

if ($Operation -eq "token" -and -not [string]::IsNullOrWhiteSpace($CsptestPath))
{
    $probeArgs += @("--csptest-path", $CsptestPath)
}

if ($Operation -eq "token" -and -not [string]::IsNullOrWhiteSpace($CryptoproPin))
{
    $probeArgs += @("--cryptopro-pin", $CryptoproPin)
}

Write-Host "Running MdlpApiClient.Probe"
Write-Host "  dotnet=$dotnetCommand"
Write-Host "  auth=$Auth"
Write-Host "  operation=$Operation"
Write-Host "  user-id=$UserId"
if ($Operation -eq "token")
{
    Write-Host "  sign-thumbprint=$SignThumbprint"
    Write-Host "  csptest-path=$(if ([string]::IsNullOrWhiteSpace($CsptestPath)) { '<auto>' } else { $CsptestPath })"
    Write-Host "  cryptopro-pin=$(if ([string]::IsNullOrWhiteSpace($CryptoproPin)) { '<empty>' } else { '<set>' })"
}

& $dotnetCommand @probeArgs
exit $LASTEXITCODE

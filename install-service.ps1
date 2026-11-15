#Requires -RunAsAdministrator
# Publica o Binder Pokédex e instala/atualiza como serviço do Windows (inicia com o PC).
param(
    [string]$InstallDir = 'C:\Apps\BinderPokedex',
    [string]$ServiceName = 'BinderPokedex',
    [int]$Port = 5150
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$account = "NT SERVICE\$ServiceName"

$existing = Get-Service $ServiceName -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne 'Stopped') {
    Stop-Service $ServiceName -Force
    (Get-Service $ServiceName).WaitForStatus('Stopped', '00:00:30')
}

# Publica direto na pasta de instalação (binder.db, se existir, é preservado).
dotnet publish "$root\src\BinderPokedex" -c Release -r win-x64 --self-contained false -o $InstallDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falhou' }

# Na primeira instalação, leva junto o banco usado em desenvolvimento.
$devDb = Join-Path $root 'src\BinderPokedex\binder.db'
$db = Join-Path $InstallDir 'binder.db'
if (-not (Test-Path $db) -and (Test-Path $devDb)) {
    Copy-Item $devDb $db
    Write-Host "Banco copiado de $devDb"
}

if (-not $existing) {
    New-Service -Name $ServiceName -DisplayName 'Binder Pokédex' `
        -Description 'Pokédex do binder (http://0.0.0.0:5150)' `
        -BinaryPathName "`"$InstallDir\BinderPokedex.exe`"" -StartupType Automatic | Out-Null
    # Conta virtual do serviço, como no Orçamento.
    sc.exe config $ServiceName obj= $account | Out-Null
    # Reinicia sozinho se cair.
    sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/30000 | Out-Null
}

# A conta do serviço precisa gravar o binder.db na pasta.
icacls $InstallDir /grant "${account}:(OI)(CI)M" /T /Q | Out-Null

if (-not (Get-NetFirewallRule -DisplayName 'Binder Pokedex' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName 'Binder Pokedex' -Direction Inbound -LocalPort $Port -Protocol TCP -Action Allow -Profile Private | Out-Null
}

Start-Service $ServiceName
Get-Service $ServiceName | Format-Table Name, Status, StartType

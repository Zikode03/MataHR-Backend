#requires -Version 7.0
param([switch]$MigrateOnly)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$settings = @{}
foreach ($line in [IO.File]::ReadAllLines((Join-Path $repoRoot '.env'))) {
    if ($line -match '^([A-Z_]+)=(.*)$') {
        $settings[$Matches[1]] = $Matches[2].Trim().Trim("'")
    }
}
if (!$settings.MSSQL_SA_PASSWORD) { throw 'Set MSSQL_SA_PASSWORD in .env.' }
$port = if ($settings.MSSQL_PORT) { $settings.MSSQL_PORT } else { '14336' }
$values = @{
    'ConnectionStrings__Default' = "Server=localhost,$port;Database=MataHR_Development;User Id=sa;Password=$($settings.MSSQL_SA_PASSWORD);Encrypt=True;TrustServerCertificate=True"
    'Bootstrap__Email' = $settings.MATAHR_BOOTSTRAP_EMAIL
    'Bootstrap__Password' = $settings.MATAHR_BOOTSTRAP_PASSWORD
    'Bootstrap__TenantId' = $settings.MATAHR_TENANT_ID
}
$previous = @{}
try {
    foreach ($name in $values.Keys) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name)
        [Environment]::SetEnvironmentVariable($name, $values[$name])
    }
    $migratorArgs = if ($MigrateOnly) { @('--migrate-only') } else { @() }
    & dotnet run --project (Join-Path $repoRoot 'src/MataHR.DbMigrator') -- @migratorArgs
    if ($LASTEXITCODE -ne 0) { throw 'MataHR database setup failed.' }
} finally {
    foreach ($name in $previous.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name])
    }
}

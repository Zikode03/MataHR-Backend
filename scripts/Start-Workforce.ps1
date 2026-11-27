#requires -Version 7.0
param([switch]$ApplyDevelopmentMigrations, [ValidateSet("Debug", "Release")][string]$Configuration = "Debug")
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$settings=@{}
foreach($line in [IO.File]::ReadAllLines((Join-Path $repoRoot '.env'))){if($line -match '^([A-Z_]+)=(.*)$'){$value=$Matches[2].Trim();if($value.StartsWith("'") -and $value.EndsWith("'")){$value=$value.Substring(1,$value.Length-2)};$settings[$Matches[1]]=$value}}
if(!$settings.MSSQL_SA_PASSWORD){throw 'Set MSSQL_SA_PASSWORD in .env.'}
$port=if($settings.MSSQL_PORT){$settings.MSSQL_PORT}else{'14336'}
$values=@{
 'ConnectionStrings__Default'="Server=localhost,$port;Database=MataHR_Development;User Id=sa;Password=$($settings.MSSQL_SA_PASSWORD);Encrypt=True;TrustServerCertificate=True";
 'ASPNETCORE_ENVIRONMENT'='Development';
 'Database__UseManagedNetworkingOnWindows'='true';
 'Bootstrap__TenantId'=$settings.MATAHR_TENANT_ID;
 'LegacyImport__SourceTenantId'=$settings.MATAHR_LEGACY_SOURCE_TENANT_ID;'LegacyImport__TargetTenantId'=$settings.MATAHR_TENANT_ID
}
$previous=@{}
try{
 foreach($name in $values.Keys){$previous[$name]=[Environment]::GetEnvironmentVariable($name);[Environment]::SetEnvironmentVariable($name,$values[$name])}
 if ($ApplyDevelopmentMigrations) { & (Join-Path $PSScriptRoot 'Migrate-Workforce.ps1') }
 & dotnet run --project (Join-Path $repoRoot 'src/MataHR.HttpApi.Host') --configuration $Configuration --urls https://localhost:7043
 if($LASTEXITCODE -ne 0){throw 'Workforce backend failed to start.'}
}finally{foreach($name in $previous.Keys){[Environment]::SetEnvironmentVariable($name,$previous[$name])}}

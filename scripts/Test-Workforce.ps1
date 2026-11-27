#requires -Version 7.0
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$settings=@{}
foreach($line in [IO.File]::ReadAllLines((Join-Path $repoRoot '.env'))){if($line -match '^([A-Z_]+)=(.*)$'){$value=$Matches[2].Trim();if($value.StartsWith("'") -and $value.EndsWith("'")){$value=$value.Substring(1,$value.Length-2)};$settings[$Matches[1]]=$value}}
if(!$settings.MSSQL_SA_PASSWORD){throw 'Set MSSQL_SA_PASSWORD in .env.'}
$old=[Environment]::GetEnvironmentVariable('MATAHR_TEST_SQL')
try {
 $port=if($settings.MSSQL_PORT){$settings.MSSQL_PORT}else{'14336'}
 $env:MATAHR_TEST_SQL="Server=localhost,$port;Database=master;User Id=sa;Password=$($settings.MSSQL_SA_PASSWORD);Encrypt=True;TrustServerCertificate=True"
 & dotnet test (Join-Path $repoRoot 'tests/MataHR.IntegrationTests') --artifacts-path (Join-Path $repoRoot '.local/workforce-tests') --verbosity minimal
 if($LASTEXITCODE -ne 0){throw 'Workforce integration tests failed.'}
} finally {[Environment]::SetEnvironmentVariable('MATAHR_TEST_SQL',$old)}

#requires -Version 7.0
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$settings=@{}
foreach($line in [IO.File]::ReadAllLines((Join-Path $repoRoot '.env'))){
 if($line -match '^([A-Z_]+)=(.*)$'){
  $value=$Matches[2].Trim()
  if($value.StartsWith("'") -and $value.EndsWith("'")){$value=$value.Substring(1,$value.Length-2)}
  $settings[$Matches[1]]=$value
 }
}
if(!$settings.MSSQL_SA_PASSWORD){throw 'Set MSSQL_SA_PASSWORD in .env.'}
$port=if($settings.MSSQL_PORT){$settings.MSSQL_PORT}else{'14336'}
$env:ConnectionStrings__Default="Server=localhost,$port;Database=MataHR;User Id=sa;Password=$($settings.MSSQL_SA_PASSWORD);Encrypt=True;TrustServerCertificate=True;Connect Timeout=5"
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:Database__UseManagedNetworkingOnWindows='true'
$env:Database__InitializeDevelopmentSchema='true'
$env:Bootstrap__Email=$settings.MATAHR_BOOTSTRAP_EMAIL
$env:Bootstrap__Password=$settings.MATAHR_BOOTSTRAP_PASSWORD
& dotnet run --project (Join-Path $repoRoot 'src/MataHR.Api') --urls https://localhost:7043
if($LASTEXITCODE -ne 0){throw 'Backend startup failed.'}

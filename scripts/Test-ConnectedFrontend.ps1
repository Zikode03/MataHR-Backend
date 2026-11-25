#requires -Version 7.0
param([Parameter(Mandatory)][string]$FrontendPath)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$frontendRoot=(Resolve-Path -LiteralPath $FrontendPath).Path
$stateDirectory=Join-Path $frontendRoot '.local'
New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
$stateFile=Join-Path $stateDirectory 'connected-browser.json'
if(Test-Path -LiteralPath $stateFile){throw 'A browser-test state file already exists. Stop the previous test host before running again.'}
if(Test-Path -LiteralPath ($stateFile+'.stop')){Remove-Item -LiteralPath ($stateFile+'.stop')}
$settings=@{}
foreach($line in [IO.File]::ReadAllLines((Join-Path $repoRoot '.env'))){if($line -match '^([A-Z_]+)=(.*)$'){$settings[$Matches[1]]=$Matches[2].Trim().Trim("'")}}
$port=if($settings.MSSQL_PORT){$settings.MSSQL_PORT}else{'14336'}
$oldSql=[Environment]::GetEnvironmentVariable('MATAHR_TEST_SQL')
$oldState=[Environment]::GetEnvironmentVariable('MATAHR_E2E_STATE')
$runner=$null
try{
 $env:MATAHR_TEST_SQL="Server=localhost,$port;Database=master;User Id=sa;Password=$($settings.MSSQL_SA_PASSWORD);Encrypt=True;TrustServerCertificate=True"
 $env:MATAHR_E2E_STATE=$stateFile
 & dotnet build (Join-Path $repoRoot 'tests/MataHR.BrowserHost') --artifacts-path (Join-Path $repoRoot '.local/browser-host') --verbosity quiet
 if($LASTEXITCODE -ne 0){throw 'Browser fixture build failed.'}
 $runnerDll=Join-Path $repoRoot '.local/browser-host/bin/MataHR.BrowserHost/debug/MataHR.BrowserHost.dll'
 $options=@{FilePath='dotnet';ArgumentList=@("`"$runnerDll`"","`"$stateFile`"");WorkingDirectory=$repoRoot;PassThru=$true;RedirectStandardOutput=(Join-Path $repoRoot '.local/browser-host.log');RedirectStandardError=(Join-Path $repoRoot '.local/browser-host-error.log')}
 if($IsWindows){$options.WindowStyle='Hidden'}
 $runner=Start-Process @options
 $deadline=[DateTime]::UtcNow.AddSeconds(60)
 while(!(Test-Path -LiteralPath $stateFile)){
  if($runner.HasExited){throw 'Browser fixture exited. Inspect .local/browser-host-error.log.'}
  if([DateTime]::UtcNow -gt $deadline){throw 'Browser fixture did not start within 60 seconds.'}
  Start-Sleep -Milliseconds 250
 }
 & npm --prefix $frontendRoot run test:e2e
 if($LASTEXITCODE -ne 0){throw 'Connected frontend browser tests failed.'}
}finally{
 if($runner -and !$runner.HasExited){[IO.File]::WriteAllText($stateFile+'.stop','stop');if(!$runner.WaitForExit(30000)){$runner.Kill();Write-Warning 'Test host required termination; check for a leftover GUID-named test database.'}}
 [Environment]::SetEnvironmentVariable('MATAHR_TEST_SQL',$oldSql)
 [Environment]::SetEnvironmentVariable('MATAHR_E2E_STATE',$oldState)
}

#requires -Version 7.0
param([switch]$ApplyDevelopmentMigrations)
# Compatibility entry point: the only supported host is the layered Workforce host.
& (Join-Path $PSScriptRoot 'Start-Workforce.ps1') -ApplyDevelopmentMigrations:$ApplyDevelopmentMigrations

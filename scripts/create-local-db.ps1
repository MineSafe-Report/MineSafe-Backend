# Creates the local development database from Michael's SQL scripts.
# It does NOT change the scripts; it just runs database/schema/*.sql in order.
#
# Usage (from the repo folder, in PowerShell):
#     ./scripts/create-local-db.ps1
#
# Needs the "sqlcmd" tool (comes with SQL Server / SSMS; or: winget install sqlcmd).
# No sqlcmd? Open SSMS, connect to (localdb)\MSSQLLocalDB, create a database called
# MineSafetyDb, then open and run each file in database/schema in number order.

param(
    [string]$Server = "(localdb)\MSSQLLocalDB",
    [string]$Database = "MineSafetyDb"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$schemaFolder = Join-Path $repoRoot "database\schema"

Write-Host "Creating database '$Database' on $Server (if it doesn't exist)..."
sqlcmd -S $Server -E -b -Q "IF DB_ID('$Database') IS NULL CREATE DATABASE [$Database];"
if ($LASTEXITCODE -ne 0) { throw "Could not connect to $Server or create the database." }

# Stop if the tables are already there, so we never run CREATE TABLE twice.
$exists = sqlcmd -S $Server -E -d $Database -h -1 -W -b -Q "SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID('dbo.Roles') IS NULL THEN 0 ELSE 1 END;"
if ($exists.Trim() -eq "1") {
    Write-Host "Tables already exist. Nothing to do. (Drop the database first to start fresh.)"
    exit 0
}

Get-ChildItem $schemaFolder -Filter "*.sql" | Sort-Object Name | ForEach-Object {
    Write-Host "  running $($_.Name)"
    sqlcmd -S $Server -E -d $Database -b -i $_.FullName
    if ($LASTEXITCODE -ne 0) { throw "Failed while running $($_.Name)" }
}

Write-Host "Done. Start the API (F5); in Development it adds test data automatically."

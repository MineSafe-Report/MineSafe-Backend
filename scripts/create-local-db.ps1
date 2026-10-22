# Creates the local development database from the SQL scripts in database/schema.
# It doesn't change the scripts, it only runs them in the right order.
#
# Usage (from the repo folder, in PowerShell):
#     ./scripts/create-local-db.ps1
#
# Needs the sqlcmd tool (included with SQL Server and SSMS, or: winget install sqlcmd).
# Without sqlcmd: open SSMS, connect to (localdb)\MSSQLLocalDB, create a database called
# MineSafetyDb, then run each file in database/schema in the order listed below.

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

# The tables must be created in this order, because each one can only reference tables
# that already exist (for example AccountRoles needs Accounts and Roles first).
$tables = @(
    "Roles", "IncidentTypes", "ReportStatuses", "Locations",
    "Accounts", "AccountRoles", "AccountLocations",
    "Reports", "ReportAttachments", "ReportReviews", "ReportAuditLog", "Notifications"
)

foreach ($table in $tables) {
    $file = Join-Path $schemaFolder "$table.sql"
    Write-Host "  running $table.sql"
    # -I turns on QUOTED_IDENTIFIER, which SQL Server needs for the filtered indexes.
    sqlcmd -S $Server -E -d $Database -b -I -i $file
    if ($LASTEXITCODE -ne 0) { throw "Failed while running $table.sql" }
}

Write-Host "Done. Start the API (F5); in Development it adds test data automatically."

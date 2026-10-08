<#
  READ-ONLY. Compares the live Azure database with the project's migrations. Changes nothing.

      powershell -ExecutionPolicy Bypass -File .\Misc\Check-Azure.ps1

  The password is read from Web.Release.config and never printed.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$m = [regex]::Match([IO.File]::ReadAllText("$root\Web.Release.config"), 'connectionString="(Server=tcp:[^"]+)"')
$azure = $m.Groups[1].Value
function Query($sql) {
    $c = New-Object System.Data.SqlClient.SqlConnection $azure; $c.Open()
    $cmd = $c.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 120
    $t = New-Object System.Data.DataTable; $t.Load($cmd.ExecuteReader()); $c.Close(); return $t
}
$tables  = @{}; Query "SELECT name FROM sys.tables" | ForEach-Object { $tables[$_.name] = $true }
$columns = @{}; Query "SELECT t.name t, c.name c FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id" | ForEach-Object { $columns["$($_.t).$($_.c)"] = $true }

"Applied migrations recorded in Azure:"
Query 'SELECT MigrationId, ProductVersion FROM __MigrationHistory ORDER BY MigrationId' | ForEach-Object { "   $($_.MigrationId)   (EF $($_.ProductVersion))" }
"Tables in Azure: $($tables.Count)"
""
$files = Get-ChildItem "$root\Migrations" -Filter '*_*.cs' | Where-Object { $_.Name -notmatch 'Designer' -and $_.Name -ne 'Configuration.cs' } | Sort-Object Name
foreach ($f in $files) {
    $src = [IO.File]::ReadAllText($f.FullName)
    $up  = $src.Substring(0, [Math]::Max(0, $src.IndexOf('public override void Down')))
    $newTables = [regex]::Matches($up, 'CreateTable\(\s*"dbo\.(\w+)"') | ForEach-Object { $_.Groups[1].Value }
    $newCols   = [regex]::Matches($up, 'AddColumn\("dbo\.(\w+)",\s*"(\w+)"')  | ForEach-Object { "$($_.Groups[1].Value).$($_.Groups[2].Value)" }
    $tHave = @($newTables | Where-Object { $tables.ContainsKey($_) }); $cHave = @($newCols | Where-Object { $columns.ContainsKey($_) })
    "{0}" -f $f.BaseName
    "   creates {0} tables, {1} of them ALREADY exist in Azure" -f @($newTables).Count, $tHave.Count
    "   adds {0} columns, {1} of them ALREADY exist in Azure" -f @($newCols).Count, $cHave.Count
    $missingT = @($newTables | Where-Object { -not $tables.ContainsKey($_) }); if ($missingT.Count) { "   tables missing in Azure: " + ($missingT -join ', ') }
    $missingC = @($newCols   | Where-Object { -not $columns.ContainsKey($_) }); if ($missingC.Count) { "   columns missing in Azure: " + ($missingC -join ', ') }
}
""
"Row counts:"
Query 'SELECT (SELECT COUNT(*) FROM AppUsers) users,(SELECT COUNT(*) FROM EventVenues) venues,(SELECT COUNT(*) FROM EventMenuTemplates) templates,(SELECT COUNT(*) FROM MenuItems) meals,(SELECT COUNT(*) FROM Products) products,(SELECT COUNT(*) FROM Assets) assets,(SELECT COUNT(*) FROM Students) students' | Format-List | Out-String
"Done. Paste everything above to Claude (it contains no password)."

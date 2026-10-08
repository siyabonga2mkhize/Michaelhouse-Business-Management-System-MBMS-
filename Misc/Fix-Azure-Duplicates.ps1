<#
  Removes the duplicate transport@michaelhouse.co.za accounts the earlier seed created on the live
  database, keeping the oldest one. It deletes nothing else. Run it yourself:

      powershell -ExecutionPolicy Bypass -File .\Misc\Fix-Azure-Duplicates.ps1

  Shows what it will remove and asks you to type FIX first. The password is read from
  Web.Release.config and never printed.
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
function Exec($sql) {
    $c = New-Object System.Data.SqlClient.SqlConnection $azure; $c.Open()
    $cmd = $c.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 120
    $n = $cmd.ExecuteNonQuery(); $c.Close(); return $n
}
$sel = "SELECT UserId, Email, Role FROM AppUsers WHERE Email='transport@michaelhouse.co.za' ORDER BY UserId"
"transport@michaelhouse.co.za accounts on Azure now:"
$rows = @(Query $sel); $rows | Format-Table UserId, Email, Role -AutoSize | Out-String
if ($rows.Count -le 1) { 'Only one account: nothing to fix.'; exit 0 }
"Will KEEP the oldest account (UserId $($rows[0].UserId)) and DELETE the other $($rows.Count - 1) copies (same email, created by the seed bug)."
if ((Read-Host 'Type FIX to continue, anything else to stop') -ne 'FIX') { 'Stopped. Nothing changed.'; exit 0 }
try { $n = Exec "DELETE FROM AppUsers WHERE Email='transport@michaelhouse.co.za' AND UserId > (SELECT MIN(UserId) FROM AppUsers WHERE Email='transport@michaelhouse.co.za')"; "Deleted $n duplicate account(s)." }
catch { "Could not delete (something may reference them): $($_.Exception.Message)"; exit 1 }
Query $sel | Format-Table -AutoSize | Out-String
'Done. Paste the output to Claude.'

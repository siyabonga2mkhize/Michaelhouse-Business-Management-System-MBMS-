<#
  Seeds the LIVE Azure database (michealhousedatabase) with the meal library, recipes,
  students and the "Founders' Day Buffet" demo feast event. Run it yourself:

      cd C:\MSMS-UC19\Michaelhouse-Business-Management-System-MBMS-
      powershell -ExecutionPolicy Bypass -File .\Misc\Seed-Azure.ps1

  - The database password is read from Web.Release.config on your PC and is never printed.
  - It asks you to type a word before each step that writes to the live database.
  - The seed only ADDS rows that are missing. It does not delete anything.
  - Web.config is switched to Azure for a few minutes (to run the demo-event page locally)
    and ALWAYS put back afterwards, even if something fails.
  Needs: your IP allowed in the SQL server firewall (already done), Visual Studio 2022.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
$iis     = 'C:\Program Files\IIS Express\iisexpress.exe'
foreach ($p in $msbuild, $iis) { if (-not (Test-Path $p)) { throw "Missing: $p" } }

# --- connection string (never printed) -------------------------------------------------
$m = [regex]::Match([IO.File]::ReadAllText("$root\Web.Release.config"), 'connectionString="(Server=tcp:[^"]+)"')
if (-not $m.Success) { throw 'Azure connection string not found in Web.Release.config' }
$azure = $m.Groups[1].Value
$b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $azure
Write-Host "Target: server $($b.DataSource), database $($b.InitialCatalog), user $($b.UserID)  (password hidden)" -ForegroundColor Cyan

function Query($sql) {
    $c = New-Object System.Data.SqlClient.SqlConnection $azure
    $c.Open(); $cmd = $c.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 120
    $t = New-Object System.Data.DataTable; $t.Load($cmd.ExecuteReader()); $c.Close(); return $t
}
function Confirm($word, $what) {
    $a = Read-Host "`n$what`nType $word to continue, anything else to stop"
    if ($a -ne $word) { Write-Host 'Stopped. Nothing further was changed.' -ForegroundColor Yellow; exit 0 }
}

# --- 1. read-only check ----------------------------------------------------------------
Write-Host "`n[1/4] Read-only check of the live database..." -ForegroundColor Green
try { $applied = @(Query 'SELECT MigrationId FROM __MigrationHistory ORDER BY MigrationId' | ForEach-Object { $_.MigrationId }) }
catch { Write-Host "Could not connect/read: $($_.Exception.Message)" -ForegroundColor Red; Write-Host 'If it mentions the firewall, add your IP under the SQL server > Networking in the portal.'; exit 1 }
Write-Host "Migration history rows on Azure: $($applied.Count) (the history can be out of step with the real tables; this script never runs migrations)."
$counts = Query 'SELECT (SELECT COUNT(*) FROM AppUsers) users,(SELECT COUNT(*) FROM MenuItems) meals,(SELECT COUNT(*) FROM Recipes) recipes,(SELECT COUNT(*) FROM Students) students,(SELECT COUNT(*) FROM CafeteriaEvents) events,(SELECT COUNT(*) FROM EventVenues) venues'
Write-Host ("Rows now: users={0} meals={1} recipes={2} students={3} events={4} venues={5}" -f $counts.users, $counts.meals, $counts.recipes, $counts.students, $counts.events, $counts.venues)
Confirm 'SEED' 'Run ONLY the seed on the LIVE database (no migrations, no schema changes; it first checks the schema matches the code and stops if not)?'

# --- 2. build + seed only ---------------------------------------------------------------
Write-Host "`n[2/4] Building..." -ForegroundColor Green
& $msbuild "$root\Michaelhouse.csproj" -p:Configuration=Debug -v:q -nologo -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host 'Checking the schema and running the seed on Azure (a few minutes)...' -ForegroundColor Green
. "$PSScriptRoot\SeedRunner.ps1"
$res = Invoke-MbmsSeed -ConnectionString $azure -BinDir "$root\bin"
Write-Host $res.Message -ForegroundColor $(if ($res.Ok) { 'Green' } else { 'Red' })
if (-not $res.Ok) { Write-Host 'Stopped. Paste this output to Claude.'; exit 1 }

# --- 3. demo feast event (the page only works on localhost, so run the site locally against Azure) ----
Confirm 'DEMO' "[3/4] Create the 'Founders' Day Buffet' demo event on the live database?"
$cfg = "$root\Web.config"; $bak = "$root\Web.config.before-azure-seed"
Copy-Item $cfg $bak -Force
$proc = $null
try {
    $xml = [IO.File]::ReadAllText($cfg)
    $local = [regex]::Match($xml, '<add name="MichaelHouse" connectionString="[^"]*"')
    if (-not $local.Success) { throw 'Could not find the MichaelHouse connection string in Web.config' }
    [IO.File]::WriteAllText($cfg, $xml.Replace($local.Value, '<add name="MichaelHouse" connectionString="' + $azure + '"'))
    $proc = Start-Process $iis -ArgumentList "/path:$root", '/port:8098', '/clr:v4.0' -PassThru -WindowStyle Hidden
    $base = 'http://localhost:8098'
    for ($i = 0; $i -lt 60; $i++) { try { $null = Invoke-WebRequest "$base/Account/Login" -UseBasicParsing -TimeoutSec 120; break } catch { Start-Sleep 3 } }
    $page = Invoke-WebRequest "$base/Account/Login" -SessionVariable s -UseBasicParsing -TimeoutSec 180
    $tok = [regex]::Match($page.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
    $null = Invoke-WebRequest "$base/Account/Login" -WebSession $s -Method Post -UseBasicParsing -TimeoutSec 180 -Body @{
        __RequestVerificationToken = $tok; Email = 'cafeteria@michaelhouse.co.za'; Password = 'Cafeteria@123' }
    Write-Host "`n--- Demo event result ---" -ForegroundColor Cyan
    (Invoke-WebRequest "$base/DevelopmentSeed/FeastPlanDemo" -WebSession $s -UseBasicParsing -TimeoutSec 600).Content
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    Copy-Item $bak $cfg -Force; Remove-Item $bak -Force
    Write-Host "`nWeb.config restored to your local (LocalDB) version." -ForegroundColor Green
}

# --- 4. report ------------------------------------------------------------------------
Write-Host "`n[4/4] Login emails on the live database (passwords are the seed ones in Migrations\Configuration.cs):" -ForegroundColor Green
Query 'SELECT Role, Email FROM AppUsers ORDER BY Role, Email' | Format-Table -AutoSize | Out-String -Width 200
Write-Host 'Dishes with recipes in the library:' -ForegroundColor Green
(Query 'SELECT COUNT(*) n FROM MenuItems WHERE IsActive=1 AND RecipeId IS NOT NULL').n
Write-Host "`nDone. Copy everything above (it contains no password) and paste it to Claude." -ForegroundColor Cyan

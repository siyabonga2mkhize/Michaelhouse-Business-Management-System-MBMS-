# --- CONFIG ---
$Repo = "Siyabonga2Mkhize/Michaelhouse-Business-Management-System-MBMS-"
$CsvPath = "Issues.csv"

# --- Read raw lines and fix header if needed ---
$lines = Get-Content -Path $CsvPath -Encoding UTF8
if ($lines[0] -eq '"title,body,milestone,labels"') {
    $lines[0] = 'title,body,milestone,labels'
}
$csvContent = $lines -join "`r`n"

# --- Parse CSV ---
$csv = $csvContent | ConvertFrom-Csv -Delimiter ','
if (-not $csv) {
    Write-Host "CSV is empty or could not be parsed." -ForegroundColor Red
    exit
}

Write-Host "Parsed $($csv.Count) rows." -ForegroundColor Green
Write-Host "First title: '$($csv[0].title)'" -ForegroundColor Yellow
Write-Host "First milestone: '$($csv[0].milestone)'" -ForegroundColor Yellow

# --- Collect unique milestones and labels ---
$milestones = $csv | ForEach-Object { $_.milestone } | Where-Object { $_ -and $_.Trim() -ne "" } | Select-Object -Unique
$labelsList = $csv | ForEach-Object { $_.labels } | Where-Object { $_ -and $_.Trim() -ne "" } | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Select-Object -Unique

# --- Create milestones (if missing) ---
foreach ($m in $milestones) {
    Write-Host "Checking milestone: $m"
    $exists = gh api repos/$Repo/milestones --jq ".[] | select(.title == `"$m`") | .title"
    if (-not $exists) {
        Write-Host "Creating milestone: $m" -ForegroundColor Yellow
        gh api repos/$Repo/milestones -f title="$m" -f state="open" --method POST
    }
}

# --- Create labels (if missing) ---
foreach ($label in $labelsList) {
    Write-Host "Checking label: $label"
    $exists = gh api repos/$Repo/labels --jq ".[] | select(.name == `"$label`") | .name"
    if (-not $exists) {
        Write-Host "Creating label: $label" -ForegroundColor Yellow
        gh label create "$label" --repo $Repo --color "ededed" --description "Auto-created label" 2>$null
    }
}

# --- Import issues ---
$count = 0
foreach ($row in $csv) {
    $title = $row.title
    $body = $row.body
    $milestone = $row.milestone
    $labels = $row.labels

    if (-not $title -or $title.Trim() -eq "") {
        Write-Host "Skipping empty title." -ForegroundColor Gray
        continue
    }

    Write-Host "Creating: $title" -ForegroundColor Cyan
    $cmd = "gh issue create --title `"$title`" --body `"$body`" --repo `"$Repo`""
    if ($milestone -and $milestone.Trim() -ne "") { $cmd += " --milestone `"$milestone`"" }
   if ($labels -and $labels.Trim() -ne "") {
    $labelArray = $labels -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }
    if ($labelArray) {
        $labelStr = $labelArray -join ','
        $cmd += " --label `"$labelStr`""
    }
}
Invoke-Expression $cmd
    $count++
    Start-Sleep -Milliseconds 200
}

$cmdArgs = @(
    "issue", "create",
    "--title", $title,
    "--body", $body,
    "--repo", $Repo
)
if ($milestone -and $milestone.Trim() -ne "") { $cmdArgs += "--milestone", $milestone }
if ($labels -and $labels.Trim() -ne "") {
    $labelArray = $labels -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }
    if ($labelArray) {
        $cmdArgs += "--label", ($labelArray -join ',')
    }
}
& gh $cmdArgs

Write-Host "Done. Created $count issues." -ForegroundColor Green
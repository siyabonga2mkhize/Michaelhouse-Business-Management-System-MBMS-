# ============================================================
# Downloads the four dlib model files that face recognition
# (Services\DlibFaceRecognitionService.cs) needs, into
# App_Data\FaceModels. About 132 MB. Run once per computer:
#
#   Right-click this file in File Explorer -> "Run with PowerShell"
#   or in a terminal:  powershell -ExecutionPolicy Bypass -File .\Download-FaceModels.ps1
#
# The files are not in git (too big for GitHub).
# Source: https://github.com/ageitgey/face_recognition_models
# ============================================================

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$target = Join-Path $PSScriptRoot "App_Data\FaceModels"
New-Item -ItemType Directory -Force -Path $target | Out-Null

$base = "https://github.com/ageitgey/face_recognition_models/raw/master/face_recognition_models/models/"
$models = [ordered]@{
    "mmod_human_face_detector.dat"              = 729940
    "shape_predictor_5_face_landmarks.dat"      = 9150489
    "dlib_face_recognition_resnet_model_v1.dat" = 22466066
    "shape_predictor_68_face_landmarks.dat"     = 99693937
}

foreach ($name in $models.Keys) {
    $file = Join-Path $target $name
    $size = $models[$name]

    if ((Test-Path $file) -and ((Get-Item $file).Length -eq $size)) {
        Write-Host "Already downloaded: $name"
        continue
    }

    Write-Host ("Downloading {0} ({1:N1} MB)..." -f $name, ($size / 1MB))
    Invoke-WebRequest -Uri ($base + $name) -OutFile $file -UseBasicParsing

    if ((Get-Item $file).Length -ne $size) {
        throw "$name did not download completely. Please run the script again."
    }
}

Write-Host ""
Write-Host "Done. Face models are in $target" -ForegroundColor Green
Write-Host "Restart IIS Express (system tray icon -> Exit), then start the website with F5."

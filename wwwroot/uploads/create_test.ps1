# Создадим простое тестовое PNG изображение
$pngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAFUlEQVR42mNgGAWjYBSMglEwCkYBACAfBRyKqZNgAAAAAElFTkSuQmCC"
$pngBytes = [System.Convert]::FromBase64String($pngBase64)
[System.IO.File]::WriteAllBytes("$PSScriptRoot\test_image.png", $pngBytes)
Write-Host "Created test image at $PSScriptRoot\test_image.png"

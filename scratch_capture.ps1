Add-Type -Path "src\RpgMacroStudio\bin\Debug\net8.0-windows\RpgMacroStudio.Core.dll"
Add-Type -Path "src\RpgMacroStudio\bin\Debug\net8.0-windows\RpgMacroStudio.dll"

[RpgMacroHarness.Data.AppSettings]::Current.IsDarkMode = $true
$form = New-Object RpgMacroHarness.UI.SettingsForm
$form.Show()
[System.Windows.Forms.Application]::DoEvents()

$bmp = New-Object System.Drawing.Bitmap($form.Width, $form.Height)
$rect = New-Object System.Drawing.Rectangle(0, 0, $form.Width, $form.Height)
$form.DrawToBitmap($bmp, $rect)
$bmp.Save("test_settings_dark.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$form.Close()
$form.Dispose()

Write-Host "SUCCESS: test_settings_dark.png created"

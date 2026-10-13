<#
  本地自签名 + 签名（用 Windows 自带的 Set-AuthenticodeSignature，不需要 Windows SDK）

  用法（在仓库根目录）：
    powershell -NoProfile -File tools\sign-local.ps1
    powershell -NoProfile -File tools\sign-local.ps1 -PublishDir <发布目录>

  做的事：
    1. 当前用户证书库里没有就生成一张自签名代码签名证书（CN=DarkMode2 (self-signed)）
    2. 导出 darkmode2-codesign.cer 到发布目录（应用启动时会把它写进系统信任库）
    3. 给 DarkMode.Ui.exe / DarkMode.Ui.dll / DarkMode.Core.dll 签名
  注意：每次 dotnet publish 都会覆盖 exe，需要重新跑一次本脚本。
#>
param(
    [string]$PublishDir = "DarkMode.Ui\bin\x64\Release\net9.0-windows10.0.26100.0\win-x64\publish",
    [string]$Subject = "CN=DarkMode2 (self-signed)"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $PublishDir)) { throw "发布目录不存在: $PublishDir (先跑 dotnet publish)" }

$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Subject -and $_.HasPrivateKey } | Select-Object -First 1

if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $Subject -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(5)
    Write-Host "已生成自签名证书: $($cert.Thumbprint)"
} else {
    Write-Host "复用已有证书: $($cert.Thumbprint)"
}

$cerPath = Join-Path $PublishDir "darkmode2-codesign.cer"
Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null
Write-Host "证书已导出: $cerPath"

foreach ($name in @("DarkMode.Ui.exe", "DarkMode.Ui.dll", "DarkMode.Core.dll")) {
    $file = Join-Path $PublishDir $name
    if (-not (Test-Path $file)) { Write-Host "跳过(不存在): $name"; continue }
    $result = Set-AuthenticodeSignature -FilePath $file -Certificate $cert
    Write-Host ("已签名 {0} 状态={1}" -f $name, $result.Status)
}

[CmdletBinding()]
param([Parameter(Mandatory)][string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) { throw 'Executable fixture missing' }
$work = Join-Path $env:RUNNER_TEMP ('vcc-signing-regression-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$cert = $null
try {
  $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=VCC Office Temporary CI Test Only' -CertStoreLocation 'Cert:\CurrentUser\My' -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -NotAfter (Get-Date).AddDays(7)
  $pfx = Join-Path $work 'fixture.pfx'
  $pass = [Guid]::NewGuid().ToString('N')
  $secret = ConvertTo-SecureString $pass -AsPlainText -Force
  Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $secret | Out-Null
  $env:WINDOWS_SIGNING_PFX_PASSWORD = $pass
  $binary = Join-Path $work 'test-unsigned-copy.exe'
  Copy-Item -LiteralPath $ExecutablePath -Destination $binary
  $sdk = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Windows Kits\10\bin'
  $tool = Get-ChildItem -LiteralPath $sdk -Recurse -File -Filter signtool.exe | Where-Object { $_.FullName -match '[\\/]x64[\\/]signtool\.exe$' } | Sort-Object FullName -Descending | Select-Object -First 1
  if ($null -eq $tool) { throw 'signtool.exe not found on Windows runner' }
  & $tool.FullName sign /fd SHA256 /f $pfx /p $pass $binary
  if ($LASTEXITCODE -ne 0) { throw 'Fixture signing failed' }
  & (Join-Path $PSScriptRoot '..\..\scripts\verify-code-signature.ps1') -FilePath $binary -PfxPath $pfx -Mode SelfSignedTest

  $trustedRejected = $false
  try {
    & (Join-Path $PSScriptRoot '..\..\scripts\verify-code-signature.ps1') -FilePath $binary -PfxPath $pfx -Mode TrustedRelease
  } catch { $trustedRejected = $true }
  if (-not $trustedRejected) { throw 'Self-signed certificate incorrectly passed TrustedRelease' }

  $bytes = [IO.File]::ReadAllBytes($binary)
  $offset = [Math]::Min(1024, $bytes.Length - 1)
  $bytes[$offset] = $bytes[$offset] -bxor 1
  [IO.File]::WriteAllBytes($binary, $bytes)
  $tamperRejected = $false
  try {
    & (Join-Path $PSScriptRoot '..\..\scripts\verify-code-signature.ps1') -FilePath $binary -PfxPath $pfx -Mode SelfSignedTest
  } catch { $tamperRejected = $true }
  if (-not $tamperRejected) { throw 'A modified signed executable was accepted' }
  Write-Host 'PASS self-signed signature, trusted-release rejection and tamper rejection'
} finally {
  Remove-Item Env:WINDOWS_SIGNING_PFX_PASSWORD -ErrorAction SilentlyContinue
  if ($null -ne $cert) { Remove-Item -LiteralPath ('Cert:\CurrentUser\My\' + $cert.Thumbprint) -Force -ErrorAction SilentlyContinue }
  Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

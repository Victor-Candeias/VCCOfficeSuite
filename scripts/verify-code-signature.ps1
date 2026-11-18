[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$FilePath,
  [Parameter(Mandatory)][string]$PfxPath,
  [ValidateSet('TrustedRelease','SelfSignedTest')][string]$Mode = 'TrustedRelease'
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) { throw 'Binary not found' }
if (-not (Test-Path -LiteralPath $PfxPath -PathType Leaf)) { throw 'PFX not found' }
$password = [Environment]::GetEnvironmentVariable('WINDOWS_SIGNING_PFX_PASSWORD','Process')
if ([string]::IsNullOrWhiteSpace($password)) { throw 'PFX password is missing' }
$flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
$expected = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($PfxPath,$password,$flags)
try {
  $now = Get-Date
  if ($now -lt $expected.NotBefore -or $now -gt $expected.NotAfter) {
    throw 'Signing certificate is expired or not yet valid'
  }
  $eku = $expected.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' } | Select-Object -First 1
  if ($null -eq $eku) { throw 'Code Signing EKU is missing' }
  $codeSigning = [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]$eku
  $oids = @($codeSigning.EnhancedKeyUsages | ForEach-Object { $_.Value })
  if ('1.3.6.1.5.5.7.3.3' -notin $oids) { throw 'Certificate cannot be used for Code Signing' }
  $sig = Get-AuthenticodeSignature -LiteralPath $FilePath
  if ($null -eq $sig.SignerCertificate) { throw 'No Authenticode signer' }
  if ($sig.SignerCertificate.Thumbprint -ne $expected.Thumbprint) { throw 'Unexpected signer thumbprint' }
  if ($Mode -eq 'TrustedRelease') {
    if ($sig.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
      throw "Public trusted signature required; status: $($sig.Status)"
    }
  } else {
    if ($expected.Subject -ne $expected.Issuer) { throw 'SelfSignedTest requires a self-issued certificate' }
    $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
    try {
      $chain.ChainPolicy.TrustMode = [System.Security.Cryptography.X509Certificates.X509ChainTrustMode]::CustomRootTrust
      $chain.ChainPolicy.CustomTrustStore.Add($expected)
      $chain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
      $chain.ChainPolicy.ApplicationPolicy.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
      if (-not $chain.Build($sig.SignerCertificate) -or $chain.ChainElements.Count -ne 1) {
        throw 'Self-signed chain could not be verified with isolated custom trust'
      }
    } finally { $chain.Dispose() }
    # Authenticode rejects modified PE hashes as HashMismatch, which must not be allowed.
    if ($sig.Status -notin @([System.Management.Automation.SignatureStatus]::Valid,[System.Management.Automation.SignatureStatus]::NotTrusted)) {
      throw "Authenticode signature/hash failed: $($sig.Status)"
    }
  }
  Write-Host "PASS $Mode signature verification for $([IO.Path]::GetFileName($FilePath))"
} finally { $expected.Dispose() }

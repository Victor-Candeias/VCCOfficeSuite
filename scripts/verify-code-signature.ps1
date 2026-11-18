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
  if ($Mode -eq 'TrustedRelease') {
    # Never classify a self-issued PFX as a publicly trusted release signer,
    # even if someone installed that certificate in the local Windows root store.
    if ($expected.Subject -eq $expected.Issuer) {
      throw 'Public release rejects self-issued/self-signed code signing certificates'
    }
    $sig = Get-AuthenticodeSignature -LiteralPath $FilePath
    if ($null -eq $sig.SignerCertificate -or $sig.SignerCertificate.Thumbprint -ne $expected.Thumbprint) {
      throw 'Signer thumbprint does not match the expected PFX'
    }
    if ($sig.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
      throw "TrustedRelease requires a publicly trusted signature; status: $($sig.Status)"
    }
  } else {
    if ($expected.Subject -ne $expected.Issuer) { throw 'SelfSignedTest requires a self-issued signer' }
    $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
    try {
      $chain.ChainPolicy.TrustMode = [System.Security.Cryptography.X509Certificates.X509ChainTrustMode]::CustomRootTrust
      $chain.ChainPolicy.CustomTrustStore.Add($expected)
      $chain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
      $chain.ChainPolicy.ApplicationPolicy.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
      if (-not $chain.Build($expected) -or $chain.ChainElements.Count -ne 1) {
        throw 'Isolated self-signed chain verification failed'
      }
    } finally { $chain.Dispose() }

    # The Windows trust provider can reject a self-signed issuer even when the
    # embedded PE signature is cryptographically correct. Never accept arbitrary
    # UnknownError: accept ONLY the specific untrusted-root trust failure after
    # pinning the signer and validating the certificate in an isolated chain.
    # The Windows regression deliberately tampers with a signed PE to ensure
    # its digest failure is NOT treated as an untrusted-root-only error.
    $sig = Get-AuthenticodeSignature -LiteralPath $FilePath
    if ($null -eq $sig.SignerCertificate -or $sig.SignerCertificate.Thumbprint -ne $expected.Thumbprint) {
      throw 'Signer thumbprint does not match the expected PFX'
    }
    $rootTrustError = $sig.StatusMessage -match '(?i)(root certificate.*not trusted|root certificate.*not.*trust|CERT_E_UNTRUSTEDROOT|0x800b0109)'
    if ($sig.Status -ne [System.Management.Automation.SignatureStatus]::Valid -and
        $sig.Status -ne [System.Management.Automation.SignatureStatus]::NotTrusted -and
        -not ($sig.Status -eq [System.Management.Automation.SignatureStatus]::UnknownError -and $rootTrustError)) {
      throw "Authenticode signer/digest verification failed: $($sig.Status) - $($sig.StatusMessage)"
    }
    Write-Host "VERIFY: isolated chain and pinned signer accepted; Windows trust status: $($sig.Status)"

  }
  Write-Host "PASS $Mode signature verification for $([IO.Path]::GetFileName($FilePath))"
} finally { $expected.Dispose() }

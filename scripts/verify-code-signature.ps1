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

    # Windows Authenticode/WinVerifyTrust may return UnknownError for an untrusted
    # root, which cannot safely be interpreted as proof of a valid PE hash.
    # On a short-lived, dedicated Actions runner, temporarily trust ONLY this
    # pinned public certificate, verify using Windows, and remove it immediately.
    Write-Host 'VERIFY: opening temporary current-user root store on ephemeral runner'
    $root = [System.Security.Cryptography.X509Certificates.X509Store]::new(
      [System.Security.Cryptography.X509Certificates.StoreName]::Root,
      [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
    $added = $false
    $root.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    try {
      $already = $root.Certificates.Find(
        [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
        $expected.Thumbprint, $false)
      if ($already.Count -ne 0) {
        throw 'Self-signed test refuses to modify a root certificate already trusted by this runner'
      }
      Write-Host 'VERIFY: temporarily trusting pinned test certificate'
      $root.Add($expected)
      $added = $true
      Write-Host 'VERIFY: checking Authenticode signer and PE digest'
      $sig = Get-AuthenticodeSignature -LiteralPath $FilePath
      if ($null -eq $sig.SignerCertificate -or $sig.SignerCertificate.Thumbprint -ne $expected.Thumbprint) {
        throw 'Signer thumbprint does not match the expected PFX'
      }
      if ($sig.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Authenticode PE signature/hash failed in isolated test: $($sig.Status)"
      }
    } finally {
      if ($added) { Write-Host 'VERIFY: removing temporary test trust'; $root.Remove($expected) }
      $root.Close()
      $root.Dispose()
    }
  }
  Write-Host "PASS $Mode signature verification for $([IO.Path]::GetFileName($FilePath))"
} finally { $expected.Dispose() }

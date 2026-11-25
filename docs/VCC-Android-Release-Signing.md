# VCC Android — APK Release, versão e assinatura

Associated issue: #574. This work does not imply VCC-Android-02 is implemented.

## Security and release policy
- Never commit a keystore, private key, password, generated APK or secret values.
- Use a **new Android signing keystore**; the Windows Authenticode certificate cannot replace it.
- Back up the keystore in two secure offline locations: loss can prevent updates to installed APKs.
- Only release from the trusted **main** branch. Protect the `android-v*` tag namespace or approve tag pushes from release maintainers only.
- Set up a protected GitHub Actions environment before distributing signed builds if additional approvals are required.
- A signed package is not equivalent to a device/emulator smoke test.

## Create a private keystore
Requires JDK / keytool. Run locally, not in CI, so the private key is never uploaded to Actions logs.

```bash
keytool -genkeypair -v -keystore vcc-office-android.keystore -storetype PKCS12 -alias vcc-office -keyalg RSA -keysize 3072 -validity 10000
```

Choose a strong, unique password; supply a key password if requested.

Convert the **keystore bytes**, not a certificate/public key, to a single-line Base64 value:

- PowerShell: `[Convert]::ToBase64String([IO.File]::ReadAllBytes("vcc-office-android.keystore")) | Set-Clipboard`
- Linux: `base64 -w 0 vcc-office-android.keystore`
- macOS: `base64 < vcc-office-android.keystore | tr -d '\\n'`

Configure GitHub repository Settings > Secrets and variables > Actions:
- `ANDROID_KEYSTORE_BASE64`: base64 of private keystore
- `ANDROID_KEYSTORE_PASSWORD`: keystore password
- `ANDROID_KEY_ALIAS`: `vcc-office`
- `ANDROID_KEY_PASSWORD`: key password (for PKCS12 commonly the same as store password)

Keep a separate secure backup of the keystore/passwords. Do not paste them into GitHub issues or chat.

## Versioning
Use MAJOR.MINOR.PATCH (for example, 0.1.0); the workflow maps this deterministically to an Android integer versionCode:
`major * 1000000 + minor * 1000 + patch`.

Example: `0.1.0` -> `1000`, `0.1.1` -> `1001`, `0.2.0` -> `2000`.

Versions must increase for each distributed update. The appId stays fixed at `pt.vcc.officesuite` and **the same keystore** must sign all upgrades.

## Build and release
- Pull requests build `Release` **without signing secrets**, exercising Android restore/build safely.
- On main: Actions > Android Release > Run workflow > choose version (e.g. `0.1.0`). This produces an APK with verified v2 signature and SHA256 checksum in Actions artifacts (30-day retention).
- Optional: push a protected tag `android-v0.1.0` on the verified main commit to trigger the same workflow.
- Do not reissue the same versionCode for an updated application: increment patch or minor first.

The Release workflow checks for all four secrets and aborts if missing. No secrets are required on pull requests.
Verify actual installation, startup, background/rotation and offline use on a device/emulator **before** considering VCC-Android-01 completely validated.

## Output
`VCCOfficeSuite-Android-v0.1.0-Release.apk` and its `.sha256` file in the GitHub Actions artifact `VCCOfficeSuite-Android-v0.1.0-Release`.

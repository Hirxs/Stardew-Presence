$cert = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert | Select-Object -First 1
if ($null -eq $cert) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=StardewPresenceDev" -CertStoreLocation Cert:\CurrentUser\My
}

# Trust certificate locally
$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
$rootStore.Open("ReadWrite")
$rootStore.Add($cert)
$rootStore.Close()

$pubStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPublisher", "CurrentUser")
$pubStore.Open("ReadWrite")
$pubStore.Add($cert)
$pubStore.Close()

Write-Host "Cert Thumbprint: $($cert.Thumbprint)"

$projectDir = (Resolve-Path "$PSScriptRoot\..").Path
$dll = Join-Path $projectDir "bin\Release\net6.0\StardewPresence.dll"
$sig = Set-AuthenticodeSignature -Certificate $cert -FilePath $dll
Write-Host "Signature Status: $($sig.Status)"

#This script generates a new cert, export to a file, then import back to cert store \My so can be used for signing.
#Make sure to delete all existing cert from store before you start.

#List all available certs 
#powershell Get-ChildItem Cert:\CurrentUser\My\
#To remove the cert
#D:\Workspace\InstaFind\Setup>powershell Remove-Item
#cmdlet Remove-Item at command pipeline position 1
#Supply values for the following parameters:
#Path[0]: Cert:\CurrentUser\My\A0DB22A19CB46C678E171B45D646EFDE7F733479
#Path[1]: Cert:\CurrentUser\My\73B1D065C9699DE5D96DE10CEDD172ADC7F89611
#Path[2]:

# Create a self-signed certificate in the local machine personal certificate store and store the result in the $cert variable.
$cert = New-SelfSignedCertificate `
-Subject 'E=Hi@z2a.info,CN=Naren Chandel' `
-Certstorelocation Cert:\CurrentUser\My `
-Type CodeSigningCert `
-FriendlyName 'Pitara Code Signing Certficate' `
-Provider 'Microsoft Enhanced RSA and AES Cryptographic Provider' `
-KeyExportPolicy Exportable `
-KeyUsage DigitalSignature `
-DnsName www.GetPitara.com `

# Display the new certificate properties
$cert | Format-List -Property *

# Export to file
Export-Certificate -Cert (Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert)[0] -FilePath pitara_code_signing.crt

#Import as trusted publisher
Import-Certificate -FilePath .\pitara_code_signing.crt -Cert Cert:\CurrentUser\TrustedPublisher

# Import as Root cert
powershell Import-Certificate -FilePath .\pitara_code_signing.crt -Cert Cert:\CurrentUser\Root

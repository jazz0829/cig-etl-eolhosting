param(
[Parameter(Mandatory=$true)]
[string]$expectedHostname,
[Parameter(Mandatory=$true)]
[string]$rootUrl,
[Parameter(Mandatory=$false)]
[string]$username,
[Parameter(Mandatory=$false)]
[string]$password
)

$serviceUrl = $rootUrl -replace "\+", $expectedHostname
$fullUrl = $serviceUrl + "/GetServiceInfo"
"Calling $fullUrl ..."

[xml]$result = $null;

if($username -and $password)
{
    $secpasswd = ConvertTo-SecureString $password -AsPlainText -Force
    $credential = New-Object System.Management.Automation.PSCredential ($username, $secpasswd)
    $result = Invoke-RestMethod ($fullUrl) -Credential $credential
}
else
{
    $result = Invoke-RestMethod ($fullUrl) -UseDefaultCredentials
}


if($result -eq $null)
{
    throw "Web call failed!"
}

#print result on screen
$StringWriter = New-Object System.IO.StringWriter;
$XmlWriter = New-Object System.Xml.XmlTextWriter $StringWriter;
$XmlWriter.Formatting = "indented";
$result.WriteTo($XmlWriter);
$XmlWriter.Flush();
$StringWriter.Flush();
$StringWriter.ToString();
###

"Verifying content..."
$hostName = $result.EnvironmentInfoResult.HostName
$version = $result.EnvironmentInfoResult.AssemblyVersion
"Deployment verified, all good!"
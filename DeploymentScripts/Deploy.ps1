param(
[Parameter(Mandatory=$true)]
[string]$serviceName,
[Parameter(Mandatory=$true)]
[string]$sourcePath,
[Parameter(Mandatory=$true)]
[string]$destinationPath,
[Parameter(Mandatory=$true)]
[string[]]$executableName,
[Parameter(Mandatory=$false)]
[string]$serviceAccount,
[Parameter(Mandatory=$true)]
[string]$serviceApiUri
)

"Deployment started"
"serviceName: $serviceName"
"sourcePath: $sourcePath"
"destinationPath: $destinationPath"
"serviceAccount: $serviceAccount"
"Check if service $serviceName exists and delete it"

function ManualyDeleteService
{
    "Unable to uninstall the service. Manually deleting it. Checking if registry key still exists."
            
    "Deleting service $serviceName"
	$service.Delete()
            
    $keyPath = Get-ItemProperty "hklm:\SYSTEM\CurrentControlSet\Services\$serviceName" -ErrorAction Ignore

    if($keyPath)
    {
        "Registry key for service still exists, removing registry key."
        Remove-Item -Path $keyPath -Recurse
    }
    else
    {
        "Registry key for the service does not exist."        
    }
}

function StopProcess($executable)
{
    $processName = $executable -replace ".exe", ""
    $serviceProc = Get-Process -Name $processName -ErrorAction Ignore

    if($serviceProc)
    {
        "Process is still alive: $processName. Stoping."
        Stop-Process -InputObject $serviceProc -Force 
    }
}

function Stop-ServiceWithTimeout ([string] $name, [int] $timeoutSeconds) {
    $timespan = New-Object -TypeName System.Timespan -ArgumentList 0,0,$timeoutSeconds
    $svc = Get-Service -Name $name
    if ($svc -eq $null) { return $false }
    if ($svc.Status -eq [ServiceProcess.ServiceControllerStatus]::Stopped) { return $true }        
        $svc.Stop()
    try {
        $svc.WaitForStatus([ServiceProcess.ServiceControllerStatus]::Stopped, $timespan)
    }
    catch [ServiceProcess.TimeoutException] {
        Write-Verbose "Timeout stopping service $($svc.Name)"
        return $false
    }
    return $true
}

$subinacl = "C:\Program Files (x86)\Windows Resource Kits\Tools\subinacl.exe"
	
try{
	$service = Get-WmiObject -Class Win32_Service -Filter "Name='$serviceName'"
	if($service)
	{					
        try{			
            "Stoping service $serviceName. Timeout 30 seconds."
            Stop-ServiceWithTimeout $serviceName 30
        }catch{
            "Unable to stop the service: $serviceName." 
        }

        "Check if the process is still running"

        foreach($executable in $executableName)
        {
            StopProcess($executable)
        }

        "Uninstalling service."
       
        $proc = $null;
        try 
        {
            $serviceExecutable = $executableName[0] -replace ".exe", ""
            $proc = Start-Process $serviceExecutable -ArgumentList "uninstall" -Verb runAs -Wait -PassThru -ErrorAction Stop    
        } 
        catch [Exception]
        {
            "Error occured while trying to uninstall the service $serviceName"
             echo $_.Exception | Format-List -force
            ManualyDeleteService
        }

        if($proc -ne $null -and $proc.ExitCode -ne 0)
        {
            ManualyDeleteService
        }
       
        if($serviceAccount){
            "Revoking permissions to $serviceAccount for service $serviceName"
            $arguments1 = "/service $serviceName /deny=$serviceAccount=F >null"		
		    Invoke-Expression "& `"$subinacl`" $arguments1"		
        }
         
        "Deleting ACL for url: $serviceApiUri"
		& netsh http delete urlacl url=$serviceApiUri 
	}
	else{
		"Service not found."
        "Check if processes are still running."
        foreach($executable in $executableName)
        {
            StopProcess($executable)
        }
	}
}
catch [Exception]
{    
	"Exception occured when trying to remove current version of the service."
    echo $_.Exception | Format-List -force
    & netsh http delete urlacl url=$serviceApiUri 
}

if(Test-Path $destinationPath)
{
    "Waiting for 15 seconds before attempting to delete old files."
    Start-Sleep -s 15
    "Deleting destination folder content"
    Remove-Item $destinationPath -recurse -force
}

"Copying artifacts to destination folder"
Copy-Item -Path $sourcePath -Destination $destinationPath  -Recurse -force

$executable = $destinationPath + $executableName[0]
if((Test-Path($executable)) -eq $true)
{
    "Installing service."
    Start-Process $executable -ArgumentList "install" -Verb runAs -Wait -PassThru -ErrorAction Stop        

    "Reserving the service URL for the service account"
    & netsh http add urlacl url=$serviceApiUri user=$serviceAccount	    

    if($serviceAccount) {
        "Service account provided. Granting permissions to $serviceAccount for service $serviceName"
        $arguments2 = "/service $serviceName /grant=$serviceAccount=F >null"	
        Invoke-Expression "& `"$subinacl`" $arguments2"
    }

    "Starting service."
    Start-Service $serviceName

    "Requesting service information"
    $service = Get-WmiObject -Class Win32_Service -Filter "Name='$serviceName'"
    if($service -ne $null)
    {
        $state = $service.State
        "Checking service status: $state"
        if($state -ne 'Running')
        {
            throw 'Service not started!'
        }
        "Service Started" 
        "Deployment completed"
    }
    else{
        throw "Service not found"
    }   
}
else{
		throw "Service executable not found!"
}
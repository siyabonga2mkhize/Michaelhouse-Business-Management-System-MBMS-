<#
  Adds a wildcard IIS Express binding (if applicationhost.config exists in the .vs folder)
  and registers a URL ACL + firewall rule so IIS Express accepts requests from the LAN.

  Usage (run as Administrator from the solution root):
	.\scripts\add-iisexpress-wildcard.ps1

  The script will:
  - add a URL ACL for http://+:65133/ using netsh
  - create a firewall rule to open TCP port 65133
  - if a solution-scoped applicationhost.config exists under .vs\<SolutionName>\config, it will add a binding
	<binding protocol="http" bindingInformation="*:65133:*" /> to the first <site> if not already present.
#>

param(
	[int]$Port = 65133
)

function Ensure-Admin {
	$current = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
	if (-not $current.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
		Write-Error "This script must be run as Administrator. Right-click PowerShell and choose 'Run as Administrator'."
		exit 1
	}
}

Ensure-Admin

$url = "http://+:$Port/"
Write-Output "Adding URL ACL: $url"
try {
	& netsh http add urlacl url=$url user=Everyone | Out-Null
	Write-Output "URL ACL added (or already exists)."
} catch {
	Write-Warning "Failed to add URL ACL via netsh. You may need to run this command manually as Admin: netsh http add urlacl url=$url user=Everyone"
}

$ruleName = "IISExpress $Port"
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
	Write-Output "Creating firewall rule to allow TCP port $Port"
	try {
		New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port | Out-Null
		Write-Output "Firewall rule created."
	} catch {
		Write-Warning "Failed to create firewall rule. You may need to run this command manually as Admin: New-NetFirewallRule -DisplayName \"$ruleName\" -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port"
	}
} else {
	Write-Output "Firewall rule '$ruleName' already exists."
}

# Attempt to find a solution-scoped applicationhost.config in .vs\<SolutionName>\config
$sln = Get-ChildItem -Path . -Filter *.sln | Select-Object -First 1
if ($null -eq $sln) {
	Write-Warning "No .sln file found in current directory. Skipping automatic applicationhost.config modification."
	exit 0
}

$solutionName = [System.IO.Path]::GetFileNameWithoutExtension($sln.Name)
$appHostPath = Join-Path -Path ".vs" -ChildPath "$solutionName\config\applicationhost.config"

if (-not (Test-Path $appHostPath)) {
	Write-Warning "Solution-scoped applicationhost.config not found at: $appHostPath"
	Write-Output "If you want automatic binding addition, open Visual Studio once so it generates .vs\$solutionName\config\applicationhost.config, then re-run this script."
	exit 0
}

Write-Output "Found applicationhost.config at: $appHostPath"


[xml]$xml = Get-Content $appHostPath

# Robustly find the <sites> node using XPath (works even if property access doesn't)
$sites = $xml.SelectSingleNode('//system.applicationHost/sites')
if ($null -eq $sites) {
	Write-Warning "applicationhost.config doesn't contain expected <system.applicationHost><sites> section or it couldn't be found via XPath. Skipping."
	exit 0
}

$firstSite = $sites.site | Select-Object -First 1
if ($null -eq $firstSite) {
	Write-Warning "No <site> entries found in applicationhost.config. Skipping."
	exit 0
}

$bindingsNode = $firstSite.bindings
if ($null -eq $bindingsNode) {
	# create bindings node
	$bindingsNode = $xml.CreateElement("bindings")
	$firstSite.AppendChild($bindingsNode) | Out-Null
}

$exists = $false
foreach ($b in $bindingsNode.binding) {
	if ($b.protocol -eq "http" -and $b.bindingInformation -eq "*:${Port}:*") { $exists = $true }
}

if ($exists) {
	Write-Output "Binding *:${Port}:* already present in the first site."
} else {
	# backup
	$backup = "$appHostPath.bak"
	Copy-Item -Path $appHostPath -Destination $backup -Force
	Write-Output "Backed up original applicationhost.config to $backup"

	$newBinding = $xml.CreateElement("binding")
	$newBinding.SetAttribute("protocol", "http")
	$newBinding.SetAttribute("bindingInformation", "*:${Port}:*")
	$bindingsNode.AppendChild($newBinding) | Out-Null

	# Save using the absolute path to avoid Write errors when current directory is different (e.g., C:\Windows\system32)
	try {
		$fullPath = (Resolve-Path -LiteralPath $appHostPath).ProviderPath
	} catch {
		$fullPath = $appHostPath
	}

	$xml.Save($fullPath)
	Write-Output "Added binding *:${Port}:* to the first <site> in applicationhost.config"
}

Write-Output "Done. Start Visual Studio (if not running) and run the project. You should be able to access the API from LAN using http://<machine-ip>:$Port/"

$ErrorActionPreference = 'Stop'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

# gets the login webpage...
$loginPage = Invoke-WebRequest -Uri 'http://localhost:52000/Login.aspx' -WebSession $session -UseBasicParsing
$vs = [regex]::Match($loginPage.Content, 'name="__VIEWSTATE" id="__VIEWSTATE" value="([^"]*)"').Groups[1].Value
$vsg = [regex]::Match($loginPage.Content, 'name="__VIEWSTATEGENERATOR" id="__VIEWSTATEGENERATOR" value="([^"]*)"').Groups[1].Value
$ev = [regex]::Match($loginPage.Content, 'name="__EVENTVALIDATION" id="__EVENTVALIDATION" value="([^"]*)"').Groups[1].Value

# logs into student acount,,,
$body = @{
    '__VIEWSTATE' = $vs
    '__VIEWSTATEGENERATOR' = $vsg
    '__EVENTVALIDATION' = $ev
    'ctl00$MainContent$txtEmail' = 'sipho@student.nwu.ac.za'
    'ctl00$MainContent$txtPassword' = 'CampusSafe2026!'
    'ctl00$MainContent$btnLogin' = 'Sign In'
}
$loginResp = Invoke-WebRequest -Uri 'http://localhost:52000/Login.aspx' -Method POST -Body $body -WebSession $session -UseBasicParsing
Write-Host "Logged in. Response Length: $($loginResp.Content.Length)"

# opens safe route page...
$safeRoute = Invoke-WebRequest -Uri 'http://localhost:52000/SafeRoute.aspx' -WebSession $session -UseBasicParsing
Write-Host "SafeRoute page status: $($safeRoute.StatusCode)"

# tests the route api calculation,,,
$url = 'http://localhost:52000/Handlers/Api.ashx?action=route&startLat=-26.6885&startLng=27.0940&endLat=-26.6920&endLng=27.0910'
$routeApi = Invoke-WebRequest -Uri $url -WebSession $session -UseBasicParsing
Write-Host "Route API status: $($routeApi.StatusCode)"
Write-Host "Route API response:"
Write-Host $routeApi.Content

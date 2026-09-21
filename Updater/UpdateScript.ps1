param
(
    [Parameter(Mandatory=$true)][string]$toolID,
    [switch]$skipVersion,
    [int]$PIDtoWait,
    [string]$pkgDir,
    [string]$csprojPath,
    [string]$oldVersion,
    [string]$newVersion
)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -Name Win32 -Namespace Console -MemberDefinition @'
[DllImport("kernel32.dll")]
public static extern IntPtr GetConsoleWindow();
[DllImport("user32.dll", SetLastError = true)]
public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
'@
function Clear-Line($top)
{
    $width = $Host.UI.RawUI.WindowSize.Width
    [Console]::SetCursorPosition(0, $top)
    Write-Host (" " * ($width - 1)) -NoNewline
    [Console]::SetCursorPosition(0, $top)
}
function Copy-ToLocalFeed($localSource, $toolID, $latest)
{
    Get-ChildItem -Path $localSource -Filter "$toolID.*.nupkg" | Remove-Item -Force
    Copy-Item $latest.FullName -Destination $localSource
    Write-Host " 📦 Copied $($latest.Name) to local nupkg install directory"
}
$hwnd = [Console.Win32]::GetConsoleWindow()
$ownerPID = 0
[void][Console.Win32]::GetWindowThreadProcessId($hwnd, [ref]$ownerPID)
Write-Host " ⌛ Waiting for $toolID process PID: $PIDtoWait to exit..."
while (Get-Process -Id $PIDtoWait -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 200 }
Write-Host " ✅ $toolID process exited. Proceeding with update..."
Write-Host " 🏗️ Moving new package to local nupkg install directory..."
$latest = Get-ChildItem -Path $pkgDir -Filter "$toolID.*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($latest)
{
    try
    {
        Write-Host " 🔍 Searching for user defined local nuget feed..."
        $localSource = (Select-Xml -Path "$env:APPDATA\NuGet\NuGet.Config" -XPath "//packageSources/add[@key='local']/@value").Node.Value
        if (-not $localSource) { throw " ⚠️ No user defined local nuget feed defined" }
        Write-Host " 🎯 Found user defined local nuget feed"
        Copy-ToLocalFeed $localSource $toolID $latest
    }
    catch
    {
        try
        {
            Write-Host " 🔍 Searching for globally defined local nuget feed..."
            $localSource = (Select-Xml -Path  "${env:ProgramFiles(x86)}\NuGet\Config\NuGet.Config" -XPath "//packageSources/add[@key='local']/@value").Node.Value
            if (-not $localSource) { throw " ⚠️ No globally defined local nuget feed defined" }
            Write-Host " 🎯 Found globally defined local nuget feed"
            Copy-ToLocalFeed $localSource $toolID $latest
        }
        catch
        {
            while ($true)
            {
                $top = [Console]::CursorTop
                $addLocal = Read-Host " ❓ Add locally defined feed (Y/N)"
                if ($addLocal -eq "Y")
                {
                    while ($true)
                    {
                        $top = [Console]::CursorTop
                        $addLocal = Read-Host " ❓Use default location: `"$env:USERPROFILE\source\repos\.nupkg\`" (Y/N)"
                        if ($addLocal -eq "Y")
                        {
                            $localFeed = "$env:USERPROFILE\source\repos\.nupkg\"
                            break
                        }
                        elseif ($addLocal -eq "N")
                        {
                            while ($true)
                            {
                                $topLocalFeed = [Console]::CursorTop
                                $localFeed = Read-Host " 📂 Enter desired directory path"
                                Write-Host " ℹ️ Entered directory path is: $localFeed"
                                while ($true)
                                {
                                    $top = [Console]::CursorTop
                                    $addLocal = Read-Host " ❓Is this correct (Y/N)"
                                    if ($addLocal -eq "Y") { break }
                                    elseif ($addLocal -eq "N") { Clear-Line $topLocalFeed }
                                    else { Clear-Line $top }
                                }
                                break
                            }
                        }
                        else { Clear-Line $top }
                    }
                    while ($true)
                    {
                        $top = [Console]::CursorTop
                        $addLocal = Read-Host " ❓ Add to user or global config (U/G)"
                        if ($addLocal -eq "U")
                        {
                            $configPath = "$env:APPDATA\NuGet\NuGet.Config"
                            dotnet nuget add source $localFeed -n local --configfile $configPath
                            $localSource = $localFeed
                            Copy-ToLocalFeed $localSource $toolID $latest
                            break
                        }
                        elseif ($addLocal -eq "G")
                        {
                            $configPath = "${env:ProgramFiles(x86)}\NuGet\Config\NuGet.Config"
                            dotnet nuget add source $localFeed -n local --configfile $configPath
                            $localSource = $localFeed
                            Copy-ToLocalFeed $localSource $toolID $latest
                            break
                        }
                        else { Clear-Line $top }
                    }
                    break
                }
                elseif ($addLocal -eq "N")
                {
                    $localSource = $latest.DirectoryName
                    break
                }
                else { Clear-Line $top }
            }
        }
    }
}
else
{
    Write-Host " ❌ No nupkg found in $pkgDir"
    if ($ownerPID -eq $PID) { Read-Host "🚪 Press Enter to exit" }
    exit 1
}
Write-Host " ⚙️ Updating $toolID..."
Write-Host " 🧠 Executing: dotnet tool update --global $toolID"
& dotnet tool update --global $toolID --source $localSource --verbosity detailed
if ($LASTEXITCODE -eq 0)
{
    $timestamp = Get-Date -Format "dd-MM-yyyy HH:mm:ss"
    Write-Host " ✅ $toolID successfully updated to latest build at $timestamp"
}
else
{
    Write-Host " ❌ $toolID update failed with exit code $LASTEXITCODE"
    if (-not $skipVersion)
    {
        $proj = $csprojPath
        $text = Get-Content $proj -Raw
        $text = $text -replace "<Version>$newVersion</Version>", "<Version>$oldVersion</Version>"
        Set-Content $proj $text -Encoding UTF8
        Write-Host " ↩️ Restored version number: $newVersion → $oldVersion"
    }
}
if ($ownerPID -eq $PID) { Read-Host " 🚪 Press Enter to exit" }
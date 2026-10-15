param([switch]$NoBuild)
Set-Location D:\Unity\KeystoneKapers
unity command recompile | Out-Null
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 2
    $st = (unity command recompile_status --format json | ConvertFrom-Json).data.result
    if ($st.status -ne 'compiling') { break }
}
"compile: $($st.status) failed=$($st.compilationFailed)"
$st.errors | ForEach-Object { $_ | ConvertTo-Json -Compress }
if ($st.compilationFailed) { exit 1 }
if (-not $NoBuild) {
    $r = unity command run_script --file AgentScripts/RunBuild.cs --entry RunBuild.Main --timeout_ms 300000 --timeout 320 --format json | ConvertFrom-Json
    $r.data.result.result
    $r.errors | ConvertTo-Json -Depth 4
}

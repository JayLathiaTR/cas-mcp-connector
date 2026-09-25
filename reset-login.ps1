# Clears mcp-remote's cached OAuth tokens so the connector shows the login screen again.
# Run this right before a demo, then restart Claude Desktop.
$cache = Join-Path $env:USERPROFILE ".mcp-auth\mcp-remote-v1"
if (Test-Path $cache) {
    Remove-Item $cache -Recurse -Force
    Write-Host "Cleared mcp-remote auth cache: $cache" -ForegroundColor Green
} else {
    Write-Host "No mcp-remote cache found at $cache (nothing to clear)." -ForegroundColor Yellow
}
Write-Host "Now restart Claude Desktop to trigger the sign-in flow." -ForegroundColor Cyan

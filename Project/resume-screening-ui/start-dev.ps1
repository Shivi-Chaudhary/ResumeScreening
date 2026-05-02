# Ensures Node.js is on PATH (fixes "node is not recognized" when using npm/ng)
$nodeDir = "${env:ProgramFiles}\nodejs"
if (Test-Path $nodeDir) {
  $env:Path = "$nodeDir;$env:Path"
}
Set-Location $PSScriptRoot
npm run start

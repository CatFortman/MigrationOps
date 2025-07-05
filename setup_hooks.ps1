# setup_hooks.ps1
# This script sets up Git hooks by copying them from the GitHooks directory to the .git/hooks directory.

# Define the source directory where your custom hooks are stored
$sourceDir = "MigrationTracker.ConsoleApp\GitHooks"

# Define the target directory where Git hooks are stored in your repository
$targetDir = ".git/hooks"

# Check if the source directory exists
if (-Not (Test-Path $sourceDir)) {
    Write-Host "Source directory '$sourceDir' not found. Please ensure this script is run from the root of the repository." -ForegroundColor Red
    exit 1
}

# Check if the target directory exists
if (-Not (Test-Path $targetDir)) {
    Write-Host "Git hooks directory '$targetDir' not found. Please ensure this script is run in a Git repository." -ForegroundColor Red
    exit 1
}

# Get all files from the source directory
$hookFiles = Get-ChildItem -Path $sourceDir -File

# Copy each hook file to the target directory
foreach ($hook in $hookFiles) {
    $targetPath = Join-Path $targetDir $hook.Name
    Copy-Item -Path $hook.FullName -Destination $targetPath -Force
    Write-Host "Copied '$($hook.Name)' to '$targetDir'" -ForegroundColor Green
}

Write-Host "Git hooks setup complete!" -ForegroundColor Cyan

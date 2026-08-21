$ErrorActionPreference = "Continue"
Set-Location D:\AppleMusicDesktopLyrics

Write-Host "=== 1. abort rebase ===" -ForegroundColor Cyan
git rebase --abort 2>&1

Write-Host "=== 2. merge origin/main ===" -ForegroundColor Cyan
git merge origin/main --no-edit 2>&1

Write-Host "=== 3. resolve: website->remote, desktop->local ===" -ForegroundColor Cyan
git checkout --theirs -- . 2>&1
git checkout --ours -- LyricHover.App LyricHover.Core LyricHover.Core.TranslationContractTests LyricHover.Tests Directory.Build.props CHANGELOG.md tools store 2>&1

Write-Host "=== 4. commit ===" -ForegroundColor Cyan
git add -A 2>&1
git commit -m "Merge origin/main: remote website + local desktop v3.0.16" 2>&1

Write-Host "=== 5. push ===" -ForegroundColor Cyan
git push origin main 2>&1

Write-Host "=== DONE ===" -ForegroundColor Green

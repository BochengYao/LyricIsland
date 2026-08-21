$ErrorActionPreference = "Continue"
Set-Location D:\AppleMusicDesktopLyrics

Write-Host "=== 1. remove promo materials from git tracking ===" -ForegroundColor Cyan
git rm -r --cached "视觉宣传" 2>&1
git rm --cached README_EN.md 2>&1

Write-Host "=== 2. commit ===" -ForegroundColor Cyan
git add -A 2>&1
git commit -m "chore: rewrite README with Lyric Dock, ignore 3D assets and promo materials" 2>&1

Write-Host "=== 3. push ===" -ForegroundColor Cyan
git push origin main 2>&1

Write-Host "=== DONE ===" -ForegroundColor Green

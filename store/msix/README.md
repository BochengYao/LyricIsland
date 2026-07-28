# Microsoft Store MSIX

`build-msix.ps1` 使用 Partner Center 为 Lyric Island 分配的正式包身份，
从 `publish/current` 生成 x64 MSIX。包版本自动取自
`Directory.Build.props`，并转换为商店要求的四段版本号。

```powershell
.\store\msix\build-msix.ps1
```

如果已经确认 `publish/current` 是当前源码生成的版本，可以跳过再次发布：

```powershell
.\store\msix\build-msix.ps1 -SkipPublish
```

生成的文件位于 `store/package/msix/`，不会提交到 Git。该 MSIX 保持未签名，
用于上传 Partner Center，认证通过后由 Microsoft Store 签名。

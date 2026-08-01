# Microsoft Store MSIX

`build-msix.ps1` 使用 Partner Center 为 Lyric Island 分配的正式包身份，
在独立临时目录生成自包含的 x64 应用并封装为 MSIX。它不会读取或修改
`publish/current`。包版本自动取自 `Directory.Build.props`，并转换为商店
要求的四段版本号。

```powershell
.\store\msix\build-msix.ps1
```

如已单独运行过完整测试，可以仅跳过 MSIX 流程中的重复测试；自包含发布仍会执行：

```powershell
.\store\msix\build-msix.ps1 -SkipTests
```

生成的文件位于 `store/package/msix/`，不会提交到 Git。该 MSIX 保持未签名，
用于上传 Partner Center，认证通过后由 Microsoft Store 签名。常规的
`publish.ps1` 仍生成精简的框架依赖版 `publish/current`。

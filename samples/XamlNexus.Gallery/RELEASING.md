# Gallery 便携包发布 / Portable Gallery builds

Gallery 在本仓库独立维护，版本来自 `Directory.Build.props`。它不使用生成项目的安装器、更新 feed 或 PR 发布标签流程。

Gallery is maintained in this repository. Its version comes from `Directory.Build.props`; generated-project installer and update-feed workflows do not apply.

## 本地构建 / Local build

在仓库根目录执行 / Run from the repository root:

```powershell
./eng/Publish-Gallery.ps1
# Optional ARM64 build
./eng/Publish-Gallery.ps1 -Architecture arm64
```

输出位于仓库 `.artifacts/gallery/` 下带时间戳的目录。ZIP 包含自包含应用和资源，解压完整目录后运行 `XamlNexus.Gallery.exe`；不能只复制 EXE。

Output is written to a timestamped directory under `.artifacts/gallery/`. Extract the entire ZIP and launch `XamlNexus.Gallery.exe`; the executable requires the accompanying resources.

## CI 与发布检查 / CI and release checks

仓库根目录的 `.github/workflows/gallery.yml` 在相关 PR 或手动触发时生成并上传便携包 artifact，不自动创建 GitHub Release。

The root `.github/workflows/gallery.yml` builds and uploads a portable artifact for relevant pull requests or manual runs. It does not automatically publish a GitHub Release.

发布前更新版本，使用新目录解压验证启动、中文/英文切换、示例页、源码浏览及复制。构建脚本检查必要资源，但这些检查不能替代启动验证。ARM64 包需要在对应设备上验证。

Before publishing, update the version and verify a fresh extraction: startup, both languages, examples, source browsing and copy actions. Resource checks in the script do not replace startup testing. Validate ARM64 packages on a matching device.

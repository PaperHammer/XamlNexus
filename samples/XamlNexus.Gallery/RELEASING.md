# Gallery 便携包发布 / Portable Gallery builds

Gallery 在本仓库独立维护。正式发布使用 CLI 的版本和同一提交；独立调试构建默认使用 `Directory.Build.props` 的版本。

Official Gallery builds use the CLI version and the same source revision. Standalone development builds default to `Directory.Build.props`.

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

正式发布由 `.github/workflows/release-merged-pull-request.yml` 统一完成：构建 x64/ARM64 Gallery，生成固定版本、下载地址和 SHA-256 清单，将清单放入 NuGet 工具包。包检查同时核对清单和两个 ZIP 的哈希。先上传 GitHub Release 资源，再发布 NuGet，使新安装的工具可以立即下载 Gallery。

发布重试会复用相同提交已发布的 ZIP 和 NuGet 包，不重新构建或覆盖固定哈希的资源。若已有发布不完整或不匹配，流程失败并要求修复或递增版本。

The production workflow builds both architectures, packs their pinned URLs and SHA-256 values into the tool, validates the package against the archives, publishes GitHub assets, then publishes NuGet. Retries reuse existing assets from the same source revision; existing pinned assets are never replaced with different bytes.

发布前更新版本，使用新目录解压验证启动、中文/英文切换、示例页、源码浏览及复制。构建脚本检查必要资源，但这些检查不能替代启动验证。ARM64 包需要在对应设备上验证。

Before publishing, update the version and verify a fresh extraction: startup, both languages, examples, source browsing and copy actions. Resource checks in the script do not replace startup testing. Validate ARM64 packages on a matching device.

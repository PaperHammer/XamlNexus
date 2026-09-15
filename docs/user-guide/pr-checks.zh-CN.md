# PR 流程与校验开关

[English](pr-checks.md) | [简体中文](pr-checks.zh-CN.md)

本文说明**新生成应用项目**的行为。XamlNexus 仓库根目录还有自己的发布工作流，与生成项目不同。旧项目应先检查自己的 `.github/workflows`，新模板不会自动替换已有文件。

## PR 会触发什么

生成项目提供 `.github/workflows/validate-pull-request.yml`，工作流名为 `Validate pull request`，作业 ID 为 `validate`。当目标分支为 `main`，且发生以下事件时触发：

- 创建 PR（`opened`），包括草稿 PR。
- 向 PR 分支推送新提交（`synchronize`）。
- 重新打开 PR（`reopened`）。
- 将草稿标记为可评审（`ready_for_review`）。

默认不因修改标签或描述、直接向 `main` 推送、关闭或合并 PR 而单独触发；不提供手动触发按钮。工作流配置见[源码模板](../../src/Templates/Shared/.github/workflows/validate-pull-request.yml)。

流程在 Windows runner 中安装 .NET 8 和 10，从 `eng/publishing/release.json` 读取 `solution`，依次执行：

| 步骤 | 检查范围 |
|---|---|
| Restore | 依赖是否能还原 |
| Build | 解决方案能否以 Release/x64 编译 |
| Test | 运行解决方案中的测试；是否有有效测试取决于用户项目 |

前一步失败时，后续步骤默认不执行。当前流程没有 GUI、安装器运行或签名验证，也没有强制格式检查。命令显式使用 `NuGetAudit=false`，不进行 NuGet 漏洞审计。

它不会自动发布、上传安装器、创建 Release 或改版本号；不要求 `release:*` 标签和 Release notes。PR 模板仅提供变更与验证说明。CI 也没有调用 `xamlnexus validate` 或 `doctor`。

## 开启或关闭 CI 执行

仓库允许 GitHub Actions 且该工作流处于启用状态时，满足上述事件就会运行。可在仓库 **Actions → Validate pull request → 菜单 → Disable workflow / Enable workflow** 中关闭或重新开启，也可在已认证且有权限的 GitHub CLI 中执行：

```powershell
gh workflow disable validate-pull-request.yml --repo OWNER/REPO
gh workflow enable validate-pull-request.yml --repo OWNER/REPO
```

禁用工作流保留 YAML 文件。重新启用后，用新的匹配事件触发检查，例如向 PR 推送新提交。[GitHub：启停工作流](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/disable-and-enable-workflows)

## 是否阻止合并是另一个设置

生成器不会替你设置 GitHub 分支保护或规则集。若希望校验失败时阻止合并：

1. 先运行一次 PR 校验。
2. 在仓库 Settings 的 Rules / Rulesets，或 Branches 的分支保护中，为 `main` 配置必需状态检查。
3. 选择这次工作流实际报告的 `validate` 检查，保存规则；是否需要分支保持最新可单独选择。

如果希望继续运行 CI，但允许不等结果就合并，移除规则中的该必需检查即可，不必删除工作流。其他评审、组织规则仍可能限制合并；修改这些设置需要对应权限，功能可用性取决于仓库与 GitHub 方案。[GitHub：受保护分支](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)

关闭或删除工作流前，应同时检查是否还将它设为必需项。通过路径过滤或提交消息跳过整个工作流，可能使必需检查一直处于 Pending，仍无法合并。如果启用了 merge queue，还需增加 `merge_group` 触发，当前模板未配置它。[GitHub：排查必需检查](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks)

## 调整检查范围

直接编辑生成项目的 YAML：

| 需求 | 调整方式 |
|---|---|
| 校验其他目标分支 | 修改 `pull_request.branches`，例如 `[main, develop]` |
| 草稿阶段不运行作业 | 在 `jobs.validate` 下加 `if: github.event.pull_request.draft == false`；当前默认会运行草稿 |
| 暂时不跑测试 | 删除或注释 `Test` 步骤；恢复该步骤即可开启 |
| 开启漏洞审计 | 将 Restore 中的 `NuGetAudit=false` 改为 `true`；审计告警是否导致失败还取决于 NuGet/MSBuild 配置 |
| 增加格式检查 | 添加格式检查步骤；现有流程没有此检查 |
| 更换解决方案 | 更新 `eng/publishing/release.json` 的 `solution`，或改成工作流直接指定路径 |

不要仅删除 Build 而保留带 `--no-build` 的 Test：测试步骤依赖前面生成的结果。重命名作业或改变触发条件时，也要同步检查 GitHub 的必需检查设置。

## CLI 校验与文件保护

这些规则在本地命令中执行，不受 GitHub Actions 开关控制：

| 类别 | 行为与可调整范围 |
|---|---|
| `validate` / `doctor` | 手动执行的检查；不调用就不产生报告，但其他写入命令仍检查自己的前置条件 |
| 创建参数 | 名称、架构、语言、解决方案格式必须有效；没有统一关闭开关 |
| 组件依赖与冲突 | `add` 检查架构兼容、依赖及重复安装；不能通过关闭 CI 跳过 |
| 自定义源码 | 允许修改；普通内容差异通常是警告，缺失受管理文件是错误 |
| 自动更新、移除 | 哈希不符合预期或文件缺失时停止，保护用户修改；没有 `--force` 跳过开关 |
| 脚手架合并 | 无法可靠合并时报告冲突；按冲突处理流程解决 |
| 路径与并发 | 拒绝越界、符号链接、重复目标及过期或并发写入；没有关闭开关 |
| 页面导航接入 | 可用 `page add Orders --no-navigation` 跳过自动导航注册 |
| 构建 | `run --no-build` 可运行已有输出，但不包含尚未编译的修改 |

`--dry-run` 是预览，不是跳过校验；`--json` 只改变输出格式。不要通过修改哈希绕过用户修改保护。详见[命令详解](commands.zh-CN.md)、[诊断](doctor.zh-CN.md)和[项目升级](project-upgrade.zh-CN.md)。

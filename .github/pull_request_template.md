## 变更说明 / Summary

<!-- 说明解决的问题、修改后的行为和影响范围。
Describe the problem, resulting behavior, and affected areas. -->

## 验证 / Validation

<!-- 填写执行的检查及结果；未验证的部分请明确说明。
List checks and results, and identify anything not verified. -->

## 发布方式 / Release type

合并前请在 PR 的 Labels 中添加且仅添加一个标签；正文填写标签不会自动设置 Labels。
Apply exactly one PR label before merging; mentioning it in this description does not set the label.

| 标签 / Label | 行为 / Behavior |
|---|---|
| `release:none` | 只合并，不自动发布 / Merge without publishing |
| `release:stable` | 自动发布项目中声明的正式版本 / Publish the declared stable version |
| `release:preview` | 自动发布项目中声明的预览版本 / Publish the declared prerelease version |

自动发布 PR 需要先修改 `src/XamlNexus/XamlNexus.csproj` 中的版本，且高于目标分支版本。工作流只读取并校验版本，不会自动递增。请同步 `Version` 和 `PackageVersion`；`AssemblyVersion` 使用对应的纯数字版本。
Publishing PRs must update the project version above the target branch version. The workflow reads and validates versions; it does not increment them. Keep `Version` and `PackageVersion` aligned, with a corresponding numeric `AssemblyVersion`.

正式版示例：`1.0.4`；预览版示例：`1.0.4-preview.1`（程序集版本使用 `1.0.4`）。使用 `release:none` 时不要求增加版本号。
Examples: `1.0.4` for stable, or `1.0.4-preview.1` for preview (assembly version `1.0.4`). `release:none` does not require a version increase.

## 发布说明 / Release notes

<!-- 自动发布时，必须在下面两个标记之间填写面向用户的更新说明，它会成为 GitHub Release 的正文。
For automated releases, enter user-facing notes between the markers below. They become the GitHub Release body. -->

<!-- release-notes:start -->

<!-- release-notes:end -->

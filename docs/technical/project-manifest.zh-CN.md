# XamlNexus 项目清单

[English](project-manifest.md) | [简体中文](project-manifest.zh-CN.md)

每个生成项目的解决方案根目录都包含 `xamlnexus.json`。它是 `add`、`list`、`validate`、`doctor` 和 `upgrade` 等命令使用的机器可读契约。

## 结构版本 1

```json
{
  "schemaVersion": 1,
  "generatorVersion": "1.0.3",
  "project": {
    "name": "MyApp",
    "preset": "winui",
    "profile": "standard",
    "language": "en-US",
    "solutionFormat": "sln"
  },
  "modules": [
    {
      "id": "editorconfig",
      "version": "1.0.0",
      "source": "recipe",
      "files": [
        {
          "path": ".editorconfig",
          "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        }
      ]
    }
  ],
  "scaffoldFiles": [
    {
      "path": "Directory.Build.props",
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "baselineContentGzipBase64": "H4sI..."
    },
    {
      "path": "MyApp.UI/App.xaml.cs",
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "baselineContentGzipBase64": "H4sI...",
      "userEditable": true
    }
  ]
}
```

- `schemaVersion` 表示清单结构版本。工具必须拒绝修改不支持的新版本清单。
- `generatorVersion` 是最后生成项目结构的 XamlNexus CLI 版本。
- `project.preset` 为 `winui` 或 `hybrid`。
- `project.profile` 为初始能力组合：`standard`（默认）或 `basic`。旧清单可省略，按 standard 处理。它记录创建意图，不代表当前安装状态，add/remove 不改变此字段。
- `project.language` 为生成应用的默认语言。
- `project.solutionFormat` 为 `sln`（默认）或 `slnx`。
- `modules` 记录 XamlNexus 管理的能力，模块 ID 不区分大小写且必须唯一。
- 模块 `source` 为 `template` 时来自预设；为 `recipe` 时由组件安装加入。
- 模块 `version` 记录负责其生成文件及迁移规则的 XamlNexus 或 Recipe 版本。
- Recipe 模块记录其拥有的文件及 SHA-256，以便校验及后续移除、升级检测用户修改。
- `scaffoldFiles` 记录模板来源文件。SHA-256 保护基线；可选的 `baselineContentGzipBase64` 保存压缩后的精确原始内容，供三方合并使用。
- `userEditable: true` 标识通常由用户和 Recipe 定制的源码、工程文件。validate 不把这些定制当作损坏；upgrade 仍与保存的基线比较。
- `userEditable` 是兼容保留的诊断提示，不是编辑许可。false 或省略时也可修改；内容差异产生警告而非错误。保留原始哈希，供自动更新与移除保护本地修改。
- 旧版仅含哈希的记录仍然有效；本地内容不再匹配时采用保守冲突处理。

清单有意保持确定性，不包含时间戳、机器路径、用户名等环境信息。应与生成解决方案一起纳入版本控制。

使用 `xamlnexus list` 查看项目身份和模块，使用 `xamlnexus validate` 检查清单及模块关键文件。两者从当前目录向父目录查找清单，也支持 `--project <path>`。

保存前会校验完整清单，再通过临时文件原子替换目标。改变项目能力的命令必须将源码和清单作为同一事务更新。

可安装模块的事务与兼容规则见 [Recipe 契约](recipe-contract.zh-CN.md)。

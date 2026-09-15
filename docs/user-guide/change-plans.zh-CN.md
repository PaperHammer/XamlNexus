# 变更计划与机器可读输出

[English](change-plans.md) | [简体中文](change-plans.zh-CN.md)

修改已有项目的命令支持经过校验的预览：

```powershell
xamlnexus add sqlite --project D:\Projects\MyApp --dry-run
xamlnexus remove sqlite --project D:\Projects\MyApp --dry-run
xamlnexus update sqlite --project D:\Projects\MyApp --dry-run
xamlnexus upgrade --project D:\Projects\MyApp --dry-run
```

`--dry-run` 构建并校验与实际事务相同的文件和结构化工程计划，但不更新项目文件或清单。升级规划期间可能在项目外创建并删除临时目标脚手架。

脚本或 CI 需要读取结果时，加上 `--json`：

```powershell
xamlnexus add sqlite --project D:\Projects\MyApp --dry-run --json
```

成功预览包含 `status: "planned"`、`dryRun: true`、版本信息和 changes 数组。Recipe 变更具有 owned 或 project 范围。升级计划提供 canApply、冲突以及变更类型和相对路径，不输出生成文件内容。

升级的 strategy 可为：textMerge（独立行修改）、xmlMerge（XML/MSBuild/XAML 语义合并）、solutionMerge（SLN 工程、节或配置的独立添加）、direct（无需合并的直接替换）。

冲突正文也不放进 JSON。需要导出 LOCAL／BASE／TARGET 文档时，显式使用 `upgrade --conflict-output <directory>`，不可同时加 dry-run；导出不覆盖已有文件。

上述四种命令也支持不带 dry-run 的 JSON。成功事务返回 applied 或更具体的无操作／基线状态，以及变化路径。失败使用统一结构：

```json
{
  "operation": "add",
  "status": "error",
  "error": {
    "code": "XR1201",
    "message": "XR1201: ..."
  }
}
```

错误不来自带版本的 Recipe 或升级契约时，code 为 null。以退出码为准：0 成功，1 校验／规划／应用失败，2 命令用法错误。JSON 作为单个文档写入标准输出。

## 查询命令

list、validate、recipes 也支持 JSON：

```powershell
xamlnexus list --project D:\Projects\MyApp --json
xamlnexus validate --project D:\Projects\MyApp --json
xamlnexus recipes --json
```

list 返回项目身份、生成器版本和模块。validate 返回 valid 或 invalid 状态、isValid 布尔值及完整带编码问题列表。recipes 返回所有内置描述，包括兼容性、依赖和冲突元数据。

操作失败及命令行错误使用相同 operation/status/error 结构。JSON 模式的语法错误为 `XC1001`；项目定位或清单读取失败，list 使用 `XL1001`，validate 使用 `XV1001`。默认仍为面向人的文本输出。

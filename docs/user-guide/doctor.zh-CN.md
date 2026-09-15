# XamlNexus 诊断

[English](doctor.md) | [简体中文](doctor.zh-CN.md)

在生成项目内任意目录运行只读的项目与环境诊断：

```powershell
xamlnexus doctor
xamlnexus doctor --project D:\Projects\MyApp
```

CI 或问题报告可使用 JSON 输出：

```powershell
xamlnexus doctor --project D:\Projects\MyApp --json
```

出现错误时退出码为 1；警告会显示，但保持退出码 0。

## 检查内容

- `XD10xx`：Windows、项目目录选中的 .NET SDK、本地启用的 NuGet 源。
- `XD20xx` 和 `XNxxxx`：清单及受管理文件状态。
- `XD30xx`：Windows 目标框架和 Windows App SDK 版本一致性。
- `XD40xx`：已安装 Recipe 是否可用，以及目录版本差异。
- `XD50xx`：SQLite Data 工程归属、WAL 源码线索、混合架构 Protobuf 接入。
- `XD60xx`：发布元数据与配置。

环境检查不访问 NuGet 网络源，只检查本地 SDK 选择和源配置。SDK 解析遵循项目或祖先目录的 global.json；机器上存在其他 SDK，不代表当前选中的缺失或不受支持版本有效。命令不打印密钥与签名证书。

用户修改生成文件会得到定制警告，而不是编辑违规错误。缺失必要文件仍是错误。SQLite WAL 检测只是源码文本启发式：发现 WAL 文本不能证明运行中数据库的模式；未发现只产生警告，不阻止自定义初始化。

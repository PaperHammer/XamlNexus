# AI 开发指导

[English](ai-guidance.md) | [简体中文](ai-guidance.zh-CN.md)

XamlNexus 随 CLI 内置两类开发指导，使用已有 AI 编码工具完成项目开发。它们是 skills、架构说明和协作规则；不包含模型、推理引擎或独立 agent harness。

| 类型 | 用途 | 提供方式 |
|---|---|---|
| `xamlnexus-cli` | 正确调用创建、页面、Recipe、诊断和升级命令，理解 JSON、预览、文件归属与冲突 | 随源码提供，新项目自动生成 |
| `xamlnexus-project` | 熟悉实际宿主、项目分层、模块注册、DI、页面生命周期、业务激活和关闭保护 | 新项目自动生成 |

## 新建项目

```powershell
xamlnexus new MyApp --preset winui --profile basic
xamlnexus new HybridApp --preset hybrid
```

开发指导作为本地文件随创建过程直接写入，无需单独安装或初始化命令。当前源码仓库也提供 CLI skill 文件。

生成结果包含根目录 `AGENTS.md`、`CLAUDE.md`、`QWEN.md`，以及三个工具各自的 skills：

| 工具 | 项目级 skills 目录 | 规则入口 |
|---|---|---|
| Codex | `.agents/skills` | `AGENTS.md` |
| Claude Code | `.claude/skills` | `CLAUDE.md` 引用 `AGENTS.md` |
| Qwen Code | `.qwen/skills` | `QWEN.md` 指向 `AGENTS.md` |

三个 skills 目录初始内容相同，每个包含上述两类 skill。项目说明填入真实项目名、preset、初始 profile、语言、解决方案格式和启动工程。WinUI 项目从 `.UI` 启动；混合项目从 WPF 宿主启动，业务服务与存储保留在宿主，通过现有 RPC 边界访问。

`AGENTS.md` 约定业务代码归属、构造函数注入、页面离开与关闭保护、维护基线、现有样式与本地化规范，并给出编译和诊断命令。当前能力以 `xamlnexus.json` 和实际代码为准；初始 profile 不代表后续安装的模块。

## 自定义与升级

按实际项目补充命名、业务边界、测试命令和团队规范。长说明放入 skill 的 `references`，避免根规则复制整个代码库。这里的规则是 AI 开发指导，不是命令权限或运行时安全隔离。

新建项目的指导文件作为可编辑脚手架记录基线，后续通过 `upgrade --dry-run` 与三方合并维护；本地修改按既有升级冲突机制保护。参见[项目升级](project-upgrade.zh-CN.md)。

开发请求例如：“用 XamlNexus CLI 在当前项目新增 Orders 列表页，然后按项目规范接入数据源并编译验证”。代理应读取对应 skill、检查实际代码与命令版本，再调用 CLI 并修改业务代码；编译成功不代表原生窗口、焦点或未保存对话框已经通过运行验收。

目录布局依据 [Codex skills](https://learn.chatgpt.com/docs/build-skills)、[Claude Code skills](https://code.claude.com/docs/en/skills) 和 [Qwen Code skills](https://qwenlm.github.io/qwen-code-docs/en/users/features/skills/) 的项目级约定。实际发现与调用取决于所用客户端版本及配置。

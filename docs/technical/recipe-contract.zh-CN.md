# XamlNexus Recipe 契约 v1

[English](recipe-contract.md) | [简体中文](recipe-contract.zh-CN.md)

Recipe 是对已有生成项目进行的、带版本和事务语义的变更。v1 契约位于 `XamlNexus.Common.Recipes`，供内置 Recipe 及未来基于包的适配器使用；这不表示包式扩展入口已经实现。

使用 `xamlnexus recipes` 查看内置目录，使用 `xamlnexus add <recipe> [--project <path>]` 安装、`remove` 移除、`update` 迁移到当前工具目录中的新版本。

内置 Recipe 为 `settings`、`editorconfig`、`sqlite`、`system-tray` 和 `app-update`。前三者支持两种架构；后两者面向纯 WinUI，因为混合宿主已有托盘与更新能力。app-update 依赖 settings。运行和进程边界见 [SQLite](../user-guide/sqlite-recipe.zh-CN.md)、[托盘](../user-guide/system-tray-recipe.zh-CN.md)与[在线更新](../user-guide/app-update-recipe.zh-CN.md)。

## 描述信息

每个 Recipe 声明：

- 小写 kebab-case ID 与语义版本；
- 显示名称和说明；
- 支持的架构（winui、hybrid 或两者）；
- 必须已安装的模块依赖；
- 与其冲突的模块。

Recipe ID 成为清单中不区分大小写的唯一模块 ID。安装成功后记录 `source: "recipe"` 和自身版本。

## 安装时的文件与项目变更

`IXamlNexusRecipe.CreatePlan` 返回文件操作和可选结构化工程操作。文件操作包括：

- `Create`：仅当目标不存在时创建。
- `Replace`：仅当当前 SHA-256 匹配预期值时替换。
- `Delete`：仅当当前 SHA-256 匹配预期值时删除。

路径相对于项目根目录。绝对路径、越界路径、重复目标及直接修改清单均被拒绝。Recipe 只能先列出要执行的操作，此时不得修改项目。

结构化操作支持：

- 向现有 csproj 添加 PackageReference；
- 确保包版本至少满足数值型最低版本，不降级更新版本；
- 向 csproj 添加 ProjectReference；
- 将已有或事务创建的 csproj 加入 SLN／SLNX，并处理相应解决方案结构；SLN 包括构建配置映射；
- 向 MSBuild gRPC 工程添加已有或事务创建的 proto 源文件。

操作解析 MSBuild XML 或解决方案结构，不做文本搜索替换；添加已有等价引用会被拒绝。

`EnsurePackageReference` 遇到条件引用时报告 `XR1247`，包括项、祖先、元数据上的条件，带条件的 Update 项以及 Choose 分支。工具无法保证所有构建配置都具备依赖。请检查条件，提供满足最低版本的无条件引用后重试。工具在检查待执行操作时就会拒绝修改，尚未写入工程或清单。

共享 csproj、sln 和 slnx 文件在失败时按事务恢复，但不记录为 Recipe 独占文件，允许其他 Recipe 和用户继续独立修改。

## 移除组件

清单保存 Recipe 拥有的文件和安装哈希。移除前检查所有文件；缺失或用户修改的文件会使整个移除中止，清单不变。

实现 `IXamlNexusRecipeRemovalPlanProvider` 可声明反向结构操作，包括移除精确 ProjectReference、解决方案工程及其构建配置和文件夹映射、Protobuf 项。移除项目后，含注释或其他非空白内容的 ItemGroup 会保留；无关 XML、工程和用户文件保持原样。安装时确保存在的包引用不删除也不降级，因为其安装前状态不归 Recipe 所有。

## 更新组件

用户可定制生成文件。普通校验以 `XN1202` 警告报告内容变化，不阻止 run、page add 等开发操作；缺失文件仍为 `XN1201` 错误。清单保留原始基线，不自动接纳编辑。更新和移除继续使用严格哈希前置条件，在任何写入前拒绝修改过的文件以保护用户内容。

更新比较安装版本与当前目录版本。CLI 对同版本返回无操作，拒绝降级。新版本声明安装后应包含的文件，更新时据此确定操作：

- 已有路径变为 Replace，使用已安装哈希作前置条件；
- 新路径保持 Create；
- 不再声明的旧路径变为 Delete，使用已安装哈希作前置条件。

任何用户修改都在写入前阻止完整更新。改变工程或解决方案结构的 Recipe 实现 `IXamlNexusRecipeUpdatePlanProvider`，返回针对已安装版本的显式迁移操作。安装结构操作不会自动重放，以免重复引用。

需要 DI 或启动工作的组件可加入兼容裁剪的[应用模块](module-lifecycle.zh-CN.md)，无需把组件集成写入 App.xaml.cs，同时保持显式、确定的启动行为。

## 事务规则

写入前校验描述、架构、依赖、冲突、目标路径、文件存在性及替换／删除哈希，防止静默覆盖。

文件通过同目录临时文件写入。执行失败时恢复已变更文件，并删除事务创建的空目录。清单最后由原子写入器更新；失败事务不添加模块。移除遵循同样规则，全部文件及结构变更成功后才删除清单条目。

## 稳定错误码

- `XR10xx`：Recipe 元数据无效。
- `XR11xx`：架构、依赖、安装状态或冲突问题。
- `XR12xx`：文件操作不安全，或所依据的文件状态已过期。
- `XR13xx`：事务执行或回滚失败。
- `XR14xx`：无效移除请求。
- `XR15xx`：无效更新或版本迁移。

契约不提供任意脚本执行或文本搜索替换。后续操作类型应保持显式、声明式且可独立测试。

SLNX 通过 Path 识别 XML Project 元素，包括文件夹内工程，保留无关文件夹和配置。构造解决方案路径时使用清单的 `project.solutionFormat`。

Recipe 事务、页面添加和脚手架升级共享项目写锁。并发写入会被拒绝，活动操作结束后重试。持锁时再次检查清单和文件前置条件。`XR1304` 表示传入上下文已过期，需重新 Locate 并预览；批量操作确定后，若清单发生变化，则报告 `XR1803`。锁只协调 XamlNexus 命令，外部编辑器不参与。

项目及操作路径不得经过 junction、符号链接或其他 reparse point，包括项目根目录的祖先。这样路径别名不能绕过写锁；请使用实际物理目录。写入前还会复核目标路径。

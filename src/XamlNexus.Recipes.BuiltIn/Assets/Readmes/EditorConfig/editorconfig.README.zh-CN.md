# EditorConfig

[English](editorconfig.README.md) | [简体中文](editorconfig.README.zh-CN.md)

根目录的 `.editorconfig` 定义项目的编辑规范，无需注册运行时服务或配置应用。请使用 Visual Studio 等支持 EditorConfig 的编辑器；具体执行效果取决于编辑器和格式化工具。

默认使用 UTF-8、CRLF 换行、文件末尾换行并清理行尾空格。C# 使用四个空格缩进、大括号另起一行；XAML、XML、项目文件、YAML、JSON 和 Markdown 使用两个空格缩进。

可以按团队习惯修改 `.editorconfig`。添加文件不会自动格式化已有代码，需要时执行编辑器的格式化命令。此组件不增加运行时服务，不改变应用行为。

移除命令：`xamlnexus remove editorconfig`。移除配置不会撤销之前的格式化结果；已修改的受管理文件会受到事务检查保护。

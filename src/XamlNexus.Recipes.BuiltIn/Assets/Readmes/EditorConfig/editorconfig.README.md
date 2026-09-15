# EditorConfig

[English](editorconfig.README.md) | [简体中文](editorconfig.README.zh-CN.md)

The `.editorconfig` file defines editor conventions for this repository. No runtime registration or application configuration is needed. Use an editor that supports EditorConfig, such as Visual Studio; enforcement depends on the editor and formatter.

Defaults include UTF-8, CRLF line endings, a final newline, and removal of trailing whitespace. C# uses four-space indentation and braces on new lines. XAML, XML, project files, YAML, JSON, and Markdown use two-space indentation.

Adjust `.editorconfig` to your team's conventions. Adding the file does not reformat existing files automatically; use your editor's formatting command when needed. This Recipe adds no runtime service and does not change application behavior.

Removal: `xamlnexus remove editorconfig`. Removing the configuration does not undo previous formatting. Edited managed files are protected by transaction checks.

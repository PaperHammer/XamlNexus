# XAML-Nexus

![NuGet Version](https://img.shields.io/nuget/v/XamlNexus)
[![GitHub stars](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fapi.github.com%2Frepos%2FPaperHammer%2FXamlNexus&query=stargazers_count&label=Stars&color=pink)](https://github.com/PaperHammer/XamlNexus/stargazers)
[![Documentation](https://img.shields.io/badge/Docs-Wiki-green)](https://github.com/PaperHammer/XamlNexus/wiki)
[![Issues](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fapi.github.com%2Frepos%2FPaperHammer%2FXamlNexus&query=open_issues&label=Issues&color=orange)](https://github.com/PaperHammer/XamlNexus/issues)

## ✨ Project Overview

This project is a .NET CLI-based scaffolding tool designed for quickly generating Windows desktop application prototypes, supporting common XAML frameworks such as WPF and WinUI 3.

With unified project templates and a command-line workflow, developers can create project structures with a single command, whether it's a standard desktop application or a front-backend separated architecture. It also comes with the custom UI style framework ArcXaml by default, enabling fast construction of consistent and extensible application interfaces.

This tool is suitable for prototype validation, internal tool development, and the initialization phase of standardized desktop projects.

---

## 🛠 Installation & Commands

⚠️ All commands should be executed in the project root directory.

**Installation：**

```
# Package the project
dotnet pack

# Install globally from local source
dotnet tool install --global --add-source ./XamlNexus/bin/Release XamlNexus
```

**Usage：**

```
# Launch the interactive scaffolder
xamlnexus
```

**Update：**

```
# Update to the latest local build
dotnet pack
dotnet tool update --global --add-source ./XamlNexus/bin/Release XamlNexus
```

**Uninstallation：**

```
# Remove the tool from your system
dotnet tool uninstall --global XamlNexus
```

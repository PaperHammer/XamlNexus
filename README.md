# XAML-Nexus

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
dotnet tool install --global --add-source ./XamlNexus/bin/Release XamlNexus（）
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
dotnet tool update --global --add-source ./bin/Release XamlNexus
```

**Uninstallation：**

```
# Remove the tool from your system
dotnet tool uninstall --global XamlNexus
```

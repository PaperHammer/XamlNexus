# Settings panel

[English](README.md) | [简体中文](README.zh-CN.md)

This project provides the application settings page: background material, language, storage location, and diagnostic log export. The standard profile includes it; basic projects can add it with `xamlnexus add settings`.

The scaffold registers the settings module and navigation automatically. The project appears under the solution's Panels folder. Extend Views and ViewModels here to add application-specific settings; persistence belongs to the user settings model and client, so adding a control alone does not persist its value.

In pure WinUI, updater adds the update service and settings entry; configure its HTTPS manifest URL as described in the updater README. Tray adds a window-close behavior setting, regardless of which Recipe is installed first. These optional services are not installed simply by adding settings. Hybrid uses its existing host services.

Changing the interface language takes effect immediately; changing the background material requires restarting the UI. Storage relocation copies supported application files and keeps originals; it does not move the SQLite database.

No extra configuration is needed for the default settings page. For a Recipe-installed panel, stop the app before `xamlnexus remove settings`; dependent Recipes must be removed first. Template-owned settings cannot be removed as a Recipe.

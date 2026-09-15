# Localization

[English](localization.md) | [简体中文](localization.zh-CN.md)

Generated applications support switching Chinese and English without restarting.
The settings page applies the language to existing controls and saves it for the
next launch. A save failure restores the previous selection and language and
shows an error. Changing language preserves navigation and other settings.

## Localize a control

Use the existing WinUI3Localizer attached property in a page or user control:

```xml
<Page xmlns:l="using:WinUI3Localizer" ...>
    <TextBlock l:Uids.Uid="Orders_Title" />
</Page>
```

Add the corresponding property to both
`<App>.UIComponent/Strings/zh-CN/Resources.resw` and
`<App>.UIComponent/Strings/en-US/Resources.resw`:

```xml
<data name="Orders_Title.Text" xml:space="preserve">
  <value>Orders</value>
</data>
```

Use the Chinese translation in the Chinese resource file. The property suffix
matches the control property: `.Text`, `.Content`, `.Title`, or `.Message`.
A single Uid can localize multiple properties, such as an InfoBar title and
message. Existing controls update in place; there is no need to navigate away
or recreate the window.

The older `I18n` markup extension returns a one-time string and remains available
for compatibility. Use `l:Uids.Uid` for new UI that needs live switching.

## Text produced by code

`LanguageUtil.GetI18n(key)` reads the currently active translation. For text
stored in a view model, subscribe to `LanguageUtil.LanguageUpdated`, update the
translated properties, and raise property-change notifications. Unsubscribe when
the view model is disposed. Preserve non-language state such as selected option
indices, progress, and timestamps while refreshing labels.

`await LanguageUtil.SetLanguageAsync(code)` changes the active language and
notifies listeners. Call it on the UI thread. This method changes runtime
localization only; the settings page also persists the chosen language through
`IUserSettingsClient` and handles rollback. Both official Presets expose the same
UI-side API.

Application-owned translated text follows the selected application language.
Windows-provided dialog buttons, shell UI, and third-party notifications can
continue using the Windows display language. Exception messages retain their
original diagnostic text.


## Packaged resource refresh

On each packaged application launch, `LanguageUtil` copies the bundled Chinese and
English `Strings/<language>/Resources.resw` files into LocalFolder with replacement,
then builds the localizer. Existing local copies therefore receive the current
package contents after an MSIX upgrade, including changed and removed translations.
The selected language still comes from user settings.

These local copies are application-managed resources, not a supported place for user
translation overrides. Change the source `.resw` files and ship them in the package.
If loading or copying a bundled file fails, initialization reports the failure through
the existing startup error handling; it does not silently load an outdated copy.
The unpackaged resource-loading path is unchanged.

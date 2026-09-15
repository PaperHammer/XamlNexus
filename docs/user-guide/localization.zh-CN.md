# 本地化

[English](localization.md) | [简体中文](localization.zh-CN.md)

生成应用支持不重启切换中英文。设置页将语言应用到已有控件，并保存供下次启动使用；保存失败会恢复原选择和语言并显示错误。切换语言保留导航状态和其他设置。

## 本地化控件

在页面或用户控件上使用现有 WinUI3Localizer 附加属性：

```xml
<Page xmlns:l="using:WinUI3Localizer" ...>
    <TextBlock l:Uids.Uid="Orders_Title" />
</Page>
```

在 `<App>.UIComponent/Strings/zh-CN/Resources.resw` 和 en-US 对应文件中添加属性资源：

```xml
<data name="Orders_Title.Text" xml:space="preserve">
  <value>Orders</value>
</data>
```

中文文件填写中文译文。属性后缀需匹配控件属性，例如 `.Text`、`.Content`、`.Title` 或 `.Message`。一个 Uid 可以本地化多个属性，例如 InfoBar 标题和内容。已有控件原地刷新，无需离开页面或重建窗口。

旧的 I18n 标记扩展只返回一次性字符串，继续保留以兼容旧代码；需要动态切换的新界面使用 `l:Uids.Uid`。

## 代码生成的文本

`LanguageUtil.GetI18n(key)` 读取当前译文。ViewModel 保存的文本需要订阅 `LanguageUtil.LanguageUpdated`，刷新属性并触发属性变化通知，释放时取消订阅。刷新标签时保留选项索引、进度、时间戳等非语言状态。

在 UI 线程调用 `await LanguageUtil.SetLanguageAsync(code)` 改变活动语言并通知监听者。此方法只改变运行时本地化；设置页额外通过 IUserSettingsClient 持久化并处理回滚。两种架构提供相同的 UI 端 API。

应用自己的文本遵循所选语言。Windows 对话框按钮、Shell UI 和第三方通知可能继续使用系统显示语言。异常消息保留原始诊断文本。

## 打包资源刷新

每次打包应用启动时，LanguageUtil 将随包的中英文 `Strings/<language>/Resources.resw` 替换复制到 LocalFolder，再构建本地化器。因此 MSIX 升级后，本地副本包含当前包新增、修改和删除后的翻译；所选语言仍来自用户设置。

这些副本是应用管理的资源，不是用户翻译覆盖入口。需要定制时修改源码 resw 并随包发布。读取或复制失败会进入已有启动错误处理，不会静默使用旧副本。非打包路径保持原有资源加载方式。

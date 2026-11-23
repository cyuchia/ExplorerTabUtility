# Explorer Tab Utility

本仓库基于 [w4po/ExplorerTabUtility](https://github.com/w4po/ExplorerTabUtility)，遵循原项目的 MIT 许可证。

主要功能：

- 界面语言支持跟随系统、简体中文、English；文字使用 Windows 界面字体，图标使用独立字体。
- 偏好设置可管理开机自启动，并与托盘菜单、Windows 中的实际启动状态同步。
- 配置使用原子保存和备份恢复；减少重复保存和连续调整窗口尺寸时的写入。
- 外部文件定位先显示目标窗口，再应用并核对选中文件；新建且已就绪的标签可以立即复用。
- 可选原生扩展在临时 Explorer 窗口显示前进行隐藏；转接失败或控制程序退出时恢复窗口。

[构建与测试](docs/BUILDING.md) · [原生扩展说明](docs/NATIVE-BRIDGE.md) · [验证范围](docs/VALIDATION.md)

原生扩展需单独选择安装，当前验证环境为 Windows 11 build 26300 x64。上游官方安装包不包含这些修改；请按构建说明生成本仓库的程序。

原项目的功能介绍、作者信息与支持方式见 [README](README.md)。

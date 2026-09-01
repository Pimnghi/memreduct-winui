# 更新日志

<p align="center">
  <strong>简体中文</strong> ·
  <a href="CHANGELOG_EN.md">English</a>
</p>

本文件记录 Mem Reduct WinUI 的正式版本及主要变化。

## 未发布

当前没有未发布的变更。

## [1.2.0] - 2026-09-01

[GitHub Release](https://github.com/Pimnghi/memreduct-winui/releases/tag/v1.2.0) ·
[完整变更](https://github.com/Pimnghi/memreduct-winui/compare/v1.1.0...v1.2.0)

本版本着重改善设置页面的原生 WinUI 交互与视觉一致性，并修复托盘菜单和页面布局问题。

### 改进

- 内存清理区域使用原生复选框，将勾选框放在文字左侧；保留危险区域的禁用保护。
- 点击整组快捷键键帽即可打开编辑界面，移除独立铅笔图标，并扩大悬停和点击区域。
- 设置开关在滑块左侧显示本地化“开/关”状态，采用原生控件的标准间距。
- 规范源码、构建脚本和安装资源目录，统一使用 `MemReduct.WinUI` 模块命名；
  对外程序文件名和配置路径保持不变。

### 修复

- 稳定托盘右键菜单的背景效果和选项高度。
- 修复目录调整后标题栏及关于页的应用图标缺失。
- 消除设置页和关于页滚动时的顶部固定留白，同时保留初始页面间距。

## [1.1.0] - 2026-08-18

[GitHub Release](https://github.com/Pimnghi/memreduct-winui/releases/tag/v1.1.0)

本版本完善托盘内存监控体验，并改进项目的独立构建与发布流程。

托盘内存使用率功能源自 [Issue #1](https://github.com/Pimnghi/memreduct-winui/issues/1)，
感谢 @kokpk 提出的建议。

### 新增

- 新增可选的托盘数字图标，用整数显示物理内存使用率，并根据警告和危险阈值
  使用对应的状态颜色。
- 托盘图标悬停提示现在同时显示物理内存、虚拟内存和系统工作集的精确使用率。

### 改进

- 优化数字托盘图标的边缘绘制，消除 Shell 缩放后出现的黑边。
- 为数字托盘图标设置项使用兼容 Windows 10/11 的使用率仪表图标。
- 完善独立仓库构建支持和 x64/ARM64 GitHub Actions 验证。
- 将便携包与安装包按版本保存，重新构建当前版本时不再删除旧版本产物。
- 完善中英文项目文档和仪表盘截图。

## [1.0.0] - 2026-07-31

[GitHub Release](https://github.com/Pimnghi/memreduct-winui/releases/tag/v1.0.0)

首个稳定版本，使用原生 WinUI 3 界面重新设计 Mem Reduct，并保留 Windows
Native API 内存清理能力。

### 主要功能

- 实时显示物理内存、虚拟内存和系统工作集状态。
- 支持多种内存区域，并报告完全成功、部分成功和失败结果。
- 提供手动、托盘、全局快捷键、自动清理和命令行清理入口。
- 支持基于占用阈值和时间间隔的自动清理，以及危险区域保护。
- 提供 Fluent 风格托盘菜单、Windows 通知、单实例和计划任务开机启动。
- 支持深色、浅色和系统主题，以及 30 多种界面语言。
- 提供 `mrw-cli.exe -clean` 和 `mrw-cli.exe -clean:full` 命令。

### 发布形式

- Windows 10 1809 或更高版本、Windows 11。
- x64 和 ARM64。
- 自包含的便携 ZIP 与 per-machine 安装程序。
- 便携版配置位于程序目录的 `data` 文件夹；安装版配置位于
  `%ProgramData%\Mem Reduct WinUI\data`。

### 注意事项

- 程序需要管理员权限。
- 1.0.0 二进制文件未进行 Authenticode 签名，因此 UAC 会显示“未知发布者”。
- 项目基于 Henry++ 的 Mem Reduct，并按照 GPL-3.0 许可证发布。

# 更新日志

<p align="center">
  <strong>简体中文</strong> ·
  <a href="CHANGELOG_EN.md">English</a>
</p>

本文件记录 Mem Reduct WinUI 的正式版本及主要变化。

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

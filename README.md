# Mem Reduct WinUI

基于 [Mem Reduct](https://github.com/henrypp/memreduct) 核心引擎，使用 WinUI 3 原生界面重构的实时内存管理工具。

## 功能特性

- 实时监控物理内存、虚拟内存、系统工作集的占用情况
- 通过 Windows Native API 一键清理系统缓存
- 系统托盘图标，支持右键菜单和气泡通知
- 多语言支持（30+ 种语言）
- 基于阈值或时间间隔的自动清理
- 全局快捷键清理
- 跟随 Windows 系统深色/浅色主题
- 命令行模式（`-clean`、`-clean:full`）
- 单实例运行、最小化到托盘
- 开机自启（计划任务，无 UAC 弹窗）

## 系统要求

- Windows 10 版本 17763 或更高（64 位或 ARM64）
- 管理员权限（启动时自动提权）

## 构建

### 环境

- .NET 9.0 SDK
- Visual Studio 2022，勾选"使用 C++ 的桌面开发"工作负载

### 步骤

```powershell
# 编译 native C DLL
msbuild CoreLib\CoreLib.vcxproj -p:Configuration=Release -p:Platform=x64

# 编译 WinUI 应用
dotnet build memreduct-winui.csproj -c Release
dotnet build memreduct-winui.csproj -c Release -p:Platform=ARM64
```

输出目录：`bin\Release\net9.0-windows10.0.26100.0\win-x64\memreduct-winui.exe`

## 项目结构

```
memreduct-winui/
├── App.xaml(.cs)              应用入口
├── MainWindow.xaml(.cs)       主窗口、导航、托盘、热键
├── MainPage.xaml(.cs)         内存统计 + 清理
├── SettingsPage.xaml(.cs)     设置页面
├── AboutPage.xaml(.cs)        关于页面
├── Core/                      C# 桥接层
│   ├── CoreService.cs          C DLL 的 C# 封装
│   ├── NativeMethods.cs        P/Invoke [DllImport] 声明
│   ├── IniConfig.cs            INI 配置读写
│   ├── TrayIcon.cs             托盘图标 + 右键菜单
│   ├── AutoCleanService.cs     后台自动清理定时器
│   ├── ToastService.cs         气泡通知
│   └── StrId.cs                字符串资源 ID 常量
├── CoreLib/                   Native C DLL
│   ├── core.h / core.c         内存清理核心逻辑
│   ├── CoreLib.def             导出函数列表
│   ├── CoreLib.vcxproj         MSBuild 项目
│   └── routine/                共享 C 库
├── language/
│   └── memreduct-winui.lng     多语言翻译文件
├── Assets/                     应用图标和图片
├── app.manifest                Win32 应用清单
└── memreduct-winui.csproj      项目文件
```

## 配置

所有设置保存在 `data\memreduct-winui.ini`（exe 同级目录下的便携模式）。

## 开源许可

基于 [Mem Reduct](https://github.com/henrypp/memreduct)（Henry++），MIT License。

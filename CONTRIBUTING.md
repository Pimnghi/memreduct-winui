# Contributing / 参与贡献

[English](#english) · [简体中文](#简体中文)

## English

Thank you for helping improve Mem Reduct WinUI. Questions, bug reports, and
feature requests are handled through the repository's Issue Forms.

### Before opening a pull request

1. Search existing Issues and open one first for behavior changes or larger
   design proposals.
2. Fork the repository and create a focused branch from `main`.
3. Keep changes scoped. Do not mix formatting-only changes with functional
   work.
4. Follow the architecture and build notes in `README_EN.md`.

### Build and test

Run the complete publish script for code changes:

```powershell
.\build-publish.ps1
```

Changes to native code, P/Invoke declarations, packaging, or build logic must
be verified for both x64 and ARM64. Documentation-only changes do not require a
full dual-architecture build, but paths, commands, versions, and links must be
checked.

For UI changes, test light and dark themes, common DPI values, narrow and wide
windows, and all affected languages. New user-visible strings must be added to
the English fallback and every section of `language\memreduct-winui.lng`.

### Repository hygiene

- Do not commit `artifacts`, `bin`, `obj`, PDB files, logs, INI files, IDE
  state, or local user data.
- Do not commit generated release packages.
- Preserve the C ABI between `CoreLib` and the managed application.
- Route every cleanup entry point through `CleanupCoordinator`.
- Treat warnings as errors and resolve the cause instead of suppressing it.

### Pull requests

Complete the pull request template, describe the affected architectures and
Windows versions, and state whether localization or generated files changed.
Small, reviewable pull requests are preferred.

## 简体中文

感谢你帮助改进 Mem Reduct WinUI。使用问题、错误报告和功能建议统一通过仓库的
Issue Form 提交。

### 提交 Pull Request 前

1. 先搜索已有 Issue；涉及行为变化或较大设计调整时，请先创建 Issue 讨论。
2. Fork 仓库，并从 `main` 创建职责单一的分支。
3. 控制改动范围，不要把纯格式调整与功能修改混在一起。
4. 遵循 `README.md` 中的架构与构建说明。

### 构建与验证

代码改动应运行完整发布构建：

```powershell
.\build-publish.ps1
```

涉及 Native、P/Invoke、打包或构建逻辑的改动必须同时验证 x64 和 ARM64。仅修改
文档时无需完整双架构构建，但必须核对路径、命令、版本和链接。

界面改动应验证浅色/深色主题、常见 DPI、宽窄窗口以及受影响的语言。新增用户
可见文本时，必须同时补充英文 fallback 和
`language\memreduct-winui.lng` 的所有语言 section。

### 仓库整洁

- 不要提交 `artifacts`、`bin`、`obj`、PDB、日志、INI、IDE 状态或本地用户数据。
- 不要提交生成的发布包。
- 保持 `CoreLib` 与托管应用之间的 C ABI 一致。
- 所有清理入口必须通过 `CleanupCoordinator`。
- 项目将警告视为错误，请修复原因，不要直接屏蔽警告。

### Pull Request

请完整填写 PR 模板，说明验证过的架构和 Windows 版本，并注明本地化及生成文件
是否受到影响。推荐提交范围清晰、便于审查的小型 PR。

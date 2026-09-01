# 参与贡献

<p align="center">
  <strong>简体中文</strong> ·
  <a href="CONTRIBUTING_EN.md">English</a>
</p>

感谢你帮助改进 Mem Reduct WinUI。使用问题、错误报告和功能建议统一通过仓库的
Issue Form 提交。

## 提交 Pull Request 前

1. 先搜索已有 Issue；涉及行为变化或较大设计调整时，请先创建 Issue 讨论。
2. Fork 仓库，并从 `main` 创建职责单一的分支。
3. 控制改动范围，不要把纯格式调整与功能修改混在一起。
4. 遵循 `README.md` 中的架构与构建说明。

## 构建与验证

代码改动应运行完整发布构建：

```powershell
.\scripts\build-publish.ps1
```

涉及 Native、P/Invoke、打包或构建逻辑的改动必须同时验证 x64 和 ARM64。仅修改
文档时无需完整双架构构建，但必须核对路径、命令、版本和链接。

界面改动应验证浅色/深色主题、常见 DPI、宽窄窗口以及受影响的语言。新增用户
可见文本时，必须同时补充英文 fallback 和
`src\MemReduct.WinUI\language\memreduct-winui.lng` 的所有语言 section。

## 仓库整洁

- 不要提交 `artifacts`、`bin`、`obj`、PDB、日志、INI、IDE 状态或本地用户数据。
- 不要提交生成的发布包。
- 保持 `CoreLib` 与托管应用之间的 C ABI 一致。
- 所有清理入口必须通过 `CleanupCoordinator`。
- 项目将警告视为错误，请修复原因，不要直接屏蔽警告。

## Pull Request

请完整填写 PR 模板，说明验证过的架构和 Windows 版本，并注明本地化及生成文件
是否受到影响。推荐提交范围清晰、便于审查的小型 PR。

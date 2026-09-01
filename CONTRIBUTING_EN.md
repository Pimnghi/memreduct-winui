# Contributing

<p align="center">
  <a href="CONTRIBUTING.md">简体中文</a> ·
  <strong>English</strong>
</p>

Thank you for helping improve Mem Reduct WinUI. Questions, bug reports, and
feature requests are handled through the repository's Issue Forms.

## Before opening a pull request

1. Search existing Issues and open one first for behavior changes or larger
   design proposals.
2. Fork the repository and create a focused branch from `main`.
3. Keep changes scoped. Do not mix formatting-only changes with functional
   work.
4. Follow the architecture and build notes in `README_EN.md`.

## Build and test

Run the complete publish script for code changes:

```powershell
.\scripts\build-publish.ps1
```

Changes to native code, P/Invoke declarations, packaging, or build logic must
be verified for both x64 and ARM64. Documentation-only changes do not require a
full dual-architecture build, but paths, commands, versions, and links must be
checked.

For UI changes, test light and dark themes, common DPI values, narrow and wide
windows, and all affected languages. New user-visible strings must be added to
the English fallback and every section of
`src\MemReduct.WinUI\language\memreduct-winui.lng`.

## Repository hygiene

- Do not commit `artifacts`, `bin`, `obj`, PDB files, logs, INI files, IDE
  state, or local user data.
- Do not commit generated release packages.
- Preserve the C ABI between `CoreLib` and the managed application.
- Route every cleanup entry point through `CleanupCoordinator`.
- Treat warnings as errors and resolve the cause instead of suppressing it.

## Pull requests

Complete the pull request template, describe the affected architectures and
Windows versions, and state whether localization or generated files changed.
Small, reviewable pull requests are preferred.

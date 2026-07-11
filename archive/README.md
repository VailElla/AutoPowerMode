# 本地归档

本目录用于保留历史本地构建，避免它们继续与当前源码混在一起。

- `releases/`：历史本地发布包和校验文件。
- `legacy-builds/`：旧构建和审查产物，包含已过时的自包含发布包。

两个产物目录都被 Git 忽略，仅供本地历史查询，不得当作当前发布资产。当前 GitHub Release 应统一由 `.github/workflows/release.yml` 构建。

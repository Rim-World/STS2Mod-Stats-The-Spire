# TOOLS 工具登记

本版本未向项目 `TOOLS` 目录安装便携工具。分析使用本机已有工具：

| 工具 | 版本 | 来源 | 用途 |
|---|---|---|---|
| .NET SDK | 9.0.321 | 本机 `C:\Program Files\dotnet` | 构建 |
| ilspycmd | 9.1.0.7988 | 本机 `%USERPROFILE%\.dotnet\tools` | 反编译与差异分析 |

## 规则

- 新增便携工具必须放入 `TOOLS/<工具名>/`，并在此登记版本、来源、大小、环境变量与安装方式。
- 自写独立脚本放入 `TOOLS/scripts/`，并建立 `scripts.md`。
- `TOOLS` 一级目录只允许工具文件夹、`scripts/` 与 `TOOLS.md`。

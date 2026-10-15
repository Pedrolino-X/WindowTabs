# WindowTabs 修复增强版

将多个应用窗口组合在同一条标签栏中，使用原有右键菜单、拖拽和设置窗口管理它们。本分支沿用原版界面和操作流程，重点修复窗口状态、资源释放和多窗口拖动性能。

基于 [standard-software/WindowTabs](https://github.com/standard-software/WindowTabs) 的 `23db51a11a90cdd57377d0ef86a002496163dd78`（ss_2026.08.28）。保留 Maurice Flanagan 及后续维护者的源码历史与 MIT 许可。

## 当前改进

- 修复分组最小化、恢复时成员窗口消失或无法激活的问题。
- 修复组内置顶窗口妨碍标签切换，以及子窗口、模态对话框出现时错误跳回原标签的问题。
- 修复绘图、图标及任务栏预览资源释放；减少重复布局和重绘。
- 后台标签窗口可最小化，拖动时避免不断调整所有后台窗口；标签栏移动复用已绘制的图像。
- 默认自动分组，可在“设置 → 程序 → 自动分组”按程序关闭并保存。
- 默认合并当前组任务栏图标，可在设置中修改全局默认；单一程序组的右键开关按程序名保存，混合程序组不写入程序偏好。
- 标签支持居左、居中、居右；单一程序组可保存对齐偏好。
- 标签栏右侧增加“+”，从当前激活标签启动新窗口；资源管理器新窗口沿用当前活动窗口的文件夹路径。
- 提供简体、繁体中文新增菜单和设置文案。

详细变更见 [version.md](version.md)。本仓库不包含已弃用的重写版本。

## 使用

从本 fork 的 [Releases](https://github.com/Pedrolino-X/WindowTabs/releases) 下载便携包，解压到独立目录并运行 `WindowTabs.exe`。升级前通过托盘菜单正常退出旧版，再替换程序文件；保留自己的设置文件。不要同时运行多个版本管理同一批窗口。

## 编译

需要 Windows、Visual Studio 或 Build Tools（MSBuild、.NET Framework 4.7.2 开发工具），以及可通过 `python` 命令运行的 Python 3。

```powershell
powershell -ExecutionPolicy Bypass -File .\build-repair.ps1
```

脚本自动查找 MSBuild，并从 NuGet 下载本工程使用的 F# 编译器及依赖。可用 `-PythonPath`、`-MSBuildPath` 指定工具路径。输出位于 `WtProgram\bin\Release`。

构建阶段会在 `.tools/compat` 生成旧版 F# 编译器需要的兼容源码；开发时编辑 `WtProgram` 下的原始文件。构建不会替换已安装的程序或重启正在运行的 WindowTabs。

## 回归测试

```powershell
powershell -ExecutionPolicy Bypass -File .\build-repair.ps1 -Test
```

测试需要已登录的 Windows 图形桌面，会创建并关闭自己的测试窗口。覆盖布局、资源释放、256 标签移动、32 窗口后台最小化、组最小化/恢复、任务栏代理、置顶切换及模态对话框激活。其他针对性测试源码也在 `Tests` 中。

## 来源和许可

本项目遵循 [MIT License](LICENSE)。原版介绍与完整操作说明见 [README.md](README.md)，上游日文说明见 [README_Japanese.md](README_Japanese.md)。感谢原作者 Maurice Flanagan、payaneco、leafOfTree、standard-software 及历代贡献者。

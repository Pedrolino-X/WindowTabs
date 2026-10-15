# WindowTabs — Fixes and Enhancements

[简体中文](README.md) | **English** | [日本語](README.ja.md)

**Bring multiple Windows application windows into one tab group, and manage them with familiar clicks, drag-and-drop, and context menus.**

Maintained by [Pedrolino-X](https://github.com/Pedrolino-X), this fork is based on standard-software's WindowTabs. It preserves the original interface and workflow while improving performance with many windows, resource cleanup, and window switching stability. It is intended for workflows with multiple File Explorer, browser, office, and development-tool windows open at once.

[Download portable version](https://github.com/Pedrolino-X/WindowTabs/releases/latest) · [Changelog](version.md) · [Report an issue](https://github.com/Pedrolino-X/WindowTabs/issues)

![WindowTabs tab grouping example from upstream](README_Image/Tabs.png)

*This image from the upstream documentation shows the original tab style retained by this fork. New features are described below.*

## Download and get started

1. Download `WindowTabs-2026.10.15.zip` from [this repository's Releases](https://github.com/Pedrolino-X/WindowTabs/releases) and extract the entire archive into a directory.
2. Run `WindowTabs.exe`. Windows and .NET Framework 4.7.2 or later are required.
3. Right-click the WindowTabs system tray icon to open Settings. Under Programs, choose which applications receive tabs and whether they are grouped automatically. You can also change the language through the tray menu.
4. Click a tab to switch windows. Drag a tab into another group to combine them, out of a group to detach it, or within the group to reorder it. Right-click a tab to configure taskbar grouping, alignment, or launch a new window.

Upgrading: exit the old version normally through the tray menu before replacing the application files.

The main configuration is stored at `%APPDATA%\WindowTabs\WindowTabsSettings.txt`. Copy the entire `%APPDATA%\WindowTabs` folder to back up or migrate settings.

**Use this repository's Releases for future updates.** The application's current “Check for Updates” command still queries standard-software's upstream repository and has not yet been switched to this fork.

## What this version fixes

| Scenario | Fix or improvement |
| --- | --- |
| Minimizing and restoring a group of windows | Fixes members disappearing from the tab group or remaining in the taskbar without opening correctly. |
| A group member is set to always on top | Fixes it blocking activation of other tabs, and preserves its original topmost state when detached. |
| An application opens an editor, message box, or other owned window | Uses window ownership to handle focus and prevent jumping back to the original tab. Returning to a tab activates its associated dialog correctly. This applies to general window relationships. |
| A new window joins an existing group | Reuses the group's current position to fix misalignment between the new window and the tab strip. |
| Moving or resizing windows with many tabs | Reuses tab images, reduces repeated layout work, and restores background windows on demand instead of repositioning every member on each drag. |
| Extended use with repeated icon and taskbar preview creation | Fixes cleanup of images, drawing objects, and taskbar proxy resources to reduce accumulation. |

The most noticeable change is that stutters and freezes are gone, and window state changes feel smarter and more natural. Together with ChatGPT 6 Astra, we addressed memory leaks and other problems in the original version.
These changes retain the original settings window, tab context menus, and drag-in/drag-out workflow.

## Features and defaults

### Automatic grouping

Automatic grouping is enabled by default. To keep an application's windows separate, turn it off under **Settings → Programs → Auto grouping**. Your choice is saved and applied to that application afterward.

### Combine taskbar icons

Taskbar icons are combined for new groups by default. Change **Combine taskbar icons for new groups by default** in the Behavior settings. For an individual group, right-click a tab and toggle **Combine taskbar icons for this group**.

When every member belongs to the same application, the context-menu choice is saved by executable name. For example, disabling taskbar grouping for a File Explorer group also applies to future File Explorer groups. A group containing different applications changes only the current group and does not write a persistent preference for any application.

### Left, center, and right alignment

The tab context menu can align the entire group to the left, center, or right. Groups containing a single application save the corresponding application preference. Changes to mixed-application groups apply to the current group.

### The “+” button and File Explorer folder inheritance

Click **+** at the right end of the tab strip to launch a new window using the currently active tab's application and insert it immediately to that tab's right. This uses the same flow as the context menu's new-tab command.

For File Explorer, the new window opens the folder shown in the currently active Explorer window. If a group has “Downloads” and “Project” windows, activating “Project” and clicking + opens another “Project” window, regardless of which tab is rightmost.

### Minimize background tabs

**Minimize background tabs (all groups)** is enabled by default and can be disabled from a tab's context menu. With background members minimized, dragging mainly updates the active window. Switching tabs restores the selected window to the group's current position.

Modal dialogs and the owner windows they depend on keep the states they need, so background minimization does not interrupt password entry or editor operations.

## Performance improvements and verification

The changes reduce the work needed for each movement: reuse rendered images when tab content has not changed, reuse existing surfaces for position updates, coalesce queued position events, and avoid repeatedly moving background windows. The previous 50 ms position-update throttle has been removed during actual dragging.

Before the `2026.10.15` release, dependencies were restored in a separate directory, the project was rebuilt, and the full regression suite passed:

| Test | Result |
| --- | --- |
| Continuous movement with 256 tabs | Image-cache and position-update tests passed. |
| 32 real test windows, 100 drag steps | Zero additional positioning messages to background windows; switching to the first, middle, and last tabs passed. |
| Group minimize/restore, detach/rejoin, and topmost switching | Passed. |
| Focus chain involving an owner, editor, and modal dialog | Passed, including a dialog receiving focus before its editor joins the group. |
| Single-application preferences and mixed groups not overwriting application rules | Passed. |
| 1,000 taskbar preview callbacks | GDI object count stayed at 31 → 31, with no generation 2 garbage collections. |
| Creating and releasing 300 taskbar preview windows | Window-class and callback cleanup tests passed. |

These are the results of this automated test run. Long-term sleep/wake cycles and different third-party application combinations still need ongoing real-world feedback.

## Build from source

You need Windows, Python 3, and Visual Studio / Build Tools with MSBuild and the .NET Framework 4.7.2 development tools. The first build needs access to NuGet to download dependencies.

```powershell
git clone https://github.com/Pedrolino-X/WindowTabs.git
cd WindowTabs
powershell -ExecutionPolicy Bypass -File .\build-repair.ps1
```

The script locates MSBuild automatically, invokes Python through the `python` command, and restores fixed versions of the F# compiler and project dependencies. Output is written to `WtProgram\bin\Release`, including the executable, dependency libraries, and language/settings resources.

Use `-PythonPath` and `-MSBuildPath` to specify full paths when your tools are installed elsewhere. Building does not replace an installed WindowTabs or restart a running instance.

```powershell
# Build and run the main regression tests
powershell -ExecutionPolicy Bypass -File .\build-repair.ps1 -Test
```

Tests require a signed-in Windows graphical desktop and create and close their own test windows. Additional targeted test sources are in [`Tests`](Tests).

| Directory or file | Purpose |
| --- | --- |
| `WtProgram` | F# application, tabs, groups, settings, and language resources. |
| `Win32` | Native Windows interfaces and helpers. |
| `Tests` | Layout, resource, and window-behavior regression tests. |
| `build-repair.ps1` | Build and main test entry point for this fork. |
| `.tools/prepare-build.py` | Generates compatible sources for the older F# compiler used by the project. Edit the original files in `WtProgram` during development. |

## Reporting issues

In [Issues](https://github.com/Pedrolino-X/WindowTabs/issues), include the application version, Windows version, affected applications, and reproduction steps. For window-switching problems, describe the group's applications, topmost state, background-minimization setting, and any dialogs. For stuttering during movement, include the window count and monitor scaling settings.

## Origins and license

This fork is based on the ss_2026.08.28 source of [standard-software/WindowTabs](https://github.com/standard-software/WindowTabs), at commit [`23db51a`](https://github.com/standard-software/WindowTabs/commit/23db51a11a90cdd57377d0ef86a002496163dd78). The original author is Maurice Flanagan. Thanks to payaneco, leafOfTree, standard-software, and all previous contributors.

The upstream Git history and [MIT license](LICENSE) are preserved.

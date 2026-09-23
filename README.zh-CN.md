# My Stickies

[한국어](README.md) | [English](README.en.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja.md)

一款停靠在 Windows 屏幕右侧边缘的便笺应用，交互方式受到 macOS 应用 [Hold My Notes](https://holdmynotes.app/) 的启发。

平时，便笺收起为一条细长的书签。将鼠标移到书签上可展开便笺栏，移到便笺上可查看内容，点击即可编辑。

以下截图暂时使用英文界面。

![](bookmark.webp)

![](memo.eng.webp)

![](memo_pinned.eng.webp)

![](memo_mgmt.eng.webp)

## 主要功能

- 支持韩语、英语、简体中文和日语：首次启动时选择语言，也可在设置中更改，无需重启
- 三种显示状态：书签、便笺标签、展开的便笺内容
- 点击编辑标题与正文，更改颜色，根据内容自动调整便笺高度
- 将便笺固定为置顶悬浮窗口，拖动左侧条带可移动；支持调整大小，重启后恢复固定状态、位置和手动调整的大小
- 隐藏便笺而不删除内容，再次点击确认删除，拖动便笺调整顺序
- “全部便笺”窗口支持搜索、筛选、编辑、恢复、Markdown/文本导出和导入
- 使用 SQLite 存储，可选择 Synology Drive 等同步文件夹
- 托盘图标、全局快捷键、全屏应用检测和登录 Windows 时自动启动
- 检查 GitHub 发布版本并通过安装程序更新应用
- 可设置显示器、便笺显示数量、位置、收起延迟、字体和字号
- 支持高 DPI 显示，包括 4K 显示器（PerMonitorV2）

## 系统要求

- Windows 10 或 11，x64
- 开发环境：.NET 9 SDK
- 发布的可执行文件包含 .NET 运行时，无需单独安装

## 构建与运行

在仓库根目录运行：

```powershell
# 构建
dotnet build MyStickies.sln -c Release

# 运行单元测试与回归测试
dotnet test tests\MyStickies.Tests\MyStickies.Tests.csproj -c Release

# 验证发布脚本，不实际发布
pwsh -NoProfile -File .\tests\ReleaseScript.Tests.ps1

# 从源代码运行
dotnet run --project src\MyStickies -c Release
```

可选启动参数：

| 参数 | 行为 |
|---|---|
| `--all-notes` | 启动时打开“全部便笺” |
| `--settings` | 启动时打开“设置” |

## 发布可执行文件

```powershell
pwsh .\publish.ps1
pwsh .\publish.ps1 -Run
pwsh .\publish.ps1 -Output D:\Apps\MyStickies
```

- 以 Release 配置发布 win-x64 单个可执行文件，包含 .NET 运行时
- 默认输出到 `%LocalAppData%\Programs\MyStickies\MyStickies.exe`
- 发布前会停止正在运行的 My Stickies 进程
- 请从发布后的应用中启用登录时自动启动；如果使用构建目录中的程序进行注册，清理该目录后自动启动可能失效

## 构建安装程序

先安装 Inno Setup，再进行构建：

```powershell
winget install JRSoftware.InnoSetup
pwsh .\build-installer.ps1
pwsh .\build-installer.ps1 -Install
```

安装程序输出到 `dist\MyStickies-Setup-<版本>.exe`。可选参数 `-Install` 会在构建完成后进行静默安装。

- 安装到当前用户的 `%LocalAppData%\Programs\MyStickies`，无需管理员权限
- 创建开始菜单快捷方式，可选择创建桌面快捷方式和登录时自动启动
- 安装前关闭正在运行的应用
- 卸载后保留便笺与设置，包括 `%LocalAppData%\MyStickies` 和所选便笺文件夹中的数据
- 可执行文件未进行代码签名，在其他电脑上运行时 Windows SmartScreen 可能显示提示

## 发布 GitHub Release

安装 GitHub CLI，并使用 `gh auth login` 登录。请先提交并推送包含版本更新的代码，然后运行：

```powershell
.\build-installer.ps1
.\release.ps1 1.0.10 "Add Simplified Chinese and Japanese language support"
```

- 第一个参数为版本号，第二个参数为发布说明
- 版本号必须与项目版本一致
- 创建 `v1.0.10` 发布版本，附加 `dist\MyStickies-Setup-1.0.10.exe`，并标记为最新版本
- 以当前提交作为标签目标，因此该提交必须已推送到 GitHub
- 如果安装程序不存在、工作区有未提交的更改或发布失败，脚本会停止
- 命令参考：[GitHub CLI — gh release create](https://cli.github.com/manual/gh_release_create)

## 使用方法

### 语言

首次启动时，先选择 한국어、English、简体中文或日本語，再选择便笺文件夹。之后可右键点击托盘图标，打开“设置”，选择语言并点击“确定”。菜单和已打开的窗口会立即更新。

语言保存在本机，重启后恢复。旧版设置中未指定语言时保持韩语。切换语言不会翻译或替换已有便笺；新建便笺的默认标题和新生成的使用指南会使用所选语言。

### 便笺栏

| 操作 | 结果 |
|---|---|
| 将鼠标移到书签上 | 展开便笺标签 |
| 拖动书签上方的手柄 | 移动并保存书签位置；便笺栏中至少有一张便笺时可用 |
| 点击书签下方的按钮 | 隐藏书签与便笺栏；可通过托盘图标或全局快捷键重新显示 |
| 将鼠标移到便笺标签上 | 展开标题与正文 |
| 点击便笺 | 编辑标题或正文 |
| 上下拖动便笺 | 调整顺序 |
| 移开鼠标 | 在设定的延迟后收起便笺栏 |
| 右键点击书签或便笺 | 打开“全部便笺”“设置”“隐藏/显示便笺栏”和“退出”菜单 |

“隐藏便笺栏”会暂时隐藏书签与便笺栏。点击托盘图标或使用全局快捷键可重新显示。

### 便笺按钮

| 按钮 | 功能 |
|---|---|
| 便笺栏下方的 `+` | 新建便笺 |
| 右上角的图钉 | 将便笺分离为悬浮窗口并收起其余便笺；再次点击可放回便笺栏 |
| 右上角的 `−` | 隐藏便笺；之后可在“全部便笺”的“已隐藏”筛选中恢复 |
| 右上角的 `×` | 进入删除确认状态；三秒内再次点击可确认删除 |
| 编辑时的彩色圆点 | 更改便笺颜色 |

固定便笺在点击其他应用后仍保持置顶。拖动左侧条带可移动便笺；拖动右下角可调整宽度和高度，拖动底边只调整高度。手动调整后，编辑时仍保持该大小，超出部分可滚动查看。固定状态、位置和手动调整的大小按便笺文件夹保存在本机，重启后恢复。

悬浮便笺也支持编辑、隐藏和删除。隐藏或删除便笺时会同时取消固定。

### 编辑快捷键

| 快捷键或操作 | 功能 |
|---|---|
| 在标题栏按 Enter | 跳转到正文 |
| Ctrl+Enter 或 Ctrl+S | 保存并结束编辑 |
| Esc | 放弃修改 |
| 点击外部或选择其他便笺 | 保存并结束编辑 |

选择简体中文时，空标题会自动变为 `新便笺 (yyyy-MM-dd HH:mm)`。

### 全局快捷键

| 快捷键 | 功能 |
|---|---|
| Ctrl+Alt+S | 显示或收起便笺栏 |
| Ctrl+Alt+N | 新建便笺并开始编辑 |

如果与其他应用的快捷键冲突，可在设置中关闭全局快捷键。

### 全部便笺

通过托盘菜单或书签的右键菜单打开“全部便笺”。

- 按“全部”“未隐藏”或“已隐藏”筛选，并搜索标题与正文
- 点击详情卡片中的标题或正文进行编辑；Esc 取消，Ctrl+Enter 保存，选择其他便笺时自动保存
- 点击详情卡片底部的彩色圆点更改颜色
- 隐藏、恢复、确认删除或导出所选便笺
- 一次导入多个 `.md` 或 `.txt` 文件，将全部便笺导出到一个 Markdown 文件，或新建便笺

### 更新

应用在启动 15 秒后及每天检查一次最新 GitHub 发布版本。发现新版本时，会显示托盘通知，并将更新菜单项改为“安装更新 vX”。

- 查看版本说明，选择“立即安装”“稍后”或“跳过此版本”
- “立即安装”会下载安装程序、关闭应用、安装更新并重新启动
- 跳过的版本不会自动通知，但仍可从托盘菜单手动检查
- 在设置中关闭自动检查后，仅在手动操作时检查更新

发布附件必须命名为 `MyStickies-Setup-*.exe` 才能被识别。草稿和预发布版本会被忽略。

## 存储与同步

- 便笺存储在名为 `my_stickies.db` 的 SQLite 文件中
- 首次启动时选择语言与便笺文件夹；默认文件夹为 `%LocalAppData%\MyStickies`
- 可随时在设置中更改文件夹；已有数据库会直接加载，也可复制现有便笺或创建包含使用指南的新文件
- 语言、固定窗口的位置与大小等本机设置保存在 `%LocalAppData%\MyStickies\settings.json`
- 选择 Synology Drive 或 OneDrive 等同步文件夹，可在多台电脑间共享便笺；外部文件更改会自动重新加载
- 适用于单个用户；同时在两台电脑上编辑可能导致后同步的版本覆盖另一版本

数据库结构使用 `PRAGMA user_version` 管理，旧数据库会在启动时迁移。旧文件名 `notes.db` 也会自动改为 `my_stickies.db`。

## 设置

右键点击托盘图标并选择“设置”。

| 设置项 | 说明 |
|---|---|
| 语言 | 韩语、英语、简体中文或日语，无需重启 |
| 便笺文件夹 | 数据库文件的位置 |
| 停靠显示器 | 显示便笺栏的显示器，支持各显示器独立 DPI |
| 显示数量 | 便笺栏显示的最近便笺数量，可选 3 至 8 张 |
| 垂直位置 | 便笺栏中心相对屏幕高度的百分比 |
| 收起延迟 | 鼠标离开后收起便笺栏的等待时间 |
| 全屏时隐藏 | 同一显示器上有全屏应用位于前台时隐藏便笺栏 |
| 全局快捷键 | 启用 Ctrl+Alt+S 和 Ctrl+Alt+N |
| 字体与正文字号 | 选择系统字体，字号范围为 10 至 24 pt |
| 自动检查更新 | 启动时及每天检查新版本 |
| 登录时自动启动 | 在当前用户的 Run 注册表项中注册应用 |

## 项目结构

```text
my_stickies/
  MyStickies.sln
  publish.ps1                  可执行文件发布脚本
  build-installer.ps1          安装程序构建脚本
  release.ps1                  GitHub 发布脚本
  installer/MyStickies.iss     Inno Setup 定义
  src/MyStickies/
    App.xaml(.cs)              应用资源与单实例管理
    MainWindow.xaml(.cs)       停靠、便笺栏动画、排序与设置
    MainWindow.FloatingNotes.cs  固定便笺的生命周期与持久化
    Controls/NoteTab           便笺卡片、编辑、动画、颜色与按钮
    Windows/                   便笺管理、设置、语言选择、悬浮便笺与更新
    Data/                      SQLite 存储、设置、导出与自动启动注册
    Layout/                    布局、显示器、字体与悬浮窗口位置
    Localization/              四种语言的文本与即时语言切换
    Interop/                   Win32 窗口样式、DPI、全屏检测与全局快捷键
    Tray/                      托盘图标与菜单
    Models/                    便笺、调色板、使用指南与相对时间
    Assets/app.ico             应用图标
  tests/MyStickies.Tests/       xUnit 单元测试与回归测试
  tests/ReleaseScript.Tests.ps1  不实际发布的发布脚本测试
```

## 技术

- C#、WPF、.NET 9 和 Microsoft.Data.Sqlite
- Windows Forms 仅用于托盘图标
- 完全透明的窗口区域允许点击穿透；便笺栏展开时，近乎透明的悬停区域可在便笺之间及添加按钮周围保持便笺栏展开

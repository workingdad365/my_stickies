# My Stickies

[한국어](README.md) | [English](README.en.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja.md)

A sticky notes app docked to the right edge of your Windows screen, inspired by the interaction style of [Hold My Notes](https://holdmynotes.app/) for macOS.

Your notes stay tucked away as a thin bookmark. Hover over it to reveal the deck, hover over a note to read it, and click to edit.

![](bookmark.webp)

![](memo.eng.webp)

![](memo_pinned.eng.webp)

![](memo_mgmt.eng.webp)

## Features

- Korean, English, Simplified Chinese, and Japanese: choose a language on first launch and change it in Settings without restarting
- Three display states: bookmark, note tabs, and expanded note content
- Click to edit titles and body text, change colors, and adjust card height to the content
- Pin notes as floating windows above other apps; drag the left strip to move them; restore pinned notes and their positions after restarting
- Hide notes without deleting them, confirm deletion with a second click, and drag notes to reorder
- All notes window with search, active/hidden filters, editing, restore, Markdown/text export, and import
- SQLite storage with a configurable folder, including synced folders such as Synology Drive
- Tray icon, global keyboard shortcuts, fullscreen detection, and startup at Windows sign-in
- GitHub release checks and an installer for app updates
- Configurable monitor, visible note count, deck position, collapse delay, font, and text size
- High DPI support, including 4K displays (PerMonitorV2)

## Requirements

- Windows 10 or 11, x64
- Development: .NET 9 SDK
- The published executable includes the .NET runtime; a separate runtime installation is not required

## Build and run

Run these commands from the repository root:

```powershell
# Build
dotnet build MyStickies.sln -c Release

# Run unit and regression tests
dotnet test tests\MyStickies.Tests\MyStickies.Tests.csproj -c Release

# Validate the release script without publishing
pwsh -NoProfile -File .\tests\ReleaseScript.Tests.ps1

# Run from source
dotnet run --project src\MyStickies -c Release
```

Optional startup arguments:

| Argument | Behavior |
|---|---|
| `--all-notes` | Open All notes at startup |
| `--settings` | Open Settings at startup |

## Publish an executable

```powershell
pwsh .\publish.ps1
pwsh .\publish.ps1 -Run
pwsh .\publish.ps1 -Output D:\Apps\MyStickies
```

- Publishes a single executable in Release configuration for win-x64, including the .NET runtime
- Default destination: `%LocalAppData%\Programs\MyStickies\MyStickies.exe`
- Stops a running My Stickies process before publishing
- Enable startup at Windows sign-in from the published executable; registering a build output can break startup when that folder is cleaned

## Build an installer

Install Inno Setup once, then build:

```powershell
winget install JRSoftware.InnoSetup
pwsh .\build-installer.ps1
pwsh .\build-installer.ps1 -Install
```

The installer is written to `dist\MyStickies-Setup-<version>.exe`. The optional `-Install` switch installs it silently after building.

- Installs for the current user without administrator privileges in `%LocalAppData%\Programs\MyStickies`
- Creates a Start menu shortcut, with optional desktop shortcut and startup at Windows sign-in
- Closes a running app before installation
- Keeps notes and settings after uninstalling, including `%LocalAppData%\MyStickies` and your chosen notes folder
- The executable is not code-signed, so Windows SmartScreen may show a warning on another PC

## Publish a GitHub release

Install GitHub CLI and sign in with `gh auth login`. Commit and push your changes, including the version update, before running:

```powershell
.\build-installer.ps1
.\release.ps1 1.0.10 "Add Simplified Chinese and Japanese language support"
```

- The first argument is the version and the second is the release description
- The version must match the project version
- Uploads `dist\MyStickies-Setup-1.0.10.exe` to release `v1.0.10` and marks it as the latest release
- Uses the current commit as the tag target, so that commit must already be pushed to GitHub
- Stops if the installer is missing, the working tree has uncommitted changes, or publishing fails
- Command reference: [GitHub CLI — gh release create](https://cli.github.com/manual/gh_release_create)

## Using the app

### Language

On first launch, select Korean, English, Simplified Chinese, or Japanese before choosing a notes folder. To change the language later, right-click the tray icon, open Settings, select a language, and click OK. Menus and open windows update immediately.

The language is saved on this PC and restored at startup. Existing installations keep Korean until changed in Settings. Switching the language does not translate or replace your notes. New default titles and welcome notes use the selected language.

### The deck

| Action | Result |
|---|---|
| Hover over the bookmark | Reveal the note tabs |
| Drag the handle above the bookmark | Move and save the bookmark position; available when there is at least one note in the deck |
| Click the button below the bookmark | Hide the bookmark and deck; use the tray icon or global shortcut to show them again |
| Hover over a note tab | Expand the note to show its title and body |
| Click a note | Edit its title or body |
| Drag a note vertically | Change its order |
| Move the pointer away | Fold the deck after the configured delay |
| Right-click the bookmark or a note | Open the menu for All notes, Settings, Hide/Show deck, and Exit |

Hide deck temporarily hides the bookmark and deck. Clicking the tray icon or using a global shortcut brings them back.

### Note buttons

| Button | Action |
|---|---|
| `+` below the deck | Add a note |
| Pin at the top right | Detach the note as a floating window and fold the rest of the deck; click again to return it to the deck |
| `−` at the top right | Hide the note; restore it later from the Hidden filter in All notes |
| `×` at the top right | Arm deletion; click again within three seconds to confirm |
| Color dots while editing | Change the note color |

Pinned notes stay above other apps even when you click elsewhere. Drag the left strip to move a pinned note. Drag the bottom-right corner to resize both width and height, or the bottom edge to change only the height. After resizing, the note keeps its size while editing; scroll to read any overflowing text. The app remembers pinned notes, their positions, and manually adjusted sizes for each notes folder on this PC and restores them after restarting.

Floating notes support editing, hiding, and deletion. Hiding or deleting a note also removes its pin.

### Editing shortcuts

| Shortcut | Action |
|---|---|
| Enter in the title field | Move to the body |
| Ctrl+Enter or Ctrl+S | Save and finish editing |
| Esc | Discard changes |
| Click outside or select another note | Save and finish editing |

An empty title becomes `New note (yyyy-MM-dd HH:mm)` when English is selected.

### Global shortcuts

| Shortcut | Action |
|---|---|
| Ctrl+Alt+S | Show or fold the deck |
| Ctrl+Alt+N | Create a note and start editing |

Turn global shortcuts off in Settings if another app uses the same keys.

### All notes

Open All notes from the tray menu or the bookmark’s right-click menu.

- Filter by All, Active, or Hidden, and search titles and body text
- Click a title or body in the detail card to edit it; Esc cancels, Ctrl+Enter saves, and selecting another note saves automatically
- Change a note’s color using the dots at the bottom of the detail card
- Hide, restore, delete with confirmation, or export the selected note
- Import multiple `.md` or `.txt` files, export all notes into one Markdown file, or add a note

### Updates

The app checks the latest GitHub release 15 seconds after startup and once a day. When a new version is available, it shows a tray notification and changes the update menu item to Install update vX.

- View the release description and choose Install now, Later, or Skip this version
- Install now downloads the installer, closes the app, installs the update, and restarts the app
- Skipped versions are excluded from automatic notifications; you can still check manually from the tray menu
- Turn off automatic checks in Settings to check only when requested

Release assets must be named `MyStickies-Setup-*.exe` to be recognized. Draft releases and prereleases are ignored.

## Storage and syncing

- Notes are stored in a SQLite file named `my_stickies.db`
- First launch asks for a language and a notes folder; the default folder is `%LocalAppData%\MyStickies`
- Change the folder in Settings at any time; an existing database is loaded as is, or you can copy your notes or start a new file with welcome notes
- PC-specific settings, including language and pinned window positions, are stored in `%LocalAppData%\MyStickies\settings.json`
- Choose a synced folder such as Synology Drive or OneDrive to share notes between PCs; external file changes are reloaded automatically
- Intended for a single user; editing on two PCs at the same time can cause the last synced version to overwrite the other

The database schema uses `PRAGMA user_version`. Older databases are migrated at startup. The legacy filename `notes.db` is also renamed to `my_stickies.db` automatically.

## Settings

Right-click the tray icon and select Settings.

| Setting | Description |
|---|---|
| Language | Korean, English, Simplified Chinese, or Japanese; applies without restarting |
| Notes folder | Location of the database file |
| Dock monitor | Monitor for the deck, with per-monitor DPI support |
| Visible notes | Number of recent notes shown in the deck, from 3 to 8 |
| Vertical position | Vertical center of the deck as a percentage of the screen |
| Collapse delay | Delay before folding when the pointer leaves |
| Hide behind fullscreen apps | Hide the deck when a fullscreen app is in front on the same monitor |
| Global shortcuts | Enable Ctrl+Alt+S and Ctrl+Alt+N |
| Font and body text size | Choose a system font and size from 10 to 24 pt |
| Automatic update checks | Check for new releases at startup and daily |
| Startup at Windows sign-in | Register the app in the current user’s Run registry key |

## Project structure

```text
my_stickies/
  MyStickies.sln
  publish.ps1                 Executable publishing script
  build-installer.ps1         Installer build script
  release.ps1                 GitHub release script
  installer/MyStickies.iss    Inno Setup definition
  src/MyStickies/
    App.xaml(.cs)             Application resources and single-instance handling
    MainWindow.xaml(.cs)      Dock, deck transitions, reordering, and settings
    MainWindow.FloatingNotes.cs  Pinned note lifecycle and persistence
    Controls/NoteTab           Note card, editing, animations, colors, and buttons
    Windows/                  All notes, settings, language selection, floating notes, updates
    Data/                     SQLite repository, note store, settings, export, startup registration
    Layout/                   Deck geometry, monitors, fonts, floating window placement
    Localization/             Korean, English, Simplified Chinese, and Japanese strings and live language switching
    Interop/                  Win32 window styles, DPI, fullscreen detection, global shortcuts
    Tray/                     Tray icon and menu
    Models/                   Notes, palette, welcome notes, relative timestamps
    Assets/app.ico            Application icon
  tests/MyStickies.Tests/      xUnit unit and regression tests
  tests/ReleaseScript.Tests.ps1  Release script tests without publishing
```

## Technology

- C#, WPF, .NET 9, and Microsoft.Data.Sqlite
- Windows Forms is used only for the tray icon
- Fully transparent window regions let clicks pass through; while the deck is open, a nearly invisible hover area keeps it open between cards and around the add button

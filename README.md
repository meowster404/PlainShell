# PlainShell 🚀

> **Intelligent Terminal Shell with VS Code-Style IntelliSense & Categorized Command Catalog for Windows.**

[![Build and Release](https://github.com/your-username/PlainShell/actions/workflows/release.yml/badge.svg)](https://github.com/your-username/PlainShell/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows_x64-0078D6.svg)](https://microsoft.com)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)

---

## 🌟 What is PlainShell?

**PlainShell** is a modern terminal shell wrapper designed for Windows developers. It solves the frustration of remembering cryptic CLI syntax and parameters by bringing **IDE-grade IntelliSense auto-completion** and a **categorized command catalog** directly into your console.

When you type a command like `cd`, `git`, or `docker`, PlainShell renders a floating suggestion dropdown overlay right below your cursor—just like VS Code!

```text
D:\Projects\MyApp (main) ❯ git 
┌─ IntelliSense Suggestions ────────────── [Tab] Insert  [↑/↓] Select ─┐
│ ▶ [sub]  status                    Show working tree and modified files  │
│   [sub]  commit -m                 Commit staged changes with message    │
│   [sub]  push                      Push commits to remote repository     │
│   [sub]  pull                      Fetch and merge remote changes        │
│   [sub]  log --oneline -n 10       View last 10 commits compactly        │
└─ [F1 / Ctrl+P] All Commands Catalog ─────────────────────────────────┘
```

---

## ✨ Features

- **⚡ VS Code-Style IntelliSense Overlay**:
  - Floating card displayed directly below your command prompt.
  - Color-coded badges for suggestion types:
    - `[cmd]` : Primary commands (`cd`, `git`, `docker`, `dotnet`, `npm`, etc.)
    - `[sub]` : Subcommands (`status`, `commit -m`, `ps`, `compose up -d`)
    - `[dir]` : Directories and folders (with auto-trailing `/`)
    - `[file]`: Files (showing human-readable file sizes)
    - `[flag]`: Options and flags
- **📁 Smart File System Auto-Complete**:
  - Typing `cd ` queries the current directory and only suggests folders!
  - Typing `cd Ass` automatically matches `Assets/`.
  - Supports parent traversal (`cd ..`), relative paths, and drive switches (`D:`, `C:`).
- **🧭 Categorized Command Palette (`F1` or `Ctrl+P`)**:
  - Full-screen interactive TUI menu organized by category:
    - 📁 *Navigation & Filesystem* (`cd`, `dir`, `tree`, `mkdir`, `explorer .`)
    - 🌿 *Git Version Control* (`git status`, `git add .`, `git commit -m`, `git log`)
    - 🐳 *Docker & Containers* (`docker ps`, `docker compose up -d`, `docker logs -f`)
    - 🌐 *Networking* (`curl -I`, `ping`, `ipconfig /all`, `netstat -ano`)
    - ⚙️ *Process & System* (`tasklist`, `taskkill /F /PID`, `systeminfo`)
    - 📦 *Package Managers & Build* (`npm install`, `dotnet run`, `pip install`)
    - 🛠️ *Developer Utilities* (`code .`, `type`, `where`)
    - 💡 *Custom User Commands*
  - Search across commands by name, description, category, or tags.
  - Run immediately with `[Enter]` or insert into the prompt with `[Tab]` to edit arguments!
- **💾 Custom Command Persistence**:
  - Easily add your own custom project commands and snippets through the interactive palette or by editing `%USERPROFILE%/.plainshell/custom_commands.json`.
- **🌿 Git Branch Detection**:
  - Displays your active git branch in the prompt automatically.
- **🚀 Standalone Single-File Windows Executable**:
  - Compiles into a single `PlainShell.exe` with zero external dependencies.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Typing` | Triggers live IntelliSense suggestions and path completions |
| `Tab` | Autocomplete / insert currently selected suggestion |
| `Up` / `Down` | Navigate suggestion list (or browse command history when popup is closed) |
| `Esc` | Dismiss the suggestions popup |
| `F1` or `Ctrl + P` | Open the Categorized Command Palette & Search |
| `Ctrl + C` | Cancel current command buffer (`^C`) |
| `Ctrl + L` or `cls` | Clear terminal screen and redraw welcome banner |
| `:catalog` | Command alias to open the Command Palette |
| `:history` | View history of executed commands in this session |
| `:help` | Display shortcuts and help information |
| `exit` or `quit` | Exit PlainShell |

---

## 📦 Download & Run

### Option 1: Download from GitHub Releases
1. Go to the [Releases](https://github.com/your-username/PlainShell/releases) page.
2. Download `PlainShell-windows-x64.zip` or `PlainShell.exe`.
3. Run `PlainShell.exe` from any terminal or double-click to start!

### Option 2: Build from Source
Requirements: .NET 10 SDK or .NET 8/9 SDK.

```powershell
# Clone the repository
git clone https://github.com/your-username/PlainShell.git
cd PlainShell

# Build and run directly
dotnet run

# Or build the single-file self-contained .exe
.\build.ps1
```
The published binary will be generated at `./dist/PlainShell.exe`.

---

## 🛠️ Diagnostics & CLI Flags

PlainShell includes a built-in diagnostic and test suite:

```powershell
# Run automated self-tests
PlainShell.exe --test

# View version info
PlainShell.exe --version

# View CLI options
PlainShell.exe --help
```

---

## 🚀 Publishing Releases to GitHub

This repository comes pre-configured with a GitHub Actions workflow (`.github/workflows/release.yml`) that automatically builds, packages, and publishes the `.exe` whenever you create a release or push a git tag.

### Step 1: Initialize Git and Push to GitHub
```powershell
git init
git add .
git commit -m "feat: initial commit of PlainShell"
git branch -M main
git remote add origin https://github.com/<your-username>/PlainShell.git
git push -u origin main
```

### Step 2: Create a Release Tag
```powershell
# Create a release version tag (e.g. v1.0.0)
git tag -a v1.0.0 -m "Release v1.0.0: Intelligent Windows Terminal Shell"
git push origin v1.0.0
```

GitHub Actions will automatically:
1. Compile PlainShell as a single-file, self-contained Windows x64 binary.
2. Bundle `PlainShell.exe`, `commands.json`, and `README.md` into `PlainShell-windows-x64.zip`.
3. Create a GitHub Release and attach the `.zip` and standalone `.exe` as downloadable assets.

---

## 🎨 Customizing Commands

PlainShell reads commands from `Assets/commands.json`. You can easily add new commands in JSON format:

```json
{
  "Name": "docker compose logs -f",
  "Category": "Docker & Containers",
  "Description": "Stream real-time logs for all docker compose services",
  "Syntax": "docker compose logs -f [service]",
  "Example": "docker compose logs -f web",
  "Tags": ["docker", "logs", "compose", "stream"]
}
```

Custom commands added inside the application (`F1` -> `+ Add Custom Command`) are automatically stored in `%USERPROFILE%/.plainshell/custom_commands.json` so they persist across updates.

---

## 📄 License
MIT License. Free for personal and commercial use.

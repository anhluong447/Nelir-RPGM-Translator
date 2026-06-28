# Nelir's RPGM Translator

A desktop tool built in **WPF (.NET 10.0)** to translate RPG Maker MZ/MV game data files (`Map*.json` and `CommonEvents.json`). It features a premium light-themed user interface, flat/structured machine-translation (MTL) imports, background autosaving, global search & replace, and selective translation exports.

---

## 🚀 Key Features

* **3-Folder Translation Workflow**:
  - **RAW Folder (Read)**: Ingests original game JSON files, automatically extracting translatable rows (dialogue text, choices, name tags) while ignoring meaningless scripts or system commands.
  - **MTL Folder (Read)**: Imports reference machine translations, supporting both structured game files and simple key-value flat JSON structures.
  - **Selective Export (Write)**: Exports translated strings to flat JSON files (mapping `UniqueKey` to `TranslationText`) for selected files.
* **Modern WPF UI & Dark Mode**:
  - Minimalist **Light Mode** theme featuring soft warm-sand accents, clear typography, and responsive hover transitions.
  - Fully integrated **Dark Mode** (Catppuccin-like blue-gray theme) dynamically toggleable from the top toolbar button, instantly swapping colors/brushes and saving preference to local settings.
  - Dynamic overlay: Semi-transparent loading screen overlay (`BgOverlay`) dynamically matches active theme colors.
  - Grid separation using **Grouped File Headers** with line statistics.
  - Flexible layout columns (STT, RAW text, MTL, and Edited translation) supporting drag-and-drop column resizing.
  - Premium **Loading Overlay**: Displays when loading folders or importing MTL translations, reporting active file details (name and size), elapsed time, row throughput speed, and progress percentages to debug bottlenecks.
* **Translation Status Badge (`TT` Column)**:
  - High-visibility status indicator next to translations: Empty (`○` in Gray), MTL-copied (`~` in Yellow/Warning), or manually Translated (`✓` in Green/Success).
* **Background Autosave & Restore**:
  - Automatically saves progress to `.nelir_autosave.json` in the raw folder every 30 seconds to prevent data loss.
  - Prompts to restore manual translations directly into the **Bản dịch chính thức (TRANSLATED)** column on startup.
* **Manual Project Saving (.nel Format)**:
  - Save progress manually into a lightweight `.nel` file format.
  - The `.nel` file is optimized to store only path references and active translations (omitting raw data to save size).
  - Automatically loads and resolves file structures on startup. If the original data folder is missing or moved, prompts the user to easily relocate/re-link it without losing translations.
* **Keyboard-Driven Shortcuts & Undo/Redo**:
  - `Ctrl + O`: Select and open RAW folder.
  - `Ctrl + Shift + O`: Open `.nel` project workspace.
  - `Ctrl + S`: Save project workspace `.nel` file.
  - `Ctrl + Shift + S`: Save project workspace as another `.nel` file.
  - `Ctrl + E`: Export translation game files.
  - `Ctrl + Z` / `Ctrl + Y`: Undo / Redo manual translation inputs.
  - Automatically suspends undo tracking during bulk folder loads or machine translation imports to optimize performance.

---

## 🛠️ Tech Stack

* **Framework**: .NET 10.0 (Windows WPF)
* **Architecture**: Model-View-ViewModel (MVVM)
* **MVVM Helpers**: `CommunityToolkit.Mvvm` (Source generators for properties/commands)
* **WPF Behaviors**: `Microsoft.Xaml.Behaviors.Wpf`

---

## 📁 Repository Structure

```
Nelir-RPGM-Translator/
├── Nelir/                      # Core WPF Project
│   ├── Models/                 # Data schemas (TranslationRow, FileNode, ProjectState, GlossaryEntry)
│   ├── ViewModels/             # MainViewModel (UI state, commands, filtering)
│   ├── Views/                  # MainWindow, FindReplaceDialog, ExportSelectionWindow, GlossaryWindow, GlossaryTextBlock
│   ├── Services/               # RpgmParser, ExportService, AutoSaveService, AppSettingsService, GlossaryService, UndoRedoService, ThemeService
│   ├── Resources/              # Typography, geometries, control styles, and theme dictionaries
│   │   └── Themes/             # Light.xaml and Dark.xaml theme dictionaries
│   ├── Converters/             # WPF Bindings Value Converters (TranslationStatusConverter, TranslationStatusBrushConverter, BoolToThemeIconConverter, etc.)
│   └── Nelir.csproj            # WPF Project Configuration
├── test_data/                  # Sample RPG Maker JSON files (Map001, CommonEvents, glossary.json)
└── README.md                   # Project overview
```

---

## 🏗️ Getting Started

### Prerequisites
* Windows OS
* .NET 10.0 SDK

### Build & Run
Clone the repository and run the following commands in the `Nelir` project folder:

```powershell
# Navigate to the project directory
cd Nelir

# Restore dependencies and build
dotnet build

# Launch the application
dotnet run
```

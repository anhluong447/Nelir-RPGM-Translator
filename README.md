# Nelir's RPGM Translator

A desktop tool built in **WPF (.NET 10.0)** to translate RPG Maker MZ/MV game data files (`Map*.json` and `CommonEvents.json`). It features a premium light-themed user interface, flat/structured machine-translation (MTL) imports, background autosaving, global search & replace, and selective translation exports.

---

## 🚀 Key Features

* **3-Folder Translation Workflow**:
  - **RAW Folder (Read)**: Ingests original game JSON files, automatically extracting translatable rows (dialogue text, choices, name tags) while ignoring meaningless scripts or system commands.
  - **MTL Folder (Read)**: Imports reference machine translations, supporting both structured game files and simple key-value flat JSON structures.
  - **Selective Export (Write)**: Exports translated strings to flat JSON files (mapping `UniqueKey` to `TranslationText`) for selected files.
* **Modern WPF UI**:
  - Minimalist **Light Mode** theme featuring soft warm-sand accents, clear typography, and responsive hover transitions.
  - Grid separation using **Grouped File Headers** with line statistics.
  - Flexible layout columns (STT, RAW text, MTL, and Edited translation) supporting drag-and-drop column resizing.
  - Premium **Loading Overlay**: Displays when loading folders or importing MTL translations, reporting active file details (name and size), elapsed time, row throughput speed, and progress percentages to debug bottlenecks.
* **Sidebar Progress Tracking**:
  - Displays completion progress ratios next to each file and the root folder in the tree view (e.g. `Map001.json (0/4)`).
* **Find & Replace (Ctrl + F / Ctrl + H)**:
  - Tabbed search dialog enabling real-time navigation, case-sensitive matching, and single/bulk replacements.
  - Search box input is debounced by 300ms to eliminate UI lag.
* **Interactive Glossary Support**:
  - Automatically loads project-specific `glossary.json` files.
  - Dynamically highlights terms in the RAW column in yellow with detailed hover tooltips showing official translations.
  - Features a dedicated Glossary editor window to easily add, delete, and save definitions.
* **Background Autosave & Restore**:
  - Automatically saves progress to `.nelir_autosave.json` in the raw folder every 30 seconds to prevent data loss.
  - Prompts to restore manual translations directly into the **Bản dịch chính thức (TRANSLATED)** column on startup.

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
│   ├── Services/               # RpgmParser, ExportService, AutoSaveService, AppSettingsService, GlossaryService
│   ├── Resources/              # Color tokens, styles, geometries, and control templates
│   ├── Converters/             # WPF Bindings Value Converters
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

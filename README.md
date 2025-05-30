# DSX Game Helper Extended (DSX GHE)

A program that launches and kills [DSX](https://store.steampowered.com/app/1812620/DSX/) when running specified games so you don't have to open it manually every time. The original DSX Game Helper was created by [@raritytiks](https://github.com/raritytiks). I forked from their repository and expanded upon it with some additional features, so please support them!

## New Features
* The ability to autodetect games from a specified folder
* A dedicated settings page for the following features:

  - Pointing the program to DSX
  - Options for starting the program on bootup and starting in tray
  - Optional notifications for when DSX launches and closes, if an error occurs, or if a new update is available
    
* Dynamic tray icon improvements
* Drag and drop exes to add them
* Option to open games directly from DSX GHE (default off in the settings page)
* Edit Game Name
* EXE icons next to Game Name, with the ability to change them
* Select programs before deleting, with select all option available
* Scroll, search, and sort through lists
* Updated to .NET 8

## Getting Started
### Prerequisites

- Windows 10/11
- Any version of DSX
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-8.0.408-windows-x64-installer)

### Installation

1. Download the latest release from the [Releases](https://github.com/raritytiks/dsx-game-helper/releases) page.
2. Extract the ZIP file to a directory of your choice.

---

**NOTES:** 
* Do **NOT** run DSX GHE as an administrator, otherwise the drag and drop feature will not work.
* To add Microsoft Store games to DSX GHE, you must either drag and drop the game's EXE or scan its directory folder, as traditional adding does not work due to administrator protections on Microsoft Store game EXEs.

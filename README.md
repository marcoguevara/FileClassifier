# FileClassifier

FileClassifier is a Windows desktop application that organizes files into `YEAR\MM - Month` folders.

## Build and run

Install the .NET 10 SDK, then run:

```powershell
dotnet build .\FileClassifier\FileClassifier.csproj
dotnet run --project .\FileClassifier\FileClassifier.csproj
```

Choose a folder with **Browse...** or drag a folder onto the app. Select **Preview** to view the planned `YEAR\MM - Month\filename` tree before moving anything. Select **Organize Files** to proceed. Files in the selected folder and its subfolders are organized recursively. The app shows progress and allows the operation to be cancelled.

Photos and videos use a date from names matching `IMG_YYYYMMDD_XXXXXX` or `VID_YYYYMMDD_XXXXXX` first, then metadata dates, and finally the last-modified date. Other files use the last-modified date. New folders use English month names. Existing folders using English or Spanish month names are skipped.

After organizing, the app removes empty subfolders under the selected folder. Folders that still contain files that could not be moved are kept.

## Export a standalone executable

Publish a self-contained, single-file Windows x64 executable:

```powershell
dotnet publish .\FileClassifier\FileClassifier.csproj -p:PublishProfile=win-x64
```

The distributable is `artifacts\publish\win-x64\FileClassifier.exe`. It includes the .NET runtime and application dependencies; the target computer does not need the .NET runtime installed.

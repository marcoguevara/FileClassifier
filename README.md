# FileClassifier

FileClassifier is a Windows desktop application that organizes files into `YEAR\MM - Month` folders.

## Build and run

Install the .NET 10 SDK, then run:

```powershell
dotnet build .\FileClassifier\FileClassifier.csproj
dotnet run --project .\FileClassifier\FileClassifier.csproj
```

Choose a folder with **Examinar...** or drag a folder onto the app. Files in that folder and its subfolders are organized recursively. The app shows progress and allows the operation to be cancelled.

Photos and videos use a date from names matching `IMG_YYYYMMDD_XXXXXX` or `VID_YYYYMMDD_XXXXXX` first, then metadata dates, and finally the last-modified date. Other files use the last-modified date. Existing `YEAR\MM - Month` folders are skipped.

## Export a standalone executable

Publish a self-contained, single-file Windows x64 executable:

```powershell
dotnet publish .\FileClassifier\FileClassifier.csproj -p:PublishProfile=win-x64
```

The distributable is `artifacts\publish\win-x64\FileClassifier.exe`. It includes the .NET runtime and application dependencies; the target computer does not need the .NET runtime installed.

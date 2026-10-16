using System.Globalization;

if (args.Length > 1 || (args.Length == 1 && args[0] is "--help" or "-h"))
{
    Console.WriteLine("Usage: FileClassifier [folder-path]");
    Console.WriteLine("Organizes files into YEAR\\MONTH folders based on last-modified date.");
    Console.WriteLine("After each folder, prompts for another. Press Ctrl+C to exit.");
    return args.Length == 1 ? 0 : 1;
}

string? nextPath = args.Length == 1 ? args[0] : null;
while (true)
{
    var sourcePath = nextPath ?? PromptForFolder();
    nextPath = null;

    if (sourcePath is null)
    {
        return 0;
    }

    if (string.IsNullOrWhiteSpace(sourcePath))
    {
        Console.Error.WriteLine("Enter a folder path, or press Ctrl+C to exit.");
        continue;
    }

    OrganizeFolder(sourcePath);
}

static void OrganizeFolder(string sourcePath)
{
    string fullSourcePath;
    try
    {
        fullSourcePath = Path.GetFullPath(sourcePath);
    }
    catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
    {
        Console.Error.WriteLine($"Invalid folder path: {exception.Message}");
        return;
    }

    if (!Directory.Exists(fullSourcePath))
    {
        Console.Error.WriteLine($"Folder not found: {fullSourcePath}");
        return;
    }

    var movedCount = 0;
    var failedCount = 0;
    var spanishCulture = CultureInfo.GetCultureInfo("es-ES");

    try
    {
        foreach (var sourceFile in Directory.EnumerateFiles(fullSourcePath, "*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var lastModified = File.GetLastWriteTime(sourceFile);
                var yearFolder = lastModified.ToString("yyyy", CultureInfo.InvariantCulture);
                var monthName = spanishCulture.TextInfo.ToTitleCase(lastModified.ToString("MMMM", spanishCulture));
                var monthFolder = $"{lastModified:MM} - {monthName}";
                var destinationDirectory = Path.Combine(fullSourcePath, yearFolder, monthFolder);
                Directory.CreateDirectory(destinationDirectory);

                var destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));
                if (File.Exists(destinationFile))
                {
                    destinationFile = GetAvailableDestinationPath(destinationDirectory, Path.GetFileName(sourceFile));
                }

                File.Move(sourceFile, destinationFile);
                Console.WriteLine($"Moved: {sourceFile} -> {destinationFile}");
                movedCount++;
            }
            catch (IOException exception)
            {
                Console.Error.WriteLine($"Could not move '{sourceFile}': {exception.Message}");
                failedCount++;
            }
            catch (UnauthorizedAccessException exception)
            {
                Console.Error.WriteLine($"Access denied for '{sourceFile}': {exception.Message}");
                failedCount++;
            }
        }
    }
    catch (IOException exception)
    {
        Console.Error.WriteLine($"Could not read folder '{fullSourcePath}': {exception.Message}");
        failedCount++;
    }
    catch (UnauthorizedAccessException exception)
    {
        Console.Error.WriteLine($"Access denied reading folder '{fullSourcePath}': {exception.Message}");
        failedCount++;
    }

    Console.WriteLine($"Finished. Moved: {movedCount}; failed: {failedCount}.");
}

static string? PromptForFolder()
{
    Console.Write("Folder path to organize (Ctrl+C to exit): ");
    return Console.ReadLine();
}

static string GetAvailableDestinationPath(string destinationDirectory, string fileName)
{
    var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
    var extension = Path.GetExtension(fileName);
    var suffix = 1;
    string candidatePath;

    do
    {
        candidatePath = Path.Combine(
            destinationDirectory,
            $"{nameWithoutExtension} ({suffix}){extension}");
        suffix++;
    }
    while (File.Exists(candidatePath));

    return candidatePath;
}

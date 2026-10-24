using System.Globalization;
using ImageMetadataReader = MetadataExtractor.ImageMetadataReader;
using ImageProcessingException = MetadataExtractor.ImageProcessingException;

if (args.Length > 1 || (args.Length == 1 && args[0] is "--help" or "-h"))
{
    Console.WriteLine("Usage: FileClassifier [folder-path]");
    Console.WriteLine("Organizes files from the folder and all subfolders into YEAR\\MONTH folders.");
    Console.WriteLine("Photos and videos use their capture date when available; other files use last-modified date.");
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
        var sourceFiles = Directory
            .EnumerateFiles(fullSourcePath, "*", SearchOption.AllDirectories)
            .Where(sourceFile => !IsInOrganizedFolder(fullSourcePath, sourceFile, spanishCulture))
            .ToArray();

        foreach (var sourceFile in sourceFiles)
        {
            try
            {
                var fileDate = GetDateForOrganization(sourceFile);
                var yearFolder = fileDate.ToString("yyyy", CultureInfo.InvariantCulture);
                var monthName = spanishCulture.TextInfo.ToTitleCase(fileDate.ToString("MMMM", spanishCulture));
                var monthFolder = $"{fileDate:MM} - {monthName}";
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

static DateTime GetDateForOrganization(string filePath)
{
    var extension = Path.GetExtension(filePath).ToLowerInvariant();
    var isPhoto = extension is
        ".jpg" or ".jpeg" or ".jpe" or ".tif" or ".tiff" or ".png" or
        ".heic" or ".heif" or ".webp" or ".avif" or ".bmp" or ".gif" or ".ico" or
        ".dng" or ".3fr" or ".arw" or ".cr2" or ".cr3" or ".crw" or ".gpr" or
        ".kdc" or ".nef" or ".orf" or ".pef" or ".raf" or ".rw2" or ".rwl" or
        ".srw" or ".pcx" or ".psd" or ".tga";
    var isVideo = extension is ".mp4" or ".mov" or ".avi" or ".m4v" or ".3gp" or ".3g2";

    if (!isPhoto && !isVideo)
    {
        return File.GetLastWriteTime(filePath);
    }

    try
    {
        var tags = ImageMetadataReader.ReadMetadata(filePath)
            .SelectMany(directory => directory.Tags)
            .Select(tag => new
            {
                Tag = tag,
                Priority = GetDateTagPriority(tag.Name, isPhoto)
            })
            .Where(item => item.Priority >= 0)
            .OrderBy(item => item.Priority);

        foreach (var item in tags)
        {
            if (TryParseMetadataDate(item.Tag.Description, out var captureDate))
            {
                return captureDate;
            }
        }
    }
    catch (ImageProcessingException exception)
    {
        Console.Error.WriteLine(
            $"Could not read capture date from '{filePath}'; using last-modified date. {exception.Message}");
    }

    return File.GetLastWriteTime(filePath);
}

static int GetDateTagPriority(string tagName, bool isPhoto)
{
    var normalizedName = tagName.Replace(" ", string.Empty, StringComparison.Ordinal)
        .Replace("/", string.Empty, StringComparison.Ordinal)
        .Replace(":", string.Empty, StringComparison.Ordinal);

    if (normalizedName.Equals("mediacreated", StringComparison.OrdinalIgnoreCase) ||
        normalizedName.Equals("mediacreatedate", StringComparison.OrdinalIgnoreCase) ||
        normalizedName.Equals("mediacreateddate", StringComparison.OrdinalIgnoreCase) ||
        normalizedName.Equals("mediacreationdate", StringComparison.OrdinalIgnoreCase))
    {
        return 0;
    }

    if (isPhoto)
    {
        return normalizedName.ToLowerInvariant() switch
        {
            "date" => 1,
            "datetimeoriginal" => 2,
            "datecreated" => 3,
            "datetimedigitized" => 4,
            "createdate" => 5,
            "datetime" => 6,
            _ => -1
        };
    }

    return normalizedName.ToLowerInvariant() switch
    {
        "date" => 1,
        "datetimeoriginal" => 2,
        "created" => 3,
        "creationtime" => 4,
        "creationdate" => 5,
        "datecreated" => 6,
        "datetime" => 7,
        _ => -1
    };
}

static bool TryParseMetadataDate(string? value, out DateTime date)
{
    var exifDateFormats = new[]
    {
        "yyyy:MM:dd HH:mm:ss",
        "yyyy:MM:dd HH:mm:ss.FFF",
        "yyyy:MM:dd HH:mm:sszzz",
        "yyyy:MM:dd HH:mm:ss.FFFzzz"
    };

    if (!string.IsNullOrWhiteSpace(value))
    {
        if (DateTime.TryParseExact(
                value,
                exifDateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out date) ||
            DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out date))
        {
            return true;
        }
    }

    date = default;
    return false;
}

static string? PromptForFolder()
{
    Console.Write("Folder path to organize (Ctrl+C to exit): ");
    return Console.ReadLine();
}

static bool IsInOrganizedFolder(string rootPath, string filePath, CultureInfo spanishCulture)
{
    var relativeDirectory = Path.GetRelativePath(rootPath, Path.GetDirectoryName(filePath)!);
    var directoryParts = relativeDirectory.Split(
        [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
        StringSplitOptions.RemoveEmptyEntries);

    if (directoryParts.Length < 2 ||
        !int.TryParse(directoryParts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
        directoryParts[0].Length != 4 ||
        year == 0 ||
        directoryParts[1].Length < 2 ||
        !int.TryParse(directoryParts[1].AsSpan(0, Math.Min(2, directoryParts[1].Length)), NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
        month is < 1 or > 12)
    {
        return false;
    }

    var monthName = spanishCulture.TextInfo.ToTitleCase(
        new DateTime(year, month, 1).ToString("MMMM", spanishCulture));
    return directoryParts[1] == $"{month:00} - {monthName}";
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

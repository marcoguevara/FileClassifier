using System.Globalization;
using ImageMetadataReader = MetadataExtractor.ImageMetadataReader;
using ImageProcessingException = MetadataExtractor.ImageProcessingException;

namespace FileClassifier;

internal sealed record OrganizationProgress(string Stage, int Completed, int Total, string CurrentFile);

internal sealed record PlannedFile(string SourcePath, string DestinationPath);

internal sealed record OrganizationPlan(
    string RootPath,
    IReadOnlyList<PlannedFile> Files,
    int Failed,
    int Total,
    bool Cancelled);

internal sealed record OrganizationResult(
    int Moved,
    int Failed,
    int Completed,
    int Total,
    int RemovedDirectories,
    bool Cancelled);

internal static class FileOrganizer
{
    public static Task<OrganizationPlan> BuildPlanAsync(
        string sourcePath,
        IProgress<OrganizationProgress> progress,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () => BuildPlan(sourcePath, progress, log, cancellationToken),
            cancellationToken);
    }

    public static Task<OrganizationResult> ExecutePlanAsync(
        OrganizationPlan plan,
        IProgress<OrganizationProgress> progress,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () => ExecutePlan(plan, progress, log, cancellationToken),
            cancellationToken);
    }

    private static OrganizationPlan BuildPlan(
        string sourcePath,
        IProgress<OrganizationProgress> progress,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!Directory.Exists(fullSourcePath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {fullSourcePath}");
        }

        progress.Report(new OrganizationProgress("Searching for files...", 0, 0, string.Empty));
        var sourceFiles = Directory
            .EnumerateFiles(fullSourcePath, "*", SearchOption.AllDirectories)
            .Where(sourceFile => !IsInOrganizedFolder(fullSourcePath, sourceFile))
            .ToArray();

        var englishCulture = CultureInfo.GetCultureInfo("en-US");
        var plannedFiles = new List<PlannedFile>(sourceFiles.Length);
        var reservedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failedCount = 0;
        var processedCount = 0;

        foreach (var sourceFile in sourceFiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var fileDate = GetDateForOrganization(sourceFile, log);
                var yearFolder = fileDate.ToString("yyyy", CultureInfo.InvariantCulture);
                var monthName = englishCulture.TextInfo.ToTitleCase(fileDate.ToString("MMMM", englishCulture));
                var monthFolder = $"{fileDate:MM} - {monthName}";
                var destinationDirectory = Path.Combine(fullSourcePath, yearFolder, monthFolder);
                var fileName = Path.GetFileName(sourceFile);
                var destinationFile = Path.Combine(destinationDirectory, fileName);
                if (File.Exists(destinationFile) || reservedDestinations.Contains(destinationFile))
                {
                    destinationFile = GetAvailableDestinationPath(
                        destinationDirectory,
                        fileName,
                        reservedDestinations);
                }

                reservedDestinations.Add(destinationFile);
                plannedFiles.Add(new PlannedFile(sourceFile, destinationFile));
            }
            catch (IOException exception)
            {
                log($"Could not prepare '{sourceFile}': {exception.Message}");
                failedCount++;
            }
            catch (UnauthorizedAccessException exception)
            {
                log($"Access denied for '{sourceFile}': {exception.Message}");
                failedCount++;
            }

            processedCount++;
            progress.Report(new OrganizationProgress(
                "Building preview...",
                processedCount,
                sourceFiles.Length,
                sourceFile));
        }

        return new OrganizationPlan(
            fullSourcePath,
            plannedFiles,
            failedCount,
            sourceFiles.Length,
            cancellationToken.IsCancellationRequested);
    }

    private static OrganizationResult ExecutePlan(
        OrganizationPlan plan,
        IProgress<OrganizationProgress> progress,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var movedCount = 0;
        var failedCount = plan.Failed;
        var processedCount = 0;

        foreach (var file in plan.Files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file.DestinationPath)!);
                File.Move(file.SourcePath, file.DestinationPath);
                movedCount++;
            }
            catch (IOException exception)
            {
                log($"Could not move '{file.SourcePath}': {exception.Message}");
                failedCount++;
            }
            catch (UnauthorizedAccessException exception)
            {
                log($"Access denied for '{file.SourcePath}': {exception.Message}");
                failedCount++;
            }

            processedCount++;
            progress.Report(new OrganizationProgress(
                "Organizing files...",
                processedCount,
                plan.Files.Count,
                file.SourcePath));
        }

        var removedDirectories = RemoveEmptyDirectories(plan.RootPath, log);
        return new OrganizationResult(
            movedCount,
            failedCount,
            processedCount,
            plan.Files.Count,
            removedDirectories,
            plan.Cancelled || cancellationToken.IsCancellationRequested);
    }

    private static int RemoveEmptyDirectories(string rootPath, Action<string> log)
    {
        var removedCount = 0;
        string[] directories;
        try
        {
            directories = Directory
                .EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories)
                .OrderByDescending(directory => directory.Length)
                .ToArray();
        }
        catch (IOException exception)
        {
            log($"Could not enumerate directories for empty-folder cleanup: {exception.Message}");
            return removedCount;
        }
        catch (UnauthorizedAccessException exception)
        {
            log($"Access denied while finding empty folders to clean up: {exception.Message}");
            return removedCount;
        }

        foreach (var directory in directories)
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                    removedCount++;
                }
            }
            catch (IOException exception)
            {
                log($"Could not remove empty folder '{directory}': {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                log($"Access denied removing empty folder '{directory}': {exception.Message}");
            }
        }

        return removedCount;
    }

    private static DateTime GetDateForOrganization(string filePath, Action<string> reportError)
    {
        if (TryGetDateFromFileName(filePath, out var fileNameDate))
        {
            return fileNameDate;
        }

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
            reportError(
                $"Could not read capture date from '{filePath}'; using last-modified date. {exception.Message}");
        }

        return File.GetLastWriteTime(filePath);
    }

    private static bool TryGetDateFromFileName(string filePath, out DateTime date)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var hasExpectedPrefix = fileName.StartsWith("IMG_", StringComparison.OrdinalIgnoreCase) ||
                                fileName.StartsWith("VID_", StringComparison.OrdinalIgnoreCase);

        if (hasExpectedPrefix &&
            fileName.Length == 19 &&
            fileName[12] == '_' &&
            fileName.AsSpan(13).ToString().All(char.IsLetterOrDigit) &&
            DateTime.TryParseExact(
                fileName.AsSpan(4, 8),
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date))
        {
            return true;
        }

        date = default;
        return false;
    }

    private static int GetDateTagPriority(string tagName, bool isPhoto)
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

    private static bool TryParseMetadataDate(string? value, out DateTime date)
    {
        var exifDateFormats = new[]
        {
            "yyyy:MM:dd HH:mm:ss",
            "yyyy:MM:dd HH:mm:ss.FFF",
            "yyyy:MM:dd HH:mm:sszzz",
            "yyyy:MM:dd HH:mm:ss.FFFzzz"
        };

        if (!string.IsNullOrWhiteSpace(value) &&
            (DateTime.TryParseExact(
                 value,
                 exifDateFormats,
                 CultureInfo.InvariantCulture,
                 DateTimeStyles.AssumeLocal,
                 out date) ||
             DateTime.TryParse(
                 value,
                 CultureInfo.InvariantCulture,
                 DateTimeStyles.AssumeLocal,
                 out date)))
        {
            return true;
        }

        date = default;
        return false;
    }

    private static bool IsInOrganizedFolder(string rootPath, string filePath)
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
            !int.TryParse(
                directoryParts[1].AsSpan(0, Math.Min(2, directoryParts[1].Length)),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var month) ||
            month is < 1 or > 12)
        {
            return false;
        }

        var date = new DateTime(year, month, 1);
        var englishCulture = CultureInfo.GetCultureInfo("en-US");
        var englishMonth = englishCulture.TextInfo.ToTitleCase(date.ToString("MMMM", englishCulture));
        var spanishCulture = CultureInfo.GetCultureInfo("es-ES");
        var spanishMonth = spanishCulture.TextInfo.ToTitleCase(date.ToString("MMMM", spanishCulture));
        return directoryParts[1] == $"{month:00} - {englishMonth}" ||
               directoryParts[1] == $"{month:00} - {spanishMonth}";
    }

    private static string GetAvailableDestinationPath(
        string destinationDirectory,
        string fileName,
        HashSet<string> reservedDestinations)
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
        while (File.Exists(candidatePath) || reservedDestinations.Contains(candidatePath));

        return candidatePath;
    }
}

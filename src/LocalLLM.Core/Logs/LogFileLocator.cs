namespace LocalLLM.Core;

public sealed class LogFileLocator
{
    private static readonly string[] LogExtensions = [".log", ".txt", ".csv"];

    public IReadOnlyList<string> FindFiles(string rootPath, DateOnly? date)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return [];
        }

        var allFiles = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(file => LogExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));

        if (date is null)
        {
            return allFiles
                .OrderByDescending(File.GetLastWriteTime)
                .Take(20)
                .ToArray();
        }

        var tokens = BuildDateTokens(date.Value);

        return allFiles
            .Where(file =>
            {
                var fileName = Path.GetFileName(file);
                if (tokens.Any(token => fileName.Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                var lastWriteDate = DateOnly.FromDateTime(File.GetLastWriteTime(file));
                return lastWriteDate == date.Value;
            })
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] BuildDateTokens(DateOnly date) =>
    [
        date.ToString("yyyyMMdd"),
        date.ToString("yyyy-MM-dd"),
        date.ToString("yyyy_MM_dd"),
        date.ToString("yyyy.MM.dd")
    ];
}

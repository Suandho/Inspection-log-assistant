using System.Text.Json;

namespace LocalLLM.Core;

public static class AssistantOptionsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static AssistantOptions Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return AssistantOptions.CreateDefault();
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Config file was not found.", path);
        }

        var json = File.ReadAllText(path);
        var loaded = JsonSerializer.Deserialize<AssistantOptions>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Config file '{path}' could not be parsed.");

        return MergeWithDefaults(loaded);
    }

    private static AssistantOptions MergeWithDefaults(AssistantOptions loaded)
    {
        var defaults = AssistantOptions.CreateDefault();

        return new AssistantOptions
        {
            LogRootPath = string.IsNullOrWhiteSpace(loaded.LogRootPath) ? defaults.LogRootPath : loaded.LogRootPath,
            IndexRootPath = string.IsNullOrWhiteSpace(loaded.IndexRootPath) ? defaults.IndexRootPath : loaded.IndexRootPath,
            UseIndexFirst = loaded.UseIndexFirst,
            EventKeywords = loaded.EventKeywords.Length == 0 ? defaults.EventKeywords : loaded.EventKeywords,
            FailureKeywords = loaded.FailureKeywords.Length == 0 ? defaults.FailureKeywords : loaded.FailureKeywords,
            InspectionAliases = MergeDictionary(defaults.InspectionAliases, loaded.InspectionAliases),
            RecommendedActions = MergeDictionary(defaults.RecommendedActions, loaded.RecommendedActions),
            InspectionRules = MergeRuleDictionary(defaults.InspectionRules, loaded.InspectionRules),
            ErrorRules = MergeRuleDictionary(defaults.ErrorRules, loaded.ErrorRules),
            EventPatterns = loaded.EventPatterns.Length == 0 ? defaults.EventPatterns : loaded.EventPatterns
        };
    }

    private static Dictionary<string, string[]> MergeDictionary(
        Dictionary<string, string[]> defaults,
        Dictionary<string, string[]> loaded)
    {
        var merged = new Dictionary<string, string[]>(defaults, StringComparer.OrdinalIgnoreCase);

        foreach (var item in loaded)
        {
            if (item.Value.Length > 0)
            {
                merged[item.Key] = item.Value;
            }
        }

        return merged;
    }

    private static Dictionary<string, T> MergeRuleDictionary<T>(
        Dictionary<string, T> defaults,
        Dictionary<string, T> loaded)
    {
        var merged = new Dictionary<string, T>(defaults, StringComparer.OrdinalIgnoreCase);

        foreach (var item in loaded)
        {
            merged[item.Key] = item.Value;
        }

        return merged;
    }
}

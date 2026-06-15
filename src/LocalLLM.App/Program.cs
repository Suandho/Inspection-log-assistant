using LocalLLM.Core;
using LocalLLM.Llm;
using System.Text;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

var arguments = ParseArguments(args);

try
{
    var question = ResolveQuestion(args, arguments);
    if (string.IsNullOrWhiteSpace(question))
    {
        Console.Write("\uC9C8\uBB38\uC744 \uC785\uB825\uD558\uC138\uC694: ");
        var input = ReadNonEmptyLine();

        if (LooksLikeCommandLine(input))
        {
            var interactiveArgs = SplitCommandLine(input);
            arguments = ParseArguments(interactiveArgs);
            ApplyInteractiveFallback(input, arguments);
            question = ResolveQuestion(interactiveArgs, arguments);
        }
        else
        {
            question = input;
        }
    }

    var configPath = arguments.GetValueOrDefault("config") ?? FindDefaultConfigPath();

    if (arguments.ContainsKey("llm-health"))
    {
        var healthText = await CheckLocalLlmHealthAsync(configPath, arguments);
        Console.WriteLine(healthText);
        return;
    }

    var options = AssistantOptionsLoader.Load(configPath);

    if (arguments.TryGetValue("log-root", out var configuredLogRoot))
    {
        options = options.WithLogRootPath(configuredLogRoot);
    }

    var assistant = new AssistantService(options);
    var answer = await BuildAnswerAsync(assistant, question, arguments, configPath);
    Console.WriteLine(answer);
}
catch (Exception exception)
{
    Console.Error.WriteLine($"\uBD84\uC11D \uC2E4\uD328: {exception.Message}");
    Environment.ExitCode = 1;
}

static string ReadNonEmptyLine()
{
    while (true)
    {
        var input = Console.ReadLine();
        if (input is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(input))
        {
            return input;
        }
    }
}

static bool LooksLikeCommandLine(string input)
{
    return input.TrimStart().StartsWith("--", StringComparison.Ordinal)
        || input.Contains("--question", StringComparison.OrdinalIgnoreCase)
        || input.Contains("--config", StringComparison.OrdinalIgnoreCase)
        || input.Contains("--llm", StringComparison.OrdinalIgnoreCase);
}

static void ApplyInteractiveFallback(string input, Dictionary<string, string> arguments)
{
    ApplyValue("config");
    ApplyValue("llm");
    ApplyValue("llm-timeout-seconds");
    ApplyValue("question");

    void ApplyValue(string key)
    {
        if (arguments.ContainsKey(key) && !key.Equals("question", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var value = ExtractOptionValue(input, key);
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments[key] = value;
        }
    }
}

static string? ExtractOptionValue(string input, string key)
{
    var marker = "--" + key;
    var markerIndex = input.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    if (markerIndex < 0)
    {
        return null;
    }

    var valueStart = markerIndex + marker.Length;
    while (valueStart < input.Length && char.IsWhiteSpace(input[valueStart]))
    {
        valueStart++;
    }

    if (valueStart >= input.Length)
    {
        return null;
    }

    if (input[valueStart] == '"')
    {
        var valueEnd = input.IndexOf('"', valueStart + 1);
        return valueEnd > valueStart
            ? input[(valueStart + 1)..valueEnd]
            : input[(valueStart + 1)..];
    }

    var nextOption = input.IndexOf(" --", valueStart, StringComparison.Ordinal);
    return nextOption > valueStart
        ? input[valueStart..nextOption].Trim()
        : input[valueStart..].Trim();
}

static string? FindDefaultConfigPath()
{
    var currentDirectoryConfig = FindConfigAbove(Environment.CurrentDirectory);
    if (currentDirectoryConfig is not null)
    {
        return currentDirectoryConfig;
    }

    return FindConfigAbove(AppContext.BaseDirectory);
}

static string? FindConfigAbove(string startPath)
{
    var directory = new DirectoryInfo(startPath);
    while (directory is not null)
    {
        var configPath = Path.Combine(directory.FullName, "config", "assistant-settings.json");
        if (File.Exists(configPath))
        {
            return configPath;
        }

        if (File.Exists(Path.Combine(directory.FullName, "LocalLLM.sln")))
        {
            return null;
        }

        directory = directory.Parent;
    }

    return null;
}

static string ResolveQuestion(string[] args, IReadOnlyDictionary<string, string> arguments)
{
    return arguments.TryGetValue("question", out var configuredQuestion)
        ? configuredQuestion
        : string.Join(' ', args.Where(argument => !argument.StartsWith("--", StringComparison.Ordinal)));
}

static async Task<string> CheckLocalLlmHealthAsync(
    string? configPath,
    IReadOnlyDictionary<string, string> arguments)
{
    var llmOptions = LocalLlmOptions.Load(configPath).WithOverrides(arguments);
    using var llmClient = new LocalHttpLlmClient(llmOptions);
    var result = await llmClient.CheckHealthAsync().ConfigureAwait(false);

    if (result.IsHealthy)
    {
        return "\uB85C\uCEEC LLM \uC11C\uBC84 \uC5F0\uACB0 \uC131\uACF5"
            + Environment.NewLine
            + $"- endpoint: {result.Endpoint}"
            + Environment.NewLine
            + $"- status: {result.StatusCode}";
    }

    return "\uB85C\uCEEC LLM \uC11C\uBC84 \uC5F0\uACB0 \uC2E4\uD328"
        + Environment.NewLine
        + $"- endpoint: {result.Endpoint}"
        + Environment.NewLine
        + $"- status: {result.StatusCode?.ToString() ?? "n/a"}"
        + Environment.NewLine
        + $"- detail: {result.ResponseText}";
}

static async Task<string> BuildAnswerAsync(
    AssistantService assistant,
    string question,
    IReadOnlyDictionary<string, string> arguments,
    string? configPath)
{
    var response = assistant.Analyze(question);

    if (!arguments.TryGetValue("llm", out var llmMode))
    {
        return response.AnswerText;
    }

    if (llmMode.Equals("compact", StringComparison.OrdinalIgnoreCase)
        || llmMode.Equals("offline", StringComparison.OrdinalIgnoreCase))
    {
        return new CompactAnswerService().GenerateAnswer(response);
    }

    if (llmMode.Equals("dummy", StringComparison.OrdinalIgnoreCase))
    {
        var llmAnswerService = new LlmAnswerService(new DummyLlmClient());
        return await llmAnswerService.GenerateAnswerAsync(response).ConfigureAwait(false);
    }

    if (llmMode.Equals("local", StringComparison.OrdinalIgnoreCase)
        || llmMode.Equals("llama", StringComparison.OrdinalIgnoreCase)
        || llmMode.Equals("http", StringComparison.OrdinalIgnoreCase))
    {
        var llmOptions = LocalLlmOptions.Load(configPath).WithOverrides(arguments);
        using var llmClient = new LocalHttpLlmClient(llmOptions);
        var understoodQuestion = await new LlmQuestionUnderstandingService(llmClient)
            .UnderstandAsync(question)
            .ConfigureAwait(false);
        if (understoodQuestion is not null)
        {
            response = assistant.Analyze(understoodQuestion.ToAnalyzerQuestion());
        }

        var llmAnswerService = new LlmAnswerService(llmClient);
        return await llmAnswerService.GenerateAnswerAsync(response).ConfigureAwait(false);
    }

    throw new InvalidOperationException($"\uC9C0\uC6D0\uD558\uC9C0 \uC54A\uB294 LLM \uBAA8\uB4DC\uC785\uB2C8\uB2E4: {llmMode}");
}

static Dictionary<string, string> ParseArguments(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var index = 0; index < args.Length; index++)
    {
        var argument = args[index];
        if (!argument.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var separatorIndex = argument.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex > 2)
        {
            result[argument[2..separatorIndex]] = argument[(separatorIndex + 1)..].Trim('"');
            continue;
        }

        var key = argument[2..];
        if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            result[key] = args[index + 1].Trim('"');
            index++;
        }
        else
        {
            result[key] = "true";
        }
    }

    return result;
}

static string[] SplitCommandLine(string commandLine)
{
    var result = new List<string>();
    var current = new StringBuilder();
    var inQuotes = false;

    foreach (var character in commandLine)
    {
        if (character == '"')
        {
            inQuotes = !inQuotes;
            continue;
        }

        if (char.IsWhiteSpace(character) && !inQuotes)
        {
            AddCurrent();
            continue;
        }

        current.Append(character);
    }

    AddCurrent();
    return result.ToArray();

    void AddCurrent()
    {
        if (current.Length == 0)
        {
            return;
        }

        result.Add(current.ToString());
        current.Clear();
    }
}

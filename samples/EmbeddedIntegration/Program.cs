using LocalLLM.Core;
using LocalLLM.Llm;

var repositoryRoot = FindRepositoryRoot();
var configPath = Path.Combine(repositoryRoot, "config", "assistant-settings.json");
var options = AssistantOptionsLoader.Load(configPath);

var assistant = new AssistantService(options);
var response = assistant.Analyze("오후 2시쯤 기기 작동이 멈췄어");

var customerAnswer = new CompactAnswerService().GenerateAnswer(response);

Console.WriteLine(customerAnswer);

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "LocalLLM.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root could not be found.");
}

using System.Text.Json;
using Exception = System.Exception;

namespace Coyote.App;

internal sealed class Project
{
    public string FileName { get; init; }

    public Dictionary<string, MotionProject> MotionProjects { get; init; }

    public void Save()
    {

    }

    public static Project Load(string fileName)
    {
        return JsonSerializer.Deserialize<Project>(File.ReadAllText(fileName)) ?? throw new Exception("Failed to open project");
    }

    public static Project Create(string fileName)
    {
        return new Project
        {
            FileName = fileName,
            MotionProjects = new Dictionary<string, MotionProject>()
        };
    }
}
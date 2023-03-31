using System.Text.Json;
using System.Text.Json.Serialization;
using Coyote.App.Movement;
using Exception = System.Exception;

namespace Coyote.App;

internal sealed class Project
{
    [JsonIgnore]
    public string FileName { get; private set; }

    [JsonInclude]
    public Dictionary<string, MotionProject> MotionProjects { get; init; }

    public void Save()
    {
        File.WriteAllText(FileName, JsonSerializer.Serialize(this));
    }

    public static Project Load(string fileName)
    {
        var project = JsonSerializer.Deserialize<Project>(File.ReadAllText(fileName)) ?? throw new Exception("Failed to open project");

        project.FileName = fileName;

        return project;
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
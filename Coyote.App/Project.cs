using System.Text.Json;
using System.Text.Json.Serialization;
using Coyote.App.Movement;
using Coyote.App.Nodes;
using Exception = System.Exception;

namespace Coyote.App;

internal sealed class Project
{
    public static Project CreateEmpty(string name)
    {
        return new Project
        {
            FileName = name,
            MotionProjects = new Dictionary<string, MotionProject>(),
            NodeProjects = new Dictionary<string, NodeProject>()
        };
    }

    [JsonIgnore]
    public string FileName { get; set; }

    [JsonInclude]
    public Dictionary<string, MotionProject> MotionProjects { get; init; }

    [JsonInclude]
    public Dictionary<string, NodeProject> NodeProjects { get; init; }

    [JsonIgnore]
    public bool IsChanged { get; private set; }

    public void SetChanged()
    {
        IsChanged = true;
    }

    public void Save()
    {
        File.WriteAllText(FileName, JsonSerializer.Serialize(this));
        IsChanged = false;
    }

    public static Project Load(string fileName)
    {
        var project = JsonSerializer.Deserialize<Project>(File.ReadAllText(fileName)) ?? throw new Exception("Failed to open project");

        project.FileName = fileName;

        return project;
    }
}
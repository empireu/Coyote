using System.Text.Json;
using System.Text.Json.Serialization;
using Coyote.App.Movement;
using Exception = System.Exception;

namespace Coyote.App;

internal sealed class Project
{
    [JsonIgnore]
    public string FileName { get; set; }

    [JsonInclude]
    public Dictionary<string, MotionProject> MotionProjects { get; init; }

    [JsonInclude]
    public MotionConstraints Constraints { get; set; }

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
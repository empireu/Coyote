using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameFramework;
using GameFramework.Utilities;
using ImGuiNET;
using Veldrid;

namespace Coyote.App;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class ConfigAccessor : Attribute { }

public sealed class KeyBind
{
    private GameApplication? _app;

    [JsonInclude]
    public string Name { get; }

    [JsonIgnore]
    public bool Bound { get; private set; }

    [JsonInclude]
    public Key Key { get; private set; }

    [JsonConstructor]
    public KeyBind(string name, Key key)
    {
        Name = name;
        Key = key;
        Bound = false;
    }

    public bool IsDown
    {
        get
        {
            if (!Bound)
            {
                throw new InvalidOperationException("Cannot get state of key before binding");
            }

            return Assert.NotNull(_app).Input.IsKeyDown(Key);
        }
    }

    public static implicit operator bool(KeyBind bind)
    {
        return bind.IsDown;
    }

    public void BindApp(GameApplication app)
    {
        _app = app;
        Bound = true;
    }

    public void BindKey(Key k)
    {
        if (!Bound)
        {
            throw new InvalidOperationException("Cannot bind key before binding the app");
        }

        Key = k;
    }
}

public class DataField<T>
{
    [JsonInclude]
    public string Name { get; }

    [JsonInclude]
    public T Value { get; private set; }

    private readonly Func<T, T>? _process;

    [JsonConstructor]
    public DataField(string name, T value)
    {
        Name = name;
        Value = value;
    }

    public DataField(string name, T value, Func<T, T> process)
    {
        Name = name;
        Value = value;
        _process = process;
    }

    public void Update(T value)
    {
        Value = _process == null ? value : _process(value);
    }

    public static implicit operator T(DataField<T> f) => f.Value;
}

internal class ConfigManager
{
    private readonly App _app;

    private sealed class JsonStorage
    {
        public JsonStorage(KeyBind[] keyBinds, DataField<float>[] floatFields, DataField<int>[] intFields)
        {
            KeyBinds = keyBinds;
            FloatFields = floatFields;
            IntFields = intFields;
        }

        public KeyBind[] KeyBinds { get; }
        public DataField<float>[] FloatFields { get; }
        public DataField<int>[] IntFields { get; }

    }

    private readonly List<KeyBind> _keyBinds = new();
    private readonly List<DataField<float>> _floatFields = new();
    private readonly List<DataField<int>> _intFields = new();

    public ConfigManager(App app)
    {
        _app = app;
        // Set up in current context by scanning for classes with ConfigAccessor:

        foreach (var classType in Assembly.GetCallingAssembly().ExportedTypes.Where(x => x.IsClass && x.GetCustomAttribute<ConfigAccessor>() != null))
        {
            var fields = classType.GetFields(BindingFlags.Static | BindingFlags.Public);

            foreach (var field in fields.Where(f => f.FieldType == typeof(KeyBind)))
            {
                var bind = field.GetValue(null) as KeyBind ?? throw new Exception($"Bind {field} is null");
                bind.BindApp(app);
                _keyBinds.Add(bind);
            }

            void BindFields<T>(List<DataField<T>> list)
            {
                list.AddRange(fields
                        .Where(f => f.FieldType == typeof(DataField<T>))
                        .Select(field => field.GetValue(null) as DataField<T> ?? throw new Exception($"Bind {field} is null")));
            }

            BindFields(_floatFields);
            BindFields(_intFields);
        }
    }

    public void Load(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var storage = JsonSerializer.Deserialize<JsonStorage>(File.ReadAllText(path));

        if (storage == null)
        {
            return;
        }

        foreach (var bindSaved in storage.KeyBinds)
        {
            _keyBinds
                .FirstOrDefault(bindActual => bindActual.Name.Equals(bindSaved.Name))
                ?.Also(bindActual => bindActual.BindKey(bindSaved.Key));
        }

        foreach (var fieldSaved in storage.FloatFields)
        {
            _floatFields
                .FirstOrDefault(fieldActual => fieldActual.Name.Equals(fieldSaved.Name))
                ?.Also(fieldActual => fieldActual.Update(fieldSaved.Value));
        }
    }

    public void Save(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(
            new JsonStorage(_keyBinds.ToArray(), _floatFields.ToArray(), _intFields.ToArray()))
        );
    }

    public void ImGuiSubmit()
    {
        var changed = false;

        nint id = 1;
        foreach (var bindField in _keyBinds)
        {
            var key = bindField.Key;
            
            ImGui.PushID(id++);
            if (ImGuiExt.EnumComboBox(ref key, bindField.Name))
            {
                bindField.BindKey(key);
                changed = true;
            }
            ImGui.PopID();
        }

        foreach (var floatField in _floatFields)
        {
            var v = floatField.Value;
            ImGui.PushID(id++);
            if ((changed |= ImGui.InputFloat(floatField.Name, ref v)))
            {
                floatField.Update(v);
            }
            ImGui.PopID();

        }

        foreach (var intField in _intFields)
        {
            var v = intField.Value;
            ImGui.PushID(id++);
            if ((changed |= ImGui.InputInt(intField.Name, ref v)))
            {
                intField.Update(v);
            }
            ImGui.PopID();
        }

        if (changed)
        {
            OnChanged?.Invoke();
        }
    }

    public event Action? OnChanged;
}
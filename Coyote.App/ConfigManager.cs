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
    private readonly GameApplication? _app;

    [JsonInclude]
    public string Name { get; }

    [JsonIgnore]
    public bool Bound { get; }

    [JsonInclude]
    public Key Key { get; }

    [JsonConstructor]
    public KeyBind(string name, Key key)
    {
        Name = name;
        Key = key;
        Bound = false;
    }

    private KeyBind(string name, Key key, GameApplication app)
    {
        _app = app;
        Name = name;
        Key = key;
        Bound = true;
    }

    public KeyBind CreateBind(Key actualKey, GameApplication app)
    {
        return new KeyBind(Name, actualKey, app);
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
}

public sealed class FloatField
{
    [JsonInclude]
    public string Name { get; }

    [JsonInclude]
    public float Value { get; private set; }

    // todo investigate nullable to replace the different Slider and InputField in the composite node?
    [JsonIgnore]
    public Vector2? Range { get; }


    public FloatField(string name, float value, Vector2? range = null)
    {
        Name = name;
        Value = value;
        Range = range;
    }

    public void SendUpdate(float newValue)
    {
        var oldValue = Value;

        if (Range.HasValue)
        {
            newValue = Math.Clamp(newValue, Range.Value.X, Range.Value.Y);
        }

        Value = newValue;

        OnUpdate?.Invoke(oldValue, this);
    }

    public event Action<float, FloatField>? OnUpdate;

    public static implicit operator float(FloatField f) => f.Value;
}

internal class ConfigManager
{
    private readonly App _app;

    private class KeyBindField
    {
        public KeyBind BaseBind { get; set; }
        public FieldInfo Field { get; }

        public KeyBindField(KeyBind baseBind, FieldInfo field)
        {
            BaseBind = baseBind;
            Field = field;
        }
    }

    private sealed class JsonStorage
    {
        public JsonStorage(KeyBind[] keyBinds, FloatField[] floatFields)
        {
            KeyBinds = keyBinds;
            FloatFields = floatFields;
        }

        public KeyBind[] KeyBinds { get; }
        public FloatField[] FloatFields { get; }
    }

    private readonly List<KeyBindField> _keyBinds = new();
    private readonly List<FloatField> _floatFields = new();

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
                _keyBinds.Add(new KeyBindField(bind, field).Also(bf => UpdateBoundKey(bf, bf.BaseBind.Key)));
            }

            foreach (var field in fields.Where(f => f.FieldType == typeof(FloatField)))
            {
                var bind = field.GetValue(null) as FloatField ?? throw new Exception($"Bind {field} is null");
                _floatFields.Add(bind);
            }
        }
    }

    private void UpdateBoundKey(KeyBindField keyBindField, Key newKey)
    {
        var bind = keyBindField.BaseBind.CreateBind(newKey, _app);
        keyBindField.Field.SetValue(null, bind);
        keyBindField.BaseBind = bind;
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
                .FirstOrDefault(bindActual => bindActual.BaseBind.Name.Equals(bindSaved.Name))
                ?.Also(bindActual => UpdateBoundKey(bindActual, bindSaved.Key));
        }

        foreach (var fieldSaved in storage.FloatFields)
        {
            _floatFields
                .FirstOrDefault(fieldActual => fieldActual.Name.Equals(fieldSaved.Name))
                ?.Also(fieldActual => fieldActual.SendUpdate(fieldSaved.Value));
        }
    }

    public void Save(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(new JsonStorage(_keyBinds.Select(x => x.BaseBind).ToArray(), _floatFields.ToArray())));
    }

    public void ImGuiSubmit()
    {
        var changed = false;

        nint id = 1;
        foreach (var bindField in _keyBinds)
        {
            var key = bindField.BaseBind.Key;
            
            ImGui.PushID(id++);
            if (ImGuiExt.EnumComboBox(ref key, bindField.BaseBind.Name))
            {
                UpdateBoundKey(bindField, key);

                changed = true;
            }
            ImGui.PopID();
        }

        foreach (var floatField in _floatFields)
        {
            var v = floatField.Value;

            var flag = false;

            ImGui.PushID(id++);
            if (floatField.Range.HasValue)
            {
                flag |= ImGui.SliderFloat(floatField.Name, ref v, floatField.Range.Value.X, floatField.Range.Value.Y);
            }
            else
            {
                flag |= ImGui.InputFloat(floatField.Name, ref v);
            }
            ImGui.PopID();

            changed |= flag;

            if (flag)
            {
                floatField.SendUpdate(v);
            }
        }

        if (changed)
        {
            OnChanged?.Invoke();
        }
    }

    public event Action? OnChanged;
}
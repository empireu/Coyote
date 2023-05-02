using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameFramework;
using GameFramework.Utilities;
using Veldrid;

namespace Coyote.App;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class InputAccessorAttribute : Attribute { }

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

internal class InputManager
{
    private readonly App _app;

    private class BindField
    {
        public KeyBind BaseBind { get; set; }
        public FieldInfo Field { get; }

        public BindField(KeyBind baseBind, FieldInfo field)
        {
            BaseBind = baseBind;
            Field = field;
        }
    }

    private readonly List<BindField> _fields = new();

    public InputManager(App app)
    {
        _app = app;
        // Set up in current context by scanning for classes with InputAccessorAttribute:

        foreach (var classType in Assembly.GetCallingAssembly().ExportedTypes.Where(x => x.IsClass && x.GetCustomAttribute<InputAccessorAttribute>() != null))
        {
            foreach (var field in classType.GetFields(BindingFlags.Static | BindingFlags.Public).Where(f => f.FieldType == typeof(KeyBind)))
            {
                var bind = field.GetValue(null) as KeyBind
                           ?? throw new Exception($"Bind {field} is null");
               
                _fields.Add(new BindField(bind, field).Also(bf => UpdateField(bf, bf.BaseBind.Key)));
            }
        }
    }

    private void UpdateField(BindField bindField, Key newKey)
    {
        var bind = bindField.BaseBind.CreateBind(newKey, _app);
        bindField.Field.SetValue(null, bind);
        bindField.BaseBind = bind;
    }

    public void Load(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var savedBinds = JsonSerializer.Deserialize<KeyBind[]>(File.ReadAllText(path));

        if (savedBinds == null)
        {
            return;
        }

        foreach (var savedBind in savedBinds)
        {
            _fields.FirstOrDefault(b => b.BaseBind.Name.Equals(savedBind.Name))?.Also(bindField =>
            {
                UpdateField(bindField, savedBind.Key);
            });
        }
    }

    public void Save(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(_fields.Select(x=>x.BaseBind).ToArray()));
    }

    public void ImGuiSubmit()
    {
        var changed = false;

        foreach (var bindField in _fields)
        {
            var key = bindField.BaseBind.Key;
            
            if (ImGuiExt.EnumComboBox(ref key, bindField.BaseBind.Name))
            {
                UpdateField(bindField, key);

                changed = true;
            }
        }

        if (changed)
        {
            OnChanged?.Invoke();
        }
    }

    public event Action? OnChanged;
}
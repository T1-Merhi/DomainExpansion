public enum FieldKind
{
    Number,
    Bool,
    Text,
}

/// <summary>One editable leaf, flattened out of the JSON tree.</summary>
public sealed class ConfigField
{
    public string Path;          // dotted path, e.g. "weapons[0].stats.Damage"
    public string Label;         // display name
    public string Group;         // heading this sits under
    public int Depth;

    public FieldKind Kind;
    public JsonObject Parent;    // owning object
    public string Key;           // property name within Parent

    public float Min;
    public float Max;
    public float Step = 1f;
    public bool Bounded;

    public string OriginalText = "";

    public string CurrentText
    {
        get
        {
            JsonNode node = Parent[Key];
            return node == null ? "" : node.ToString();
        }
    }

    public bool IsDirty => CurrentText != OriginalText;

    public float CurrentNumber =>
        float.TryParse(CurrentText, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;

    public bool CurrentBool => bool.TryParse(CurrentText, out bool b) && b;

    public void SetNumber(float value)
    {
        if (Step >= 1f) value = MathF.Round(value);
        else value = MathF.Round(value / Step) * Step;

        if (Bounded) value = Math.Clamp(value, Min, Max);

        Parent[Key] = JsonValue.Create(value);
    }

    public void SetBool(bool value) => Parent[Key] = JsonValue.Create(value);

    public void SetText(string value) => Parent[Key] = JsonValue.Create(value);

    public void Revert() => Parent[Key] = ParseBack(OriginalText);

    private JsonNode ParseBack(string text)
    {
        if (Kind == FieldKind.Bool) return JsonValue.Create(bool.TryParse(text, out bool b) && b);

        if (Kind == FieldKind.Number &&
            float.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v))
        {
            return JsonValue.Create(v);
        }

        return JsonValue.Create(text);
    }
}

/// <summary>
/// One config file, held as a mutable JSON tree with its leaves flattened into
/// an editable list.
///
/// Working on the tree rather than the typed config classes is what makes the
/// editor generic: a field added to any JSON file becomes editable with no
/// change here, which is the requirement that "every value" be editable.
/// </summary>
public sealed class ConfigDocument
{
    public string FileName { get; }
    public string Title { get; }
    public List<ConfigField> Fields { get; } = new();

    private JsonObject _root;
    private JsonObject _schema;

    public bool LoadFailed { get; private set; }

    public bool IsDirty
    {
        get
        {
            foreach (var f in Fields) if (f.IsDirty) return true;
            return false;
        }
    }

    public ConfigDocument(string fileName)
    {
        FileName = fileName;
        Title = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
        Reload();
    }

    public void Reload()
    {
        Fields.Clear();
        LoadFailed = false;

        string json = ConfigPaths.ReadWithRetry(ConfigPaths.PathFor(FileName));
        if (json == null) { LoadFailed = true; return; }

        try
        {
            _root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) as JsonObject;
        }
        catch (JsonException)
        {
            LoadFailed = true;
            return;
        }

        if (_root == null) { LoadFailed = true; return; }

        LoadSchema();
        Flatten(_root, "", "", 0);
    }

    private void LoadSchema()
    {
        _schema = null;

        string schemaFile = Path.GetFileNameWithoutExtension(FileName) + ".schema.json";
        string json = ConfigPaths.ReadWithRetry(ConfigPaths.PathFor(schemaFile));
        if (json == null) return;

        try
        {
            _schema = (JsonNode.Parse(json) as JsonObject)?["fields"] as JsonObject;
        }
        catch (JsonException)
        {
            _schema = null;
        }
    }

    /// <summary>
    /// Walks the tree collecting scalar leaves. Arrays of objects become groups
    /// named by the item's id or type, so weapons.json reads as one section per
    /// weapon rather than "weapons[2].stats.Damage".
    /// </summary>
    private void Flatten(JsonObject obj, string prefix, string group, int depth)
    {
        foreach (var pair in obj)
        {
            string path = prefix.Length == 0 ? pair.Key : $"{prefix}.{pair.Key}";

            switch (pair.Value)
            {
                case JsonObject child:
                    Flatten(child, path, $"{group}{(group.Length > 0 ? " / " : "")}{pair.Key}", depth + 1);
                    break;

                case JsonArray array:
                    FlattenArray(array, path, pair.Key, depth);
                    break;

                case JsonValue value:
                    AddField(obj, pair.Key, path, group, depth, value);
                    break;
            }
        }
    }

    private void FlattenArray(JsonArray array, string path, string key, int depth)
    {
        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject item) continue;

            string name = item["id"]?.ToString() ?? item["type"]?.ToString() ?? $"{key}[{i}]";
            Flatten(item, $"{path}[{i}]", name, depth + 1);
        }
    }

    private void AddField(JsonObject parent, string key, string path, string group, int depth, JsonValue value)
    {
        // schemaVersion is metadata about the file, not something to tune.
        if (string.Equals(key, "schemaVersion", StringComparison.OrdinalIgnoreCase)) return;

        var field = new ConfigField
        {
            Path = path,
            Key = key,
            Parent = parent,
            Group = group,
            Depth = depth,
            Label = key,
            OriginalText = value.ToString(),
            Kind = KindOf(value),
        };

        ApplySchema(field);
        Fields.Add(field);
    }

    private static FieldKind KindOf(JsonValue value)
    {
        if (value.TryGetValue(out bool _)) return FieldKind.Bool;
        if (value.TryGetValue(out double _)) return FieldKind.Number;
        return FieldKind.Text;
    }

    /// <summary>
    /// Applies min/max/step when the schema names this field. Without an entry
    /// a numeric field still renders, bounded to a range derived from its
    /// current value - so an unannotated field is usable rather than absent.
    /// </summary>
    private void ApplySchema(ConfigField field)
    {
        JsonObject entry = _schema?[field.Key] as JsonObject ?? _schema?[field.Path] as JsonObject;

        if (entry != null)
        {
            if (entry["label"] != null) field.Label = entry["label"].ToString();

            if (entry["min"] != null && entry["max"] != null)
            {
                field.Min = (float)entry["min"].GetValue<double>();
                field.Max = (float)entry["max"].GetValue<double>();
                field.Bounded = true;
            }

            if (entry["step"] != null) field.Step = (float)entry["step"].GetValue<double>();
            return;
        }

        if (field.Kind != FieldKind.Number) return;

        float current = field.CurrentNumber;

        field.Min = current < 0f ? current * 2f : 0f;
        field.Max = MathF.Max(1f, MathF.Abs(current) * 2f);
        field.Step = MathF.Abs(current) < 5f ? 0.01f : 1f;
        field.Bounded = true;
    }

    public bool Save()
    {
        if (_root == null) return false;

        var options = new JsonSerializerOptions { WriteIndented = true };
        if (!ConfigPaths.AtomicWrite(ConfigPaths.PathFor(FileName), _root.ToJsonString(options)))
            return false;

        // Saved values become the new baseline, clearing the dirty markers.
        foreach (var f in Fields) f.OriginalText = f.CurrentText;
        return true;
    }

    public void RevertAll()
    {
        foreach (var f in Fields) f.Revert();
    }

    /// <summary>Restores the shipped file and reloads, so reset always works.</summary>
    public bool ResetToDefault()
    {
        if (!ConfigPaths.ResetToDefault(FileName)) return false;

        Reload();
        return true;
    }
}

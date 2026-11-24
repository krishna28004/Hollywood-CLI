using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hollywood.Cli.Configuration;

public sealed record AppConfiguration
{
    public int SchemaVersion { get; init; } = 1;
    public string Theme { get; init; } = "soc";
    public string Layout { get; init; } = "auto";
    public int Panels { get; init; } = 7;
    public string Speed { get; init; } = "normal";
    public int FrameRateLimit { get; init; } = 20;
    public uint Seed { get; init; } = 0x484F4C59;
    public string ColorMode { get; init; } = "auto";
    public string GlyphMode { get; init; } = "ascii";
    public bool ReducedMotion { get; init; }
    public KeyboardReactiveConfiguration KeyboardReactive { get; init; } = new();
    public string TelemetryMode { get; init; } = "simulated";
    public string[] EnabledModules { get; init; } = [
        "code", "system", "wave", "network", "security", "data", "radar", "topology", "build", "dashboard", "rain"
    ];

    public static string DefaultPath
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hollywood", "config.json");

            if (OperatingSystem.IsMacOS())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Hollywood", "config.json");

            var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var configRoot = !string.IsNullOrWhiteSpace(xdgConfigHome) && Path.IsPathRooted(xdgConfigHome)
                ? xdgConfigHome
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(configRoot, "hollywood", "config.json");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8,
        WriteIndented = true
    };

    public static AppConfiguration Load(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new InvalidDataException($"Configuration file not found: {path}");
        if (info.Length > 64 * 1024) throw new InvalidDataException("Configuration file exceeds the 64 KiB limit.");
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
        RejectDuplicateProperties(document.RootElement, "$");
        var config = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions)
            ?? throw new InvalidDataException("Configuration must contain a JSON object.");
        config.Validate();
        return config;
    }

    public void Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException($"Unsupported configuration schemaVersion: {SchemaVersion}.");
        if (Theme is not ("classic" or "cyberpunk" or "soc" or "matrix" or "retro")) throw new InvalidDataException("theme must be classic, cyberpunk, soc, matrix, or retro.");
        if (Layout is not ("auto" or "focus" or "dense")) throw new InvalidDataException("layout must be auto, focus, or dense.");
        if (Panels is not (2 or 3 or 6 or 7 or 10)) throw new InvalidDataException("panels must be 2, 3, 6, 7, or 10.");
        if (Speed is not ("slow" or "normal" or "fast")) throw new InvalidDataException("speed must be slow, normal, or fast.");
        if (FrameRateLimit is < 1 or > 60) throw new InvalidDataException("frameRateLimit must be between 1 and 60.");
        if (ColorMode is not ("auto" or "truecolor" or "16color" or "mono")) throw new InvalidDataException("colorMode must be auto, truecolor, 16color, or mono.");
        if (GlyphMode != "ascii") throw new InvalidDataException("Only ASCII glyph mode is currently supported.");
        if (TelemetryMode != "simulated") throw new InvalidDataException("Only simulated telemetry is currently supported.");
        KeyboardReactive.Validate();
        if (EnabledModules.Length is < 1 or > 11) throw new InvalidDataException("enabledModules must contain between 1 and 11 module names.");
        var allowed = new HashSet<string>(["code", "network", "rain", "data", "security", "system", "build", "wave", "radar", "topology", "dashboard"], StringComparer.Ordinal);
        if (EnabledModules.Any(name => !allowed.Contains(name)) || EnabledModules.Distinct(StringComparer.Ordinal).Count() != EnabledModules.Length)
            throw new InvalidDataException("enabledModules contains an unknown or duplicate module name.");
    }

    public static string Serialize(AppConfiguration config)
    {
        config.Validate();
        return JsonSerializer.Serialize(config, JsonOptions) + Environment.NewLine;
    }

    private static void RejectDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException($"Duplicate property '{property.Name}' at {path}.");
                RejectDuplicateProperties(property.Value, $"{path}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item, $"{path}[{index++}]");
        }
    }
}

public sealed record KeyboardReactiveConfiguration
{
    public bool Enabled { get; init; } = true;
    public float IdleIntensity { get; init; } = 0.05f;
    public float MaximumIntensity { get; init; } = 1f;
    public float Sensitivity { get; init; } = 1f;
    public double CooldownSeconds { get; init; } = 3d;

    public void Validate()
    {
        if (!float.IsFinite(IdleIntensity) || IdleIntensity is < 0f or > 1f) throw new InvalidDataException("keyboardReactive.idleIntensity must be between 0 and 1.");
        if (!float.IsFinite(MaximumIntensity) || MaximumIntensity < IdleIntensity || MaximumIntensity > 1f) throw new InvalidDataException("keyboardReactive.maximumIntensity must be between idleIntensity and 1.");
        if (!float.IsFinite(Sensitivity) || Sensitivity is < 0.1f or > 5f) throw new InvalidDataException("keyboardReactive.sensitivity must be between 0.1 and 5.");
        if (!double.IsFinite(CooldownSeconds) || CooldownSeconds is < 0.5d or > 10d) throw new InvalidDataException("keyboardReactive.cooldownSeconds must be between 0.5 and 10.");
    }
}

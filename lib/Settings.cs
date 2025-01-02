using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;

using System.Text.Json;
using Skatech.Euphoria;
using System.DirectoryServices;
using System.Windows.Media;

namespace Skatech.Components.Settings;

interface ISettings {
    string? Get(string name, string? defaultValue = default);
    string? Set(string name, string? value);
}

static class SettingsExtensions {
    public static string GetString(this ISettings settings,
            string name, string? defaultValue = default, bool propagateDefaultValue = false) {
        return settings.Get(name)
            ?? (propagateDefaultValue ? settings.Set(name, defaultValue) : defaultValue)
            ?? throw new InvalidOperationException($"Setting not found: \"{name}\"."); 
    }

    public static bool GetBoolean(this ISettings settings,
            string name, bool defaultValue = false, bool propagateDefaultValue = false) {
        var text = settings.Get(name);
        if (text != null) {
            return bool.Parse(text);
        }
        if (propagateDefaultValue) {
            settings.Set(name, defaultValue.ToString(CultureInfo.InvariantCulture));
        }
        return defaultValue;
    }
    
    public static int GetInteger(this ISettings settings,
            string name, int defaultValue = 0, bool propagateDefaultValue = false) {
        var text = settings.Get(name);
        if (text != null) {
            return int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }
        if (propagateDefaultValue) {
            settings.Set(name, defaultValue.ToString(CultureInfo.InvariantCulture));
        }
        return defaultValue;
    }

    public static double GetDouble(this ISettings settings,
            string name, double defaultValue = 0, bool propagateDefaultValue = false) {
        var text = settings.Get(name);
        if (text != null) {
            return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        if (propagateDefaultValue) {
            settings.Set(name, defaultValue.ToString(CultureInfo.InvariantCulture));
        }
        return defaultValue;
    }
    
    public static TEnum GetEnumeration<TEnum>(this ISettings settings, string name,
            TEnum defaultValue = default, bool propagateDefaultValue = false) where TEnum : struct {
        var text = settings.Get(name);
        if (text != null) {
            return (TEnum)Enum.Parse(typeof(TEnum), text);
        }
        if (propagateDefaultValue) {
            settings.Set(name, defaultValue.ToString());
        }
        return defaultValue;
    }

    public static string[] GetStrings(this ISettings settings, string name,
            string separator, string[] defaultValue, bool propagateDefaultValue = false) {
        var value = settings.Get(name);
        if (value != null) {
            return value.Split(separator);
        }
        if (propagateDefaultValue) {
            settings.Set(name, separator, defaultValue);
        }
        return defaultValue;
    }
    
    public static void Set(this ISettings settings, string name, bool value) {
        settings.Set(name, value.ToString(CultureInfo.InvariantCulture));
    }

    public static void Set(this ISettings settings, string name, int value) {
        settings.Set(name, value.ToString(CultureInfo.InvariantCulture));
    }

    public static void Set(this ISettings settings, string name, double value) {
        settings.Set(name, value.ToString(CultureInfo.InvariantCulture));
    }

    public static void Set(this ISettings settings, string name, Enum value) {
        settings.Set(name, value.ToString());
    }
    
    public static void Set(this ISettings settings, string name, string separator, IEnumerable<string> values) {
        settings.Set(name, String.Join(separator, values));
    }
}

class SettingsService : ISettings, IDisposable {
    readonly string _file;
    bool _modified;
    Dictionary<string, string> _data = new();

    public static SettingsService Create(string directory, bool useJSON = false) {
        return new SettingsService(Path.Combine(
            directory, useJSON ? "Settings.json" : "Settings.ini"));
    }

    public SettingsService(string file) {
        _file = file; Load();
    }

    ~SettingsService() {
        Dispose();
    }

    public void Dispose() {
        GC.SuppressFinalize(this);
        if (_modified) {
            Save();
            Debug.WriteLine("Settings saved");
        }
    }

    public string? Get(string name, string? defaultValue = default) {
        return (_data.TryGetValue(name, out string? value))
            ? value : defaultValue;
    }

    public string? Set(string name, string? value) {
        ValidateName(name);
        ValidateValue(value);
        if (value is null) {
            _data.Remove(name);
            return value;
        }
        if (Get(name) is string str && value == str) {
            return str;
        }
        _data[name] = value;
        _modified = true;
        return value;
    }

    public bool Load() {
        if (File.Exists(_file)) {
            _data = _file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(_file, Encoding.UTF8))!
                : File.ReadLines(_file, Encoding.UTF8).Select(ParseLine).ToDictionary();
            _modified = false;
            return true;
        }
        return false;
    }

    public void Save() {
        if(_file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) {
            File.WriteAllText(_file, JsonSerializer.Serialize(_data,
               new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        }
        else File.WriteAllLines(_file, _data.Select(p => $"{p.Key}={p.Value}"), Encoding.UTF8);
        _modified = false;
    }

    static KeyValuePair<string, string> ParseLine(string line) {
        int pos = line.IndexOf('=');
        if (pos > 0) {
            var key = line.Substring(0, pos);
            var val = line.Substring(pos + 1);
            ValidateName(key);
            ValidateValue(val);
            return new (key, val);
        }
        else throw new Exception($"Invalid config line format '{line}'");
    }

    static void ValidateName(string name) {
        if (name is not null && name.Length > 0)
            for (int i = 0;;)
                if (Char.IsAsciiLetterOrDigit(name[i])) {
                    if (++i == name.Length)
                        return;
                }
                else break;
        throw new Exception($"Invalid key format '{name?.Replace('\r', '?').Replace('\n', '?')}'");
    }

	static void ValidateValue(string? value) {
        if (value is not null)
            for (int i = 0; i < value.Length; ++i)
                if (value[i] == '\r' || value[i] == '\n')
                    throw new Exception(
                        $"Invalid value format '{value?.Replace('\r', '?').Replace('\n', '?')}'");
    }
}

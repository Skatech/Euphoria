using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace Skatech.IO;

static class IniFile {
    public const string DefaultExtension = ".ini";

    ///<summary>Returns sequence of records, or empty when file not exists</summary>
    public static IEnumerable<KeyValuePair<string, string>> Load(string file) {
        return File.Exists(file) 
            ? File.ReadLines(file, Encoding.UTF8).Select(ParseLine)
            : Enumerable.Empty<KeyValuePair<string, string>>();
    }

    public static void Save(string file, IEnumerable<KeyValuePair<string, string>> data) {
        File.WriteAllLines(file, data.Select(p => $"{p.Key}={p.Value}"), Encoding.UTF8);
    }

    public static KeyValuePair<string, string> ParseLine(string line) {
        int pos = line.IndexOf('=');
        if (pos > 0) {
            var key = line.Substring(0, pos);
            var val = line.Substring(pos + 1);
            ValidateKey(key);
            ValidateValue(val);
            return new (key, val);
        }
        else throw new Exception($"Invalid config line format '{line}'");
    }

    ///<summary>Returns key error description, null when key valid</summary>
    public static string? GetKeyError(string name) {
        if (string.IsNullOrEmpty(name))
            return "Key must not contain null or empty string";
        for (int i = 0; i < name.Length; ++i)
            if (!Char.IsAsciiLetterOrDigit(name[i]))
                return "Key must contain only letters or numbers";
        return null;
    }

    ///<summary>Returns value error description, null when key valid</summary>
	public static string? GetValueError(string? value) {
        if (value is not null)
            for (int i = 0; i < value.Length; ++i)
                if (value[i] == '\r' || value[i] == '\n')
                    return "Key must not contain new line symbols";
        return null;
    }

    ///<summary>Throws exception when key has errors</summary>
    public static void ValidateKey(string name) {
        if (GetKeyError(name) is string msg)
            throw new Exception(msg?.Replace('\r', '?').Replace('\n', '?'));
    }

    ///<summary>Throws exception when value has errors</summary>
	public static void ValidateValue(string? value) {
        if (GetValueError(value) is string msg)
            throw new Exception(msg?.Replace('\r', '?').Replace('\n', '?'));
    }
}
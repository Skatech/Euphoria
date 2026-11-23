using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Skatech.Components;

namespace Skatech.Euphoria;

class StoredSets {
    readonly string[][] _sets;

    public static StoredSets Default { get; } = new StoredSets(); 

    public string[] this[int index] => _sets[index];

    public bool UpdateSet(int index, IEnumerable<string> value) {
        if (!Enumerable.SequenceEqual(_sets[index], value)) {
            _sets[index] = value.ToArray();
            var sb = _sets.Aggregate(new StringBuilder(), (a, v) => a.AppendLine(String.Join('|', v)));
            var ss = Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
            ServiceLocator.Resolve<Skatech.Components.Settings.ISettings>().Set("StoredSets", ss);
            return true;
        }
        return false;
    }

    StoredSets() {
        if (ServiceLocator.Resolve<Skatech.Components.Settings.ISettings>().Get("StoredSets") is string s) {
            _sets = Encoding.UTF8.GetString(Convert.FromBase64String(s))
                .Split(Environment.NewLine).Select(s => s.Split('|', StringSplitOptions.RemoveEmptyEntries)).ToArray();
        }
        else _sets = Enumerable.Repeat(Array.Empty<string>(), 10).ToArray();
    }
}

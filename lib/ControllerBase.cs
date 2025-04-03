using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Skatech.Components.Presentation;

abstract class ControllerBase : INotifyPropertyChanged {
    readonly static Dictionary<string, PropertyChangedEventArgs> _cbcache = new (StringComparer.Ordinal);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = default) {
        ArgumentNullException.ThrowIfNull(propertyName, nameof(propertyName));
        if (PropertyChanged is not null)  {
            if (!_cbcache.TryGetValue(propertyName, out PropertyChangedEventArgs? args))
                _cbcache.Add(propertyName, args = new PropertyChangedEventArgs(propertyName));
            PropertyChanged(this, args);
        }
    }

    protected void OnPropertiesChanged(params string[] propertyNames) {
        foreach (var property in propertyNames)
            OnPropertyChanged(property);
    }

    protected bool TryUpdateField<T>(ref T field, T value, [CallerMemberName] string? propertyName = default) {
        if (value is null ? field is not null : !value.Equals(field)) {
            field = value;
            if (propertyName is not null)
                OnPropertyChanged(propertyName);
            return true;
        }
        return false;
    }
}

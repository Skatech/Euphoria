using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Input;

namespace Skatech.Components.Presentation;

static class WindowHelpers {
    public static Func<Key, bool> CreateKeyDoubleEventChecker() {
        var events = new Dictionary<Key, DateTime>();
        var period = TimeSpan.FromMilliseconds(150);
        bool Check(Key key) {
            if (events.TryGetValue(key, out DateTime prev) && period > (DateTime.Now - prev)) {
                Debug.WriteLine(DateTime.Now - prev);
                events[key] = DateTime.MinValue;
                return true;
            }
            events[key] = DateTime.Now;
            return false;
        }
        return Check;
    }

    public static bool FindTaggedObject<T>(object src, [NotNullWhen(true)] out T? val) {
        while (true) {
            if (src is FrameworkElement fel){
                if (fel.Tag is T obj) {
                    val = obj;
                    return true;
                }
                src = fel.Parent;
            }
            else if (src is FrameworkContentElement fcl){
                if (fcl.Tag is T obj) {
                    val = obj;
                    return true;
                }
                src = fcl.Parent;
            }
            else {
                val = default;
                return false;
            }
        }
    }
}
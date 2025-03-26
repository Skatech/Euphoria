using System.Windows;
using System.Collections.Generic;
using System.Globalization;
using Skatech.Components.Settings;

namespace Skatech.Components.Presentation;

static class WindowBoundsKeeper {
    static readonly Dictionary<Window, string> _records = new();
    
    public static void Register(Window window, string name, Size? defaultSize = null) {
        window.Closing += OnWindowClosing;
        _records.Add(window, name);
        if (ServiceLocator.Resolve<ISettings>().Get(_records[window]) is string s) {
            SetWindowBounds(window, Rect.Parse(s));
        }
        else if (defaultSize is Size size) {
            SetWindowSize(window, size);
        }
    }
    
    public static bool Unregister(Window window) {
        if (_records.Remove(window)) { 
            window.Closing -= OnWindowClosing;
            return true;
        }
        return false;
    }
    
    static void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e) {
        if (e.Cancel is false && sender is Window window) {
            ServiceLocator.Resolve<ISettings>().Set(_records[window],
                GetWindowBounds(window).ToString(CultureInfo.InvariantCulture));
            Unregister(window);
        }
    }

    static Rect GetWindowBounds(Window window) {
        return window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;
    }

    public static void SetWindowBounds(Window window, Rect bounds) {
        window.BeginInit();
        window.WindowState = WindowState.Normal;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = bounds.Left; window.Top = bounds.Top;
        window.Width = bounds.Width; window.Height = bounds.Height;
        window.EndInit();
    }

    public static void SetWindowSize(Window window, Size size) {
        window.BeginInit();
        window.WindowState = WindowState.Normal;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.Width = size.Width; window.Height = size.Height;
        window.EndInit();
    }
}

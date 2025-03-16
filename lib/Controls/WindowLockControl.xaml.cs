using System;
using System.Windows;
using System.Windows.Controls;

namespace Skatech.Presentation.CustomControls;

// Window data source must implement INotifyPropertyChanged and LockMessage property.
// So, just derive window controller from LockableControllerBase abstract class.

public partial class WindowLockControl : Grid {
    public WindowLockControl() {
        InitializeComponent();
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Cache;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using Skatech.IO;
using Skatech.Components;
using Skatech.Components.Presentation;

namespace Skatech.Euphoria;

public partial class ImagesItemsControl : ItemsControl {
    MainWindowController Controller => (MainWindowController)Window.GetWindow(this).DataContext;

    public ImagesItemsControl() {
        InitializeComponent();
    }

    void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e) {
        if (sender is ScrollViewer scv) {
            scv.ScrollToHorizontalOffset(scv.ScrollableWidth * 0.5);
            scv.ScrollToVerticalOffset(scv.ScrollableHeight * 0.5);
        }
    }

    private void OnHideImageGroupMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.DataContext is ImageGroupController igc)
            Controller.ShowImageGroup(igc, false);
    }  

    private void OnSelectAnotherGroupImageMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.Header is string name
                && sender is MenuItem mip && mip.Tag is ImageGroupController igc)
            igc.SelectVariant(name);
    }

    private void OnShiftImageMenuItemClick(object sender, RoutedEventArgs e) {
        if (e.OriginalSource is MenuItem mi && mi.DataContext is ImageGroupController igc)
            Controller.ShiftImageGroup(igc, mi.Header.Equals("_Right"));
    }

    private void OnShiftAnotherImageMenuItemClick(object sender, RoutedEventArgs e) {
        if (sender is MenuItem mi && mi.DataContext is ImageGroupController igc
            && e.OriginalSource is MenuItem smi && smi.DataContext is ImageGroupController igs)
                igc.Controller.ShiftImageGroups(igc, igs);
    }    

}
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using Skatech.Components.Presentation;

namespace Skatech.Euphoria;

partial class DiceWindow : Window {
    readonly DiceWindowController Controller;

    internal DiceWindow(Window owner) {
        InitializeComponent();
        Owner = owner;
        DataContext = Controller = new DiceWindowController();
    }

    private void OnKeyDownUp(object sender, KeyEventArgs e) {
        if (e.Handled = e.Key == Key.Escape) {
            Close();
        }
        else if (e.Handled = (e.Key == Key.D || e.Key == Key.Space || e.Key == Key.Enter) && e.IsDown && e.IsRepeat is false) {
            OnThrowDiceClick(this, null);
        }
    }

    void OnThrowDiceClick(object sender, RoutedEventArgs? e) {
        Controller.SwitchDiceboard();
    }
}

class DiceWindowController : ControllerBase {
    readonly static Geometry[] _edges = new[] {
        "M0,10L10,0H90L100,10V90L90,100H10L0,90V10 M45,45L55,55M45,55L55,45",
        "M0,10L10,0H90L100,10V90L90,100H10L0,90V10 M20,20L30,30M20,30L30,20 M70,70L80,80M70,80L80,70",
        "M0,10L10,0H90L100,10V90L90,100H10L0,90V10 M20,20L30,30M20,30L30,20 M45,45L55,55M45,55L55,45 M70,70L80,80M70,80L80,70",
        "M0,10L10,0H90L100,10V90L90,100H10L0,90V10 M20,20L30,30M20,30L30,20 M70,20L80,30M70,30L80,20 M20,70L30,80M20,80L30,70 M70,70L80,80M70,80L80,70",
        "M0,10L10,0H90L100,10V90L90,100H10L0,90V10 M20,20L30,30M20,30L30,20 M70,20L80,30M70,30L80,20 M45,45L55,55M45,55L55,45 M20,70L30,80M20,80L30,70 M70,70L80,80M70,80L80,70",
        "M0,10L10,0H90L100,10V90L90,100H10L0,90V10 M20,20L30,30M20,30L30,20 M70,20L80,30M70,30L80,20 M20,45L30,55M20,55L30,45 M70,45L80,55M70,55L80,45 M20,70L30,80M20,80L30,70 M70,70L80,80M70,80L80,70"
        }.Select(s => Geometry.Parse(s)).ToArray();

    public Geometry DiceFace { get; private set; } = _edges[0];
    public double DiceAngle { get; private set; } = 0;

    public DiceWindowController() {
        SwitchDiceboard();
    }

    public void SwitchDiceboard() {
        DiceAngle = Random.Shared.Next(360);
        DiceFace = _edges[Random.Shared.Next(_edges.Length)];
        OnPropertyChanged(nameof(DiceAngle));
        OnPropertyChanged(nameof(DiceFace));
    }
}

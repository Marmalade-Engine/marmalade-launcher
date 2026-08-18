using System;
using Avalonia.Controls;
using MarmaladeLauncher.Utils;

namespace MarmaladeLauncher.Views;

public partial class MainWindow : Window {
    public MainWindow() {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e) {
        base.OnOpened(e);

        bool isRtl = System.Globalization.CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
        this.SetRTL(isRtl);
    }
}
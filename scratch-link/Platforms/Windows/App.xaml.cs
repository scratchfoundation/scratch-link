// <copyright file="App.xaml.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace ScratchLink.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class, the singleton application object. This is the
    /// line of authored code executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Build and return a MauiApp instance to host our app on Windows.
    /// MacCatalyst-specific configuration can go here.
    /// </summary>
    /// <returns>A new instance of <see cref="MauiApp"/> configured for our app.</returns>
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

// <copyright file="App.xaml.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Win;

using Microsoft.UI.Xaml;
using ScratchLink;
using ScratchLink.Win.BLE;
using System.Diagnostics;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private ScratchLinkApp app;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Trace.Listeners.Add(??);
        Trace.WriteLine("Starting...");

        var appBuilder = new ScratchLinkApp.Builder();
        appBuilder.SetArguments(Environment.GetCommandLineArgs());
        appBuilder.SetSessionManager<WinSessionManager>();
        appBuilder.SetGattHelpers<WinGattHelpers, Guid>();

        this.app = appBuilder.Build();

        this.app.Run();
    }
}

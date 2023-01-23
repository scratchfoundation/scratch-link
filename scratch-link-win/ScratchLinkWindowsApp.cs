// <copyright file="ScratchLinkWindowsApp.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Win;

using ScratchLink.Win.BLE;
using System.Diagnostics;

/// <summary>
/// Windows application context to hold the <c>ScratchLinkApp</c> instance.
/// </summary>
public class ScratchLinkWindowsApp : ApplicationContext
{
    private ScratchLinkApp app;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScratchLinkWindowsApp"/> class.
    /// </summary>
    /// <param name="args">Arguments passed from the command line.</param>
    public ScratchLinkWindowsApp(string[] args)
    {
        // Trace.Listeners.Add(??);
        Trace.WriteLine("Starting...");

        var appBuilder = new ScratchLinkApp.Builder();
        appBuilder.SetArguments(args);
        appBuilder.SetSessionManager<WinSessionManager>();
        appBuilder.SetGattHelpers<WinGattHelpers, Guid>();

        this.app = appBuilder.Build();

        this.app.Run();
    }
}

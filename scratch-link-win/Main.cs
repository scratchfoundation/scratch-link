// <copyright file="Main.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Win;

/// <summary>
/// Application entry point.
/// </summary>
internal static class MainClass
{
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var appContext = new ScratchLinkWindowsApp(args);
        Application.Run(appContext);
    }
}

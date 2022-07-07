// <copyright file="Main.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac;

using AppKit;

/// <summary>
/// Application entry point.
/// </summary>
internal static class MainClass
{
    private static void Main(string[] args)
    {
        NSApplication.Init();
        NSApplication.Main(args);
    }
}

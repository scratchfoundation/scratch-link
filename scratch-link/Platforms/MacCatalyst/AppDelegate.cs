// <copyright file="AppDelegate.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using Foundation;

/// <summary>
/// The AppDelegate connects UIApplication to MauiApp on MacCatalyst.
/// </summary>
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    /// <summary>
    /// Build and return a MauiApp instance to host our app on MacCatalyst.
    /// MacCatalyst-specific configuration can go here.
    /// </summary>
    /// <returns>A new instance of <see cref="MauiApp"/> configured for our app.</returns>
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

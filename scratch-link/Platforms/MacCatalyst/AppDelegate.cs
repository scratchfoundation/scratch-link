// <copyright file="AppDelegate.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using CoreBluetooth;
using Foundation;
using ScratchLink.BLE;
using ScratchLink.Platforms.MacCatalyst;
using ScratchLink.Platforms.MacCatalyst.BLE;

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
    protected override MauiApp CreateMauiApp()
    {
        var builder = MauiProgram.CreateMauiAppBuilder();
        builder.Services.Add(new ServiceDescriptor(typeof(SessionManager), typeof(MacSessionManager), ServiceLifetime.Singleton));
        builder.Services.Add(new ServiceDescriptor(typeof(GattHelpers<CBUUID>), typeof(MacGattHelpers), ServiceLifetime.Singleton));
        return builder.Build();
    }
}

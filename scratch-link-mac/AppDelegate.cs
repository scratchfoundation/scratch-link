// <copyright file="AppDelegate.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac;

using AppKit;
using Foundation;

/// <summary>
/// Scratch Link's implementation of the NSApplicationDelegate protocol.
/// </summary>
[Register("AppDelegate")]
public class AppDelegate : NSApplicationDelegate
{
    /// <summary>
    /// Called when the app's initialization is complete but it hasn't received its first event.
    /// </summary>
    /// <param name="notification">A notification named <c>didFinishLaunchingNotification</c>.</param>
    public override void DidFinishLaunching(NSNotification notification)
    {
        // Insert code here to initialize your application
    }

    /// <summary>
    /// Called when the app is about to terminate.
    /// </summary>
    /// <param name="notification">A notification named <c>willTerminateNotification</c>.</param>
    public override void WillTerminate(NSNotification notification)
    {
        // Insert code here to tear down your application
    }
}

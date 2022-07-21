// <copyright file="AppDelegate.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac;

using System;
using System.Reflection;
using AppKit;
using CoreBluetooth;
using Foundation;
using ObjCRuntime;
using ScratchLink.Mac.BLE;

/// <summary>
/// Scratch Link's implementation of the NSApplicationDelegate protocol.
/// </summary>
[Register("AppDelegate")]
public class AppDelegate : NSApplicationDelegate
{
    private readonly Selector onVersionItemSelector;
    private readonly Selector onQuitSelector;

    private NSStatusItem statusBarItem;

    private ScratchLinkApp app;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppDelegate"/> class.
    /// </summary>
    public AppDelegate()
    {
        this.onVersionItemSelector = this.ConnectMethodToSelector(this.OnVersionItemSelected, "onVersionItemSelected");
        this.onQuitSelector = this.ConnectMethodToSelector(this.OnQuitSelected, "onQuitSelected");
    }

    /// <summary>
    /// Called when the app's initialization is complete but it hasn't received its first event.
    /// </summary>
    /// <param name="notification">A notification named <c>didFinishLaunchingNotification</c>.</param>
    public override void DidFinishLaunching(NSNotification notification)
    {
        var appBuilder = new ScratchLinkApp.Builder();
        appBuilder.SetArguments(new NSProcessInfo().Arguments);
        appBuilder.SetSessionManager<MacSessionManager>();
        appBuilder.SetGattHelpers<MacGattHelpers, CBUUID>();

        this.app = appBuilder.Build();

        this.statusBarItem = this.BuildStatusBarItem();

        this.app.Run();
    }

    /// <summary>
    /// Called when the app is about to terminate.
    /// </summary>
    /// <param name="notification">A notification named <c>willTerminateNotification</c>.</param>
    public override void WillTerminate(NSNotification notification)
    {
        this.app.Quit();
    }

    private Selector ConnectMethodToSelector(Action action, string selectorName)
    {
        var asDelegate = action as Delegate;
        var methodInfo = asDelegate.Method;
        var selector = new Selector(selectorName);
        Runtime.ConnectMethod(methodInfo, selector);
        return selector;
    }

    private NSStatusItem BuildStatusBarItem()
    {
        var appTitle = BundleInfo.Title;
        var appVersion = BundleInfo.Version;

        var menu = new NSMenu(appTitle);
        menu.AddItem($"{appTitle} {appVersion}", this.onVersionItemSelector, string.Empty);
        menu.AddItem(NSMenuItem.SeparatorItem);
        menu.AddItem("Quit", this.onQuitSelector, "q");

        var statusBarItem = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Square);
        var button = statusBarItem.Button;
        button.ImageScaling = NSImageScale.ProportionallyUpOrDown;
        button.Image = this.GetAppIcon();
        statusBarItem.Menu = menu;

        return statusBarItem;
    }

    private NSImage GetAppIcon()
    {
        try
        {
            var statusBarIcon = NSImage.ImageNamed("iconTemplate");
            if (statusBarIcon != null)
            {
                return statusBarIcon;
            }
        }
        catch (Exception)
        {
            // fall through
        }

        // image was not found, failed to load, etc.
        return NSImage.ImageNamed(NSImageName.Caution);
    }

    private void OnVersionItemSelected()
    {
        // todo
    }

    private void OnQuitSelected()
    {
        // todo: actually terminate the app
        // if doing so causes WillTerminate() to be called, remove this Quit() call.
        this.app.Quit();
    }
}

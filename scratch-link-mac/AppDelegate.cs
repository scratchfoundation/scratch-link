// <copyright file="AppDelegate.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac;

using System;
using System.Diagnostics;
using AppKit;
using CoreBluetooth;
using Foundation;
using ObjCRuntime;
using SafariServices;
using ScratchLink.Mac.BLE;

/// <summary>
/// Scratch Link's implementation of the NSApplicationDelegate protocol.
/// </summary>
[Register("AppDelegate")]
public class AppDelegate : NSApplicationDelegate
{
    // This identifier must match the PRODUCT_BUNDLE_IDENTIFIER setting for the extension's Xcode project
    private const string ExtensionBundleIdentifier = "scratch.Scratch-Link-Safari-Helper.Extension";

    private const string VersionItemSelected = "versionItemSelected:";
    private const string ExtensionItemSelected = "extensionItemSelected:";
    private const string QuitItemSelected = "quitItemSelected:";

    private NSStatusItem statusBarItem;

    private ScratchLinkApp app;

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

    private NSStatusItem BuildStatusBarItem()
    {
        var appTitle = BundleInfo.Title;
        var appVersion = BundleInfo.Version;

        var menu = new NSMenu(appTitle);
        menu.AddItem($"{appTitle} {appVersion}", new Selector(VersionItemSelected), string.Empty);
        menu.AddItem(NSMenuItem.SeparatorItem);
        menu.AddItem("Manage Safari extensions", new Selector(ExtensionItemSelected), string.Empty);
        menu.AddItem(NSMenuItem.SeparatorItem);
        menu.AddItem("Quit", new Selector(QuitItemSelected), "q");

        var statusBarItem = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Square);
        var button = statusBarItem.Button;
        button.ImageScaling = NSImageScale.ProportionallyUpOrDown;
        button.Image = this.GetAppIcon();
        button.Image.Template = true;
        statusBarItem.Menu = menu;

        return statusBarItem;
    }

    private NSImage GetAppIcon()
    {
        try
        {
            var statusBarIcon = NSImage.ImageNamed("StatusBarIcon");
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

    [Action(VersionItemSelected)]
    private void OnVersionItemSelected(NSObject sender)
    {
        var versionDetailLines = new[]
        {
            $"{BundleInfo.Title} {BundleInfo.Version} {BundleInfo.VersionDetail}",
            $"macOS {NSProcessInfo.ProcessInfo.OperatingSystemVersionString}",
        };
        var versionDetails = string.Join('\n', versionDetailLines);

        NSPasteboard.GeneralPasteboard.ClearContents();
        NSPasteboard.GeneralPasteboard.SetStringForType(versionDetails, NSPasteboard.NSStringType);

        var notification = new NSUserNotification()
        {
            Title = "Version information copied to clipboard",
            InformativeText = versionDetails,
        };
        NSUserNotificationCenter.DefaultUserNotificationCenter.DeliverNotification(notification);
    }

    [Action(ExtensionItemSelected)]
    private void OnExtensionItemSelected(NSObject sender)
    {
        SFSafariApplication.ShowPreferencesForExtension(ExtensionBundleIdentifier, this.OnShowExtensionCompleted);
    }

    private void OnShowExtensionCompleted(NSError error)
    {
        if (error != null)
        {
            Debug.Print($"Error showing Safari extension preferences: ${error}");
        }
    }

    [Action(QuitItemSelected)]
    private void OnQuitSelected(NSObject sender)
    {
        // this will cause WillTerminate to run
        NSApplication.SharedApplication.Terminate(sender);
    }
}

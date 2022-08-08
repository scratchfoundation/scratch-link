// <copyright file="AppDelegate.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Mac;

using System;
using System.Collections.Generic;
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

        var extensionMenuItem = new NSMenuItem("Manage Safari extensions", new Selector(ExtensionItemSelected), string.Empty);

        var menu = new NSMenu(appTitle);
        menu.AddItem($"{appTitle} {appVersion}", new Selector(VersionItemSelected), string.Empty);
        menu.AddItem(extensionMenuItem);
        menu.AddItem(NSMenuItem.SeparatorItem);
        menu.AddItem("Quit", new Selector(QuitItemSelected), "q");

        // Safari treats even signed extensions as "unsigned" unless they come through the Mac App Store
        // so consider this menu item "advanced" unless this is a signed MAS build.
#if !SIGNED_MAS
        menu.Delegate = new HideAdvancedMenuItemsDelegate
        {
            AdvancedMenuItems =
            {
                extensionMenuItem,
            },
        };
#endif

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
        var buildType =
#if DEBUG
            "Unsigned Debug";
#elif SIGNED_MAS
            "Mac App Store";
#elif SIGNED_DEVID
            "Developer ID";
#else
            "Unsigned Release";
#endif

        var versionDetailLines = new[]
        {
            $"{BundleInfo.Title} {BundleInfo.Version} {BundleInfo.VersionDetail}",
            $"Build type: {buildType}",
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
            Debug.Print($"Error showing Safari extension preferences: {error}");
        }
    }

    [Action(QuitItemSelected)]
    private void OnQuitSelected(NSObject sender)
    {
        // this will cause WillTerminate to run
        NSApplication.SharedApplication.Terminate(sender);
    }

    private class HideAdvancedMenuItemsDelegate : NSObject, INSMenuDelegate
    {
        public ICollection<NSMenuItem> AdvancedMenuItems { get; private set; } = new List<NSMenuItem>();

        public void MenuWillHighlightItem(NSMenu menu, NSMenuItem item)
        {
            // nothing special
        }

        [Export("menuWillOpen:")]
        public void MenuWillOpen(NSMenu menu)
        {
            const NSEventModifierMask optionKeyMask = NSEventModifierMask.AlternateKeyMask;
            var shouldHideAdvancedItems = !NSEvent.CurrentModifierFlags.HasFlag(optionKeyMask);

            foreach (var menuItem in this.AdvancedMenuItems)
            {
                menuItem.Hidden = shouldHideAdvancedItems;
            }
        }
    }
}

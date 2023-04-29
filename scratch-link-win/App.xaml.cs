// <copyright file="App.xaml.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink.Win;

using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using ScratchLink;
using ScratchLink.Win.BLE;
using System.Diagnostics;
using System.Reflection;
using Windows.ApplicationModel.DataTransfer;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private readonly string appTitle;
    private readonly string versionQuad;
    private readonly string versionDetail;

    private ScratchLinkApp app;
    private TaskbarIcon trayIcon;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    public App()
    {
        // Project setting -> Assembly attribute -> "Details" tab of the EXE/DLL properties
        // <AssemblyName> -> AssemblyTitleAttribute -> "File description" (and "Description" on the "General" tab)
        // <FileVersion> -> AssemblyFileVersionAttribute -> "File version"
        // <Product> -> AssemblyProductAttribute -> "Product name"
        // <Version> -> AssemblyInformationalVersionAttribute -> "Product version"
        var entryAssembly = Assembly.GetEntryAssembly();
        this.appTitle = entryAssembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
        this.versionQuad = entryAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        this.versionDetail = entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

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

        this.InitializeTrayIcon();

        this.app.Run();
    }

    private void InitializeTrayIcon()
    {
        var copyVersionCommand = (XamlUICommand)this.Resources["CopyVersionCommand"];
        copyVersionCommand.Label = $"{this.appTitle} {this.versionQuad}";
        copyVersionCommand.ExecuteRequested += this.CopyVersionCommand_ExecuteRequested;

        var exitCommand = (XamlUICommand)this.Resources["ExitCommand"];
        exitCommand.ExecuteRequested += this.ExitCommand_ExecuteRequested;

        this.trayIcon = (TaskbarIcon)this.Resources["ScratchLinkTaskbarIcon"];

        // TODO: maybe we should enable efficiency mode when there are no connections?
        this.trayIcon.ForceCreate(enablesEfficiencyMode: false);
    }

    private void CopyVersionCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        // TODO: signed/unsigned?
        var buildType =
#if DEBUG
            "Debug";
#else
            "Release";
#endif

        var versionDetailLines = new[]
        {
            $"Title: {this.appTitle}",
            $"Version: {this.versionDetail}",
            $"Build type: {buildType}",
            $"OS: Windows {Environment.OSVersion.VersionString}",
        };
        var versionDetails = string.Join('\n', versionDetailLines);

        var clipboardData = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy,
        };
        clipboardData.SetText(versionDetails);
        Clipboard.SetContent(clipboardData);

        this.trayIcon.ShowNotification(
            title: "Version information copied to clipboard",
            message: versionDetails,
            icon: H.NotifyIcon.Core.NotificationIcon.Info,
            timeout: TimeSpan.FromSeconds(5));
    }

    private void ExitCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        this.trayIcon.Dispose();

        // https://github.com/HavenDV/H.NotifyIcon/issues/66
        Environment.Exit(0);
    }
}

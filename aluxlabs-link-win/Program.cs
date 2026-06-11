// <copyright file="Program.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace AluxLabs.Link.Win;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WinRT;

/// <summary>
/// Custom entry point that enforces single-instance activation before the XAML runtime starts.
/// </summary>
public static class Program
{
    [STAThread]
    private static void Main()
    {
        var keyInstance = AppInstance.FindOrRegisterForKey("aluxlabs-link-main");

        // Exit before the tray icon or :20211 listener start so a second launch can't duplicate them.
        if (!keyInstance.IsCurrent)
        {
            var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            keyInstance.RedirectActivationToAsync(activatedArgs).AsTask().Wait();
            return;
        }

        ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}

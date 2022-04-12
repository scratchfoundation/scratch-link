// <copyright file="ScratchLinkApp.xaml.cs" company="Scratch Foundation">
// Copyright (c) Scratch Foundation. All rights reserved.
// </copyright>

namespace ScratchLink;

using ScratchLink.Resources.Strings;

using System.Net;
using System.Net.WebSockets;

/// <summary>
/// The <see cref="ScratchLinkApp"/> class contains the cross-platform entry point for the application.
/// </summary>
public partial class ScratchLinkApp : Application
{
    private const int WebSocketPort = 20111;

    private readonly WebSocketListener webSocketListener;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScratchLinkApp"/> class.
    /// This is the cross-platform entry point.
    /// </summary>
    public ScratchLinkApp()
    {
        this.InitializeComponent();

        this.MainPage = new MainPage();

        if (!HttpListener.IsSupported)
        {
            // TODO: this doesn't work right
            this.MainPage.DisplayAlert("Error", AppResource.HTTP_Listener_not_supported, "Quit").ContinueWith(task =>
            {
                this.Quit();
            });
            return;
        }

        this.webSocketListener = new ()
        {
            Handlers =
            {
                { ServicePath.BLE, this.HandleSessionDebug },
                { ServicePath.BT, this.HandleSessionDebug },
            },
        };
        this.webSocketListener.Listen(new[]
        {
            string.Format("http://127.0.0.1:{0}/", WebSocketPort),
            string.Format("http://localhost:{0}/", WebSocketPort),
        });
    }

    private void HandleSessionDebug(HttpListenerWebSocketContext context)
    {
        var origin = context.Headers.Get("origin");
        var socket = context.WebSocket;
        this.Dispatcher.Dispatch(() =>
        {
            this.MainPage.DisplayAlert("New connection", string.Format("Path: {0}\nOrigin: {1}", context.RequestUri.AbsolutePath, origin), "OK");
        });
        socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
    }

    private static class ServicePath
    {
        public const string BLE = "/scratch/ble";
        public const string BT = "/scratch/bt";
    }
}

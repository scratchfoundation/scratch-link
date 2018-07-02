using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Windows.Forms;

namespace scratch_link
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        const int SDMPort = 20110;

        private static class SDMPath
        {
            public const string BLE = "/scratch/ble";
            public const string BT = "/scratch/bt";
        }

        private readonly NotifyIcon _icon;
        private readonly HttpListener _server;
        private readonly SortedDictionary<string, SessionManager> _sessionManagers;

        private App()
        {
            _icon = new NotifyIcon
            {
                Icon = SystemIcons.Warning, // TODO: get a real icon
                Text = scratch_link.Properties.Resources.AppTitle,
                ContextMenu = MakeContextMenu(),
                Visible = true
            };

            _sessionManagers = new SortedDictionary<string, SessionManager>
            {
                [SDMPath.BLE] = new SessionManager(webSocket => new BLESession(webSocket)),
                [SDMPath.BT] = new SessionManager(webSocket => new BTSession(webSocket))
            };

            _server = new HttpListener();
            // "http://+:{SDMPort}" would be better but requires elevated privileges
            _server.Prefixes.Add($"http://127.0.0.1:{SDMPort}/");
            _server.Start();

            AcceptNextClient();
            UpdateIconText();
        }

        private void PrepareToClose()
        {
            _icon.Visible = false;
            _server.Close();
        }

        private ContextMenu MakeContextMenu()
        {
            var quitItem = new MenuItem
            {
                Index = 0,
                Text = "E&xit"
            };
            quitItem.Click += OnExitClicked;

            var menu = new ContextMenu()
            {
                MenuItems =
                {
                    quitItem
                }
            };

            return menu;
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
            PrepareToClose();
            Environment.Exit(0);
        }

        private void AcceptNextClient()
        {
            // If the server isn't listening the app is probably quitting
            if (_server.IsListening)
            {
                _server.BeginGetContext(ClientDidConnect, null);
            }
        }

        private async void ClientDidConnect(IAsyncResult ar)
        {
            if (!_server.IsListening)
            {
                // App is probably quitting
                return;
            }

            // Get ready for another connection
            AcceptNextClient();

            // Process this connection
            WebSocket webSocket;

            var listenerContext = _server.EndGetContext(ar);
            try
            {
                var websocketContext = await listenerContext.AcceptWebSocketAsync(null);
                webSocket = websocketContext.WebSocket;
            }
            catch (Exception e)
            {
                listenerContext.Response.StatusCode = 500;
                listenerContext.Response.Close();
                Debug.Print($"Exception attempting to accept a web socket connection: {e}");
                return;
            }

            var sessionManager = _sessionManagers[listenerContext.Request.Url.AbsolutePath];
            if (sessionManager != null)
            {
                sessionManager.ClientDidConnect(webSocket);
            }
            else
            {
                listenerContext.Response.StatusCode = 404;
                listenerContext.Response.Close();
                Debug.Print($"Client tried to connect to unknown path: {listenerContext.Request.Url.AbsolutePath}");
            }

            UpdateIconText();
        }

        private void UpdateIconText()
        {
            int totalSessions = _sessionManagers.Values.Aggregate(0,
                (total, sessionManager) =>
                {
                    return total + sessionManager.ActiveSessionCount;
                }
            );

            string text = scratch_link.Properties.Resources.AppTitle;
            if (totalSessions > 0)
            {
                text += $"{Environment.NewLine}{totalSessions} active {(totalSessions == 1 ? "session" : "sessions")}";
            }
            _icon.Text = text;
        }

        private void Application_Exit(object sender, System.Windows.ExitEventArgs e)
        {
            PrepareToClose();
        }
    }
}

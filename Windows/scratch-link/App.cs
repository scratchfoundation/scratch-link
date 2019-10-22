using Fleck;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

namespace scratch_link
{
    public class App : ApplicationContext
    {
        const int SDMPort = 20110;

        private static class SDMPath
        {
            public const string BLE = "/scratch/ble";
            public const string BT = "/scratch/bt";
        }

        private readonly NotifyIcon _icon;
        private readonly WebSocketServer _server;
        private readonly SortedDictionary<string, SessionManager> _sessionManagers;

        public App()
        {
            Application.ApplicationExit += new EventHandler(Application_Exit);

            var appAssembly = typeof(App).Assembly;
            var simpleVersionString = $"{scratch_link.Properties.Resources.AppTitle} {appAssembly.GetName().Version}";
            _icon = new NotifyIcon
            {
                Icon = scratch_link.Properties.Resources.NotifyIcon,
                Text = scratch_link.Properties.Resources.AppTitle,
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
                {
                    Items =
                    {
                        new ToolStripMenuItem(simpleVersionString, null, OnAppVersionClicked) {
                            ToolTipText = "Copy version to clipboard"
                        },
                        new ToolStripSeparator(),
                        new ToolStripMenuItem("E&xit", null, OnExitClicked)
                    },
                    ShowItemToolTips = true
                }
            };

            _sessionManagers = new SortedDictionary<string, SessionManager>
            {
                [SDMPath.BLE] = new SessionManager(webSocket => new BLESession(webSocket)),
                [SDMPath.BT] = new SessionManager(webSocket => new BTSession(webSocket))
            };
            foreach (var sessionManager in _sessionManagers.Values)
            {
                sessionManager.ActiveSessionCountChanged += new EventHandler(UpdateIconText);
            }

            var certificate = new X509Certificate2(scratch_link.Properties.Resources.WssCertificate, "Scratch");
            _server = new WebSocketServer($"wss://0.0.0.0:{SDMPort}")
            {
                RestartAfterListenError = true,
                Certificate = certificate
            };

            try
            {
                _server.Start(OnNewSocket);
            }
            catch (SocketException e)
            {
                switch (e.SocketErrorCode)
                {
                    case SocketError.AddressAlreadyInUse:
                        OnAddressInUse();
                        break;
                    default:
                        throw;
                }
            }

            UpdateIconText(this, null);
        }

        private void OnAddressInUse()
        {
            PrepareToClose();

            var title = "Address already in use!";
            var body = String.Format(
                "{0} was unable to start because port {1} is already in use.\n" +
                "\n" +
                "This means {0} is already running or another application is using that port.\n" +
                "\n" +
                "This application will now exit.",
                scratch_link.Properties.Resources.AppTitle,
                SDMPort
            );
            MessageBox.Show(body, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

            Environment.Exit(1);
        }

        private void OnAppVersionClicked(object sender, EventArgs e)
        {
            var appAssembly = typeof(App).Assembly;
            var informationalVersionAttribute =
                appAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            var versionDetails = string.Join(Environment.NewLine,
                $"{scratch_link.Properties.Resources.AppTitle} {informationalVersionAttribute.InformationalVersion}",
                Environment.OSVersion.Platform
            );

            Clipboard.SetText(versionDetails);
            _icon.ShowBalloonTip(5000, "Version information copied to clipboard", versionDetails, ToolTipIcon.Info);
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
            PrepareToClose();
            Environment.Exit(0);
        }

        private void PrepareToClose()
        {
            _icon.Visible = false;
            _server.Dispose();
        }

        private void OnNewSocket(IWebSocketConnection websocket)
        {
            var sessionManager = _sessionManagers[websocket.ConnectionInfo.Path];
            if (sessionManager != null)
            {
                sessionManager.ClientDidConnect(websocket);
            }
            else
            {
                // TODO: reply with a message indicating that the client connected to an unknown/invalid path
                // See https://github.com/statianzo/Fleck/issues/199
                websocket.OnOpen = () => websocket.Close();
                Debug.Print($"Client tried to connect to unknown path: {websocket.ConnectionInfo.Path}");
            }
        }

        internal void UpdateIconText(object sender, EventArgs e)
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

        private void Application_Exit(object sender, EventArgs args)
        {
            PrepareToClose();
        }
    }
}

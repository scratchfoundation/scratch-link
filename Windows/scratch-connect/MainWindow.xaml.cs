using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;

namespace scratch_connect
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly System.Windows.Forms.NotifyIcon _icon;

        public MainWindow()
        {
            InitializeComponent();

            _icon = new System.Windows.Forms.NotifyIcon
            {
                Icon = SystemIcons.Warning, // TODO: get a real icon
                Text = Properties.Resources.AppTitle,
                Visible = true
            };
            _icon.DoubleClick += delegate
            {
                Show();
                WindowState = WindowState.Normal;
            };
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
            base.OnStateChanged(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _icon.Visible = false;
            base.OnClosing(e);
        }
    }
}

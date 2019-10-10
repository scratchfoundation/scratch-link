using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace scratch_link.Dialogs
{
    public partial class AddressInUse : Form
    {
        string textContent;

        public AddressInUse()
        {
            InitializeComponent();

            pictureBox1.Image = SystemIcons.Error.ToBitmap();

            textContent = string.Format(
                "{0} was unable to start because port {1} is already in use.\n" +
                "\n" +
                "This means {0} is already running or another application is using that port.\n" +
                "\n" +
                "This application will now exit.",
                scratch_link.Properties.Resources.AppTitle,
                App.SDMPort
            );
            label1.Text = textContent;

            textContent += "\n\nDetails:\n PID \tProcess Name\n";

            var pid = Fiddler.Winsock.MapLocalPortToProcessId(App.SDMPort);
            if (pid > 0)
            {
                var process = Process.GetProcessById(pid);
                var item = new ListViewItem(new [] { process.ProcessName, pid.ToString() });
                listView1.Items.Add(item);
                textContent += pid.ToString() + '\t' + process.ProcessName + '\n';
            }
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void CopyButton_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(textContent);
        }
    }
}

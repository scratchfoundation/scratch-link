using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace scratch_link.Dialogs
{
    public partial class AddressInUse : Form
    {
        public AddressInUse()
        {
            InitializeComponent();

            pictureBox1.Image = SystemIcons.Error.ToBitmap();
            label1.Text = String.Format(
                "{0} was unable to start because port {1} is already in use.\n" +
                "\n" +
                "This means {0} is already running or another application is using that port.\n" +
                "\n" +
                "This application will now exit.",
                scratch_link.Properties.Resources.AppTitle,
                App.SDMPort
            );

            var pid = Fiddler.Winsock.MapLocalPortToProcessId(App.SDMPort);
            if (pid > 0)
            {
                var process = Process.GetProcessById(pid);
                var item = new ListViewItem(new [] { process.ProcessName, pid.ToString() });
                listView1.Items.Add(item);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}

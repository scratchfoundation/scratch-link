using System;
using System.Windows.Forms;

namespace scratch_link
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var appContext = new App();
            Application.Run(appContext);
        }
    }
}

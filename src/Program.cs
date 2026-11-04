using System;
using System.Threading;
using System.Windows.Forms;
using MetriX.Services;
using MetriX.UI;

namespace MetriX
{
    static class Program
    {
        private const string AppMutexName = "MetriX_SingleInstance_Mutex_9921";

        [STAThread]
        static void Main(string[] args)
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, AppMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("metriX is already running in your taskbar / system tray.", "metriX",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                try
                {
                    var configService = new ConfigService();
                    var networkMonitor = new NetworkMonitor();
                    var taskbarTracker = new TaskbarTracker();

                    var trayContext = new TrayApplicationContext(configService, networkMonitor, taskbarTracker);
                    Application.Run(trayContext);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error initializing metriX:\n" + ex.Message, "metriX Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}

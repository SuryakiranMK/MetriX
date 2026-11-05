using System;
using System.IO;
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

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                Application.ThreadException += (s, e) =>
                {
                    try { File.WriteAllText(Path.Combine(baseDir, "metrix_crash.log"), e.Exception.ToString()); } catch { }
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    try { File.WriteAllText(Path.Combine(baseDir, "metrix_crash.log"), e.ExceptionObject.ToString()); } catch { }
                };

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
                    try { File.AppendAllText(Path.Combine(baseDir, "debug.log"), "Program Main Exception: " + ex + "\n"); } catch { }
                    MessageBox.Show("Error initializing metriX:\n" + ex.Message, "metriX Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}

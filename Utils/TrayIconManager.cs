using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;

namespace TubePulse.Utils
{
    [SupportedOSPlatform("windows")]
    public class TrayIconManager : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private Thread? _uiThread;
        private readonly CancellationTokenSource _cts = new();

        public event EventHandler? ExitRequested;

        public void Start()
        {
            _uiThread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion ?? "unknown";

                _notifyIcon = new NotifyIcon
                {
                    Text = $"TubePulse v{version}",
                    Visible = true
                };

                var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    _notifyIcon.Icon = SystemIcons.Application;
                }

                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("Exit", null, (s, e) =>
                {
                    ExitRequested?.Invoke(this, EventArgs.Empty);
                });
                _notifyIcon.ContextMenuStrip = contextMenu;

                Application.Run();
            });

            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.IsBackground = true;
            _uiThread.Start();
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                if (_notifyIcon.ContextMenuStrip?.InvokeRequired == true)
                {
                    _notifyIcon.ContextMenuStrip.Invoke(() =>
                    {
                        _notifyIcon.Visible = false;
                        _notifyIcon.Dispose();
                        Application.ExitThread();
                    });
                }
                else
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    Application.ExitThread();
                }
            }

            _cts.Dispose();
        }
    }
}

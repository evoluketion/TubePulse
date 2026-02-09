using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TubePulse
{
    public class Program
    {
#if WINDOWS_BUILD
        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();
#endif

        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var headless = config.GetValue<bool>("TubePulse:Headless");
            IDisposable? trayIcon = null;

#if WINDOWS_BUILD
            if (headless)
            {
                FreeConsole();
                trayIcon = WindowsHeadlessHelper.Start();
            }
#endif

            var host = CreateHostBuilder(args).Build();

#if WINDOWS_BUILD
            if (trayIcon is Utils.TrayIconManager tray)
            {
                tray.ExitRequested += (s, e) =>
                {
                    host.StopAsync().GetAwaiter().GetResult();
                };
                tray.Start();
            }
#endif

            host.Run();
            trayIcon?.Dispose();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<TubePulseSettings>(hostContext.Configuration.GetSection("TubePulse"));
                    services.AddHostedService<Worker>();
                });
    }
}
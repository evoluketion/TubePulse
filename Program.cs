using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TubePulse
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService() // Enables Windows service support
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<TubePulseSettings>(hostContext.Configuration.GetSection("TubePulse"));
                    services.AddHostedService<Worker>();
                });
    }
}
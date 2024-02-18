using worker_notification;
using Coravel;
using Oracle.ManagedDataAccess.Client;
using Gamidas.Utils;

HostBuilder builder = new();
builder.ConfigureAppConfiguration((hostingContext, config) =>
{
	config.SetBasePath("/app/config");
	config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
});

IHost host = builder.ConfigureServices((hostContext, services) =>
{
	services.ConfigureGamidas();
	services.AddTransient(x => new OracleConnection(hostContext.Configuration.GetConnectionString("Default")));
	services.AddScheduler();
	services.AddTransient<EmailNotification>();
})
	.Build();

host.Services.UseScheduler(scheduler =>
{
	scheduler.Schedule<EmailNotification>().DailyAt(9, 0);
});

host.Run();

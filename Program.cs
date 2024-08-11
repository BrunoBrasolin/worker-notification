using worker_notification;
using Coravel;
using Oracle.ManagedDataAccess.Client;
using Gamidas.Utils;
using Serilog;
using Serilog.Events;

HostBuilder builder = new();
builder.ConfigureAppConfiguration((hostingContext, config) =>
{
	config.SetBasePath("/app/config");
	config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
});

string connectionString = "";

IHost host = builder.ConfigureServices((hostContext, services) =>
{
	connectionString = hostContext.Configuration.GetConnectionString("Default");
	services.ConfigureGamidas();
	services.AddTransient(x => new OracleConnection(connectionString));
	services.AddSerilog();
	services.AddScheduler();
	services.AddTransient<EmailNotification>();
}).Build();

Log.Logger = new LoggerConfiguration()
	.Enrich.WithProperty("ApplicationName", "WORKER-Notification")
	.MinimumLevel.Warning()
	.MinimumLevel.Override("worker_notification", LogEventLevel.Information)
	.WriteTo.Oracle(cfg => cfg.WithSettings(connectionString)
		.UseBurstBatch()
		.CreateSink())
	.CreateLogger();

host.Services.UseScheduler(scheduler =>
{
	scheduler.Schedule<EmailNotification>().DailyAt(9, 0);
});

host.Run();

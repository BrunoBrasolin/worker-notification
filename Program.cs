using worker_notification;
using Coravel;
using Oracle.ManagedDataAccess.Client;
using Gamidas.Utils;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.ConfigureGamidas();
        services.AddTransient(x => new OracleConnection(hostContext.Configuration.GetConnectionString("Default")));
        services.AddScheduler();
        services.AddTransient<EmailNotification>();
    })
    .Build();

host.Services.UseScheduler(scheduler => {
    scheduler.Schedule<EmailNotification>().DailyAt(9, 0);
});

host.Run();

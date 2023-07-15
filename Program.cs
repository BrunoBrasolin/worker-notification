using worker_notification;
using MySqlConnector;
using Coravel;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddTransient(x => new MySqlConnection(hostContext.Configuration.GetConnectionString("Default")));
        services.AddScheduler();
        services.AddTransient<EmailNotification>();
    })
    .Build();

host.Services.UseScheduler(scheduler => {
    scheduler.Schedule<EmailNotification>().DailyAt(8, 0);
});

host.Run();

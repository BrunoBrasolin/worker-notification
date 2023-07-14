using MySqlConnector;
using worker_notification;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHostedService<Worker>();
        services.AddTransient(x => new MySqlConnection(hostContext.Configuration.GetConnectionString("Default")));
    })
    .Build();

host.Run();

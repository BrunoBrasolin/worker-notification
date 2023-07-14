using Dapper;
using MySqlConnector;
using System.Net;
using System.Net.Mail;

namespace worker_notification
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly MySqlConnection _connection;
        private readonly IConfiguration _configuration;

        public Worker(ILogger<Worker> logger, MySqlConnection connection, IConfiguration configuration)
        {
            _logger = logger;
            _connection = connection;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (DateTime.Now.Hour == 8 && DateTime.Now.Minute == 00)
                {
                    SmtpConfiguration smtpConfiguration = new(_configuration.GetValue<string>("SmtpConfiguration:Server"), _configuration.GetValue<int>("SmtpConfiguration:Port"), _configuration.GetValue<string>("SmtpConfiguration:Username"), _configuration.GetValue<string>("SmtpConfiguration:AppPassword"));

                    IEnumerable<NotificationSettingMapper> notifications = _connection.Query<NotificationSettingMapper>("SELECT * FROM NOTIFICATIONS_SETTINGS WHERE DUE_DATE > SYSDATE();");

                    SmtpClient smtpClient = new(smtpConfiguration.Server, smtpConfiguration.Port)
                    {
                        EnableSsl = true,
                        Credentials = new NetworkCredential(smtpConfiguration.Username, smtpConfiguration.AppPassword)
                    };


                    foreach (NotificationSettingMapper notification in notifications)
                    {
                        MailMessage message = new(smtpConfiguration.Username, notification.RECIPIENT)
                        {
                            Subject = notification.SUBJECT,
                            Body = notification.BODY + $"\r\n\r\nData final: {notification.DUE_DATE.ToString("dd/MM/yyyy")}"
                        };

                        try
                        {
                            smtpClient.Send(message);

                            _logger.LogInformation($"Email {notification.ID} sent successfully.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error while sending email: {ex.Message}");
                        }

                    }
                }

                await Task.Delay(60000, stoppingToken);
            }
        }
    }

    class SmtpConfiguration
    {
        public string Server;
        public int Port;
        public string Username;
        public string AppPassword;

        public SmtpConfiguration(string server, int port, string username, string appPassword)
        {
            this.Server = server;
            this.Port = port;
            this.Username = username;
            this.AppPassword = appPassword;
        }
    }

    class NotificationSettingMapper
    {
        public int ID { get; set; }
        public string RECIPIENT { get; set; }
        public string SUBJECT { get; set; }
        public string BODY { get; set; }
        public DateTime DUE_DATE { get; set; }
    }
}
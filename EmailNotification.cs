using Coravel.Invocable;
using System.Net.Mail;
using System.Net;
using MySqlConnector;
using Dapper;

namespace worker_notification;
public class EmailNotification : IInvocable
{
    private readonly ILogger<EmailNotification> _logger;
    private readonly MySqlConnection _connection;
    private readonly IConfiguration _configuration;

    public EmailNotification(ILogger<EmailNotification> logger, MySqlConnection connection, IConfiguration configuration)
    {
        _logger = logger;
        _connection = connection;
        _configuration = configuration;
    }

    public Task Invoke()
    {
        DateTime dateTimeNow = DateTime.UtcNow.AddHours(-3);

        SmtpConfiguration smtpConfiguration = new(_configuration.GetValue<string>("SmtpConfiguration:Server"), _configuration.GetValue<int>("SmtpConfiguration:Port"), _configuration.GetValue<string>("SmtpConfiguration:Username"), _configuration.GetValue<string>("SmtpConfiguration:AppPassword"));

        IEnumerable<NotificationSettingMapper> notifications = _connection.Query<NotificationSettingMapper>("SELECT * FROM NOTIFICATIONS_SETTINGS;");

        SmtpClient smtpClient = new(smtpConfiguration.Server, smtpConfiguration.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(smtpConfiguration.Username, smtpConfiguration.AppPassword)
        };

        foreach (NotificationSettingMapper notification in notifications)
        {
            if (dateTimeNow > notification.DUE_DATE)
            {
                _connection.Execute("DELETE FROM NOTIFICATIONS_SETTINGS WHERE ID = @id", new { id = notification.ID });

                continue;
            }

            decimal yearsLeft = (notification.DUE_DATE - dateTimeNow).Days / 365;
            decimal daysLeft = (notification.DUE_DATE - dateTimeNow).Days;
            decimal hoursLeft = (notification.DUE_DATE - dateTimeNow).Hours;

            MailMessage message = new(smtpConfiguration.Username, notification.RECIPIENT)
            {
                Subject = $"Worker Notification | {notification.SUBJECT}",
                Body = notification.BODY
                + $"\r\n\r\n--------------------------------------------------------------"
                + $"\r\n\r\nRestam: {yearsLeft} anos, {daysLeft} dias e {hoursLeft} horas."
                + $"\r\n\r\nData final: {notification.DUE_DATE:dd/MM/yyyy}"
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

        return Task.CompletedTask;
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

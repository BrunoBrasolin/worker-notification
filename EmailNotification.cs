using Coravel.Invocable;
using Dapper;
using Oracle.ManagedDataAccess.Client;
using Gamidas.Utils.RabbitMQ.Model;
using Gamidas.Utils.RabbitMQ.Send;

namespace worker_notification;
public class EmailNotification : IInvocable
{
    private readonly ILogger<EmailNotification> _logger;
    private readonly OracleConnection _connection;
    private readonly ISendEvent _sendEvent;

    public EmailNotification(ILogger<EmailNotification> logger, OracleConnection connection, ISendEvent sendEvent)
    {
        _logger = logger;
        _connection = connection;
        _sendEvent = sendEvent;
    }

    public Task Invoke()
    {
        DateTime dateTimeNow = DateTime.UtcNow.AddHours(-3);

        IEnumerable<NotificationSettingMapper> notifications = _connection.Query<NotificationSettingMapper>("SELECT * FROM NOTIFICATIONS_SETTINGS");

        _logger.LogInformation($"Found {notifications.Count()} e-mails to send.");

        foreach (NotificationSettingMapper notification in notifications)
        {
            _logger.LogInformation($"Starting {notification.ID}.");

            if (dateTimeNow > notification.DUE_DATE)
            {
                _connection.Execute("DELETE FROM NOTIFICATIONS_SETTINGS WHERE ID = :id", new { id = notification.ID });

                _logger.LogInformation($"Notification {notification.ID} deleted successfully.");

                continue;
            }

            if ((notification.DUE_DATE - dateTimeNow).TotalDays > 30)
            {
                _logger.LogInformation($"Notification {notification.ID} does not passed the validation.");
                continue;
            }

            decimal yearsLeft = (notification.DUE_DATE - dateTimeNow).Days / 365;
            decimal daysLeft = (notification.DUE_DATE - dateTimeNow).Days;
            decimal hoursLeft = (notification.DUE_DATE - dateTimeNow).Hours;

            string subject = $"Worker Notification | {notification.SUBJECT}";
            string body = notification.BODY
                + $"\r\n\r\n--------------------------------------------------------------"
                + $"\r\n\r\nRestam: {yearsLeft} anos, {daysLeft} dias e {hoursLeft} horas."
                + $"\r\n\r\nData final: {notification.DUE_DATE:dd/MM/yyyy}";

            EmailModel email = new(notification.RECIPIENT, body, subject);

            _logger.LogInformation($"Email model created: {subject}.");

            try
            {
                _sendEvent.SendEmail(email);
                _logger.LogInformation($"E-mail {notification.ID} published successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while publishing email: {ex.Message}");
            }
        }

        return Task.CompletedTask;
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

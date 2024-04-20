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

        _logger.LogInformation("Found {Count} e-mails to send.", notifications.Count());

        foreach (NotificationSettingMapper notification in notifications)
        {
            _logger.LogInformation("Starting {NotificationId}.", notification.ID);

            if (dateTimeNow > notification.DUE_DATE)
            {
                _connection.Execute("DELETE FROM NOTIFICATIONS_SETTINGS WHERE ID = :id", new { id = notification.ID });

                _logger.LogInformation("Notification {NotificationId} deleted successfully.", notification.ID);

                continue;
            }

            TimeSpan timeSpan = notification.DUE_DATE - dateTimeNow;
            if (timeSpan.TotalDays > 30)
            {
                _logger.LogInformation("Notification {NotificationId} does not passed the validation.", notification.ID);
                continue;
            }

            decimal yearsLeft = (decimal)timeSpan.Days / 365;
            decimal daysLeft = timeSpan.Days;
            decimal hoursLeft = timeSpan.Hours;

            string subject = $"Worker Notification | {notification.SUBJECT}";
            string body = notification.BODY
                + $"\r\n\r\n--------------------------------------------------------------"
                + $"\r\n\r\nRestam: {yearsLeft} anos, {daysLeft} dias e {hoursLeft} horas."
                + $"\r\n\r\nData final: {notification.DUE_DATE:dd/MM/yyyy}";

            EmailModel email = new(notification.RECIPIENT, body, subject);

            _logger.LogInformation("Email model created: {Subject}.", subject);

            try
            {
                _sendEvent.SendEmail(email);
                _logger.LogInformation("E-mail {NotificationId} published successfully.", notification.ID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing email: {ExceptionMessage}", ex.Message);
            }
        }

        return Task.CompletedTask;
    }

    class NotificationSettingMapper
    {
        public int ID { get; } = 0;
        public string RECIPIENT { get; } = string.Empty;
        public string SUBJECT { get; } = string.Empty;
        public string BODY { get; } = string.Empty;
        public DateTime DUE_DATE { get; } = DateTime.MinValue;
    }
}

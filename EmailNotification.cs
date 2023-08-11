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

        foreach (NotificationSettingMapper notification in notifications)
        {
            if (dateTimeNow > notification.DUE_DATE)
            {
                _connection.Execute("DELETE FROM NOTIFICATIONS_SETTINGS WHERE ID = :id", new { id = notification.ID });

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

            try
            {
                _sendEvent.SendEmail(email);
                _logger.LogInformation($"Email {notification.ID} send successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while sending email: {ex.Message}");
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

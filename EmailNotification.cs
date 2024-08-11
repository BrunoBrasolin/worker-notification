using Coravel.Invocable;
using Dapper;
using Oracle.ManagedDataAccess.Client;
using Gamidas.Utils.RabbitMQ.Model;
using Gamidas.Utils.RabbitMQ.Send;

namespace worker_notification;
public class EmailNotification(ILogger<EmailNotification> logger, OracleConnection connection, ISendEvent sendEvent) : IInvocable
{
	private readonly ILogger<EmailNotification> _logger = logger;
	private readonly OracleConnection _connection = connection;
	private readonly ISendEvent _sendEvent = sendEvent;

	public Task Invoke()
	{
		DateTime dateTimeNow = DateTime.UtcNow.AddHours(-3);

		IEnumerable<NotificationSettingMapper> notifications = _connection.Query<NotificationSettingMapper>("SELECT * FROM NOTIFICATIONS_SETTINGS");

		_logger.LogInformation("Found {notifications} e-mails to send.", notifications.Count());

		foreach (NotificationSettingMapper notification in notifications)
		{
			_logger.LogInformation("Starting {notification}.", notification.ID);

			if (dateTimeNow > notification.DUE_DATE)
			{
				_connection.Execute("DELETE FROM NOTIFICATIONS_SETTINGS WHERE ID = :id", new { id = notification.ID });

				_logger.LogInformation("Notification {notification} deleted successfully.", notification.ID);

				continue;
			}

			if ((notification.DUE_DATE - dateTimeNow).TotalDays > 30)
			{
				_logger.LogInformation("Notification {notification} does not passed the validation.", notification.ID);
				continue;
			}

			decimal daysLeft = (notification.DUE_DATE - dateTimeNow).Days;
			decimal hoursLeft = (notification.DUE_DATE - dateTimeNow).Hours;

			string subject = $"Worker Notification | {notification.SUBJECT}";
			string body = notification.BODY
				+ $"\r\n\r\n--------------------------------------------------------------"
				+ $"\r\n\r\nRestam: {daysLeft} dias e {hoursLeft} horas."
				+ $"\r\n\r\nData final: {notification.DUE_DATE:dd/MM/yyyy}";

			EmailModel email = new(notification.RECIPIENT, body, subject);

			_logger.LogInformation($"Email model created: {subject}.");

			try
			{
				_sendEvent.SendEmail(email);
				_logger.LogInformation("E-mail {notification} published successfully.", notification.ID);
			}
			catch (Exception ex)
			{
				_logger.LogError("Error while publishing email: {message}", ex.Message);
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

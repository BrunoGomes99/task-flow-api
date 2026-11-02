using Microsoft.Extensions.Logging;
using TaskFlow.Application.Enums;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.UseCases.Reminders;
using TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;
using TaskFlow.Application.UseCases.Reminders.PublishDueSoonReminders;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using DomainTask = TaskFlow.Domain.Entities.Task;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.Tests.Reminders;

public sealed class DueSoonReminderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async System.Threading.Tasks.Task Scanner_PublishesTasksInWindow_AndSkipsExistingReceipt()
    {
        var due = Now.UtcDateTime.AddHours(23).AddMinutes(30);
        var fresh = new DomainTask(Guid.NewGuid(), "Estudar", "x", DomainTaskStatus.Pending, due);
        var sent = new DomainTask(Guid.NewGuid(), "Já avisada", "x", DomainTaskStatus.Pending, due);
        var query = new FakeTaskReminderQuery([fresh, sent]);
        var logs = new FakeNotificationLogRepository();
        logs.Seed(sent.Id, due);
        var publisher = new FakeDueSoonPublisher();
        var scanner = new DueSoonReminderPublish(query, logs, publisher, new FixedTimeProvider(Now));

        var summary = await scanner.PublishAsync(CancellationToken.None);

        Assert.Equal(Now.UtcDateTime.AddHours(23), query.WindowStart);
        Assert.Equal(Now.UtcDateTime.AddHours(24), query.WindowEnd);
        Assert.Equal(1, summary.Published);
        Assert.Equal(1, summary.Skipped);
        var published = Assert.Single(publisher.Messages);
        Assert.Equal(fresh.Id, published.TaskId);
        Assert.Equal(fresh.UserId, published.UserId);
        Assert.Equal(DueInstant.NormalizeUtc(due), published.DueDate);
    }

    [Fact]
    public void Email_UsesPortugueseSubjectAndUtcDueInstant()
    {
        var due = new DateTime(2026, 10, 17, 18, 30, 0, DateTimeKind.Utc);

        var email = DueReminderEmail.Compose("ana@example.com", "Relatório", due);

        Assert.Equal("ana@example.com", email.To);
        Assert.Equal("Lembrete: Relatório vence em cerca de 24 horas", email.Subject);
        Assert.Equal("A tarefa \"Relatório\" vence em 2026-10-17 18:30 UTC.", email.Body);
    }

    [Fact]
    public async System.Threading.Tasks.Task Delivery_Acknowledges_WhenReceiptAlreadyExists()
    {
        var (message, _) = OwnedTask(DueInWindow());
        var logs = new FakeNotificationLogRepository();
        logs.Seed(message.TaskId, message.DueDate);
        var email = new FakeEmailSender();
        var logger = new RecordingLogger<DueSoonReminderDelivery>();
        var delivery = Delivery(new FakeTaskReminderQuery([]), logs, email, new FakeUserRepository(), logger);

        var result = await delivery.DeliverAsync(message, CancellationToken.None);

        Assert.Equal(DueSoonDeliveryOutcome.Acknowledge, result.Outcome);
        Assert.Equal(DueSoonDeliveryReasons.AlreadySent, result.Reason);
        AssertDeliveryLog(logger, message, DueSoonDeliveryReasons.AlreadySent);
        Assert.Empty(email.Sent);
        Assert.Equal(0, logs.Inserts);
    }

    [Fact]
    public async System.Threading.Tasks.Task Delivery_AcknowledgesWithoutMail_WhenTaskMissingCompletedOrDueDateChanged()
    {
        var due = DueInWindow();
        var (missingMessage, _) = OwnedTask(due);
        var (completedMessage, completed) = OwnedTask(due, DomainTaskStatus.Completed);
        var (movedMessage, moved) = OwnedTask(due);
        var email = new FakeEmailSender();
        var logs = new FakeNotificationLogRepository();
        var logger = new RecordingLogger<DueSoonReminderDelivery>();
        var movedMessageWithNewDue = movedMessage with { DueDate = due.AddHours(5) };

        var missingOutcome = await Delivery(
            new FakeTaskReminderQuery([]),
            logs,
            email,
            new FakeUserRepository(),
            logger).DeliverAsync(missingMessage, CancellationToken.None);

        var completedOutcome = await Delivery(
            new FakeTaskReminderQuery([], completed),
            logs,
            email,
            new FakeUserRepository(),
            logger).DeliverAsync(completedMessage, CancellationToken.None);

        var movedOutcome = await Delivery(
            new FakeTaskReminderQuery([], moved),
            logs,
            email,
            new FakeUserRepository(),
            logger).DeliverAsync(movedMessageWithNewDue, CancellationToken.None);

        Assert.Equal(DueSoonDeliveryOutcome.Acknowledge, missingOutcome.Outcome);
        Assert.Equal(DueSoonDeliveryReasons.Skipped, missingOutcome.Reason);
        Assert.Equal(DueSoonDeliveryOutcome.Acknowledge, completedOutcome.Outcome);
        Assert.Equal(DueSoonDeliveryReasons.Skipped, completedOutcome.Reason);
        Assert.Equal(DueSoonDeliveryOutcome.Acknowledge, movedOutcome.Outcome);
        Assert.Equal(DueSoonDeliveryReasons.Skipped, movedOutcome.Reason);
        AssertDeliveryLogs(
            logger,
            (missingMessage, DueSoonDeliveryReasons.Skipped),
            (completedMessage, DueSoonDeliveryReasons.Skipped),
            (movedMessageWithNewDue, DueSoonDeliveryReasons.Skipped));
        Assert.Empty(email.Sent);
        Assert.Equal(0, logs.Inserts);
    }

    [Fact]
    public async System.Threading.Tasks.Task Delivery_Requeues_WhenOwnerMissingOrEmailRejected()
    {
        var user = new User("Ana", "ana@example.com", "hashhash");
        var (message, task) = OwnedTask(DueInWindow(), ownerId: user.Id);
        var email = new FakeEmailSender { Accepts = false };
        var logs = new FakeNotificationLogRepository();
        var users = new FakeUserRepository();
        users.Seed(user);
        var logger = new RecordingLogger<DueSoonReminderDelivery>();

        var missingOwner = await Delivery(
            new FakeTaskReminderQuery([], task),
            logs,
            email,
            new FakeUserRepository(),
            logger).DeliverAsync(message, CancellationToken.None);

        var rejected = await Delivery(
            new FakeTaskReminderQuery([], task),
            logs,
            email,
            users,
            logger).DeliverAsync(message, CancellationToken.None);

        Assert.Equal(DueSoonDeliveryOutcome.Requeue, missingOwner.Outcome);
        Assert.Equal(DueSoonDeliveryReasons.Requeue, missingOwner.Reason);
        Assert.Equal(DueSoonDeliveryOutcome.Requeue, rejected.Outcome);
        Assert.Equal(DueSoonDeliveryReasons.Requeue, rejected.Reason);
        AssertDeliveryLogs(
            logger,
            (message, DueSoonDeliveryReasons.Requeue),
            (message, DueSoonDeliveryReasons.Requeue));
        Assert.DoesNotContain(user.Email.Value, string.Join('\n', logger.Information.Select(entry => entry.Message)), StringComparison.Ordinal);
        Assert.Single(email.Sent);
        Assert.Equal(0, logs.Inserts);
    }

    [Fact]
    public async System.Threading.Tasks.Task Delivery_SendsThenStoresReceipt_AndAcknowledges()
    {
        var user = new User("Ana", "ana@example.com", "hashhash");
        var (message, task) = OwnedTask(DueInWindow(), title: "Prazo", ownerId: user.Id);
        var users = new FakeUserRepository();
        users.Seed(user);
        var email = new FakeEmailSender();
        var logs = new FakeNotificationLogRepository();
        var logger = new RecordingLogger<DueSoonReminderDelivery>();

        var outcome = await Delivery(
            new FakeTaskReminderQuery([], task),
            logs,
            email,
            users,
            logger).DeliverAsync(message, CancellationToken.None);

        Assert.Equal(DueSoonDeliveryOutcome.Acknowledge, outcome.Outcome);
        Assert.Equal(DueSoonDeliveryReasons.Sent, outcome.Reason);
        AssertDeliveryLog(logger, message, DueSoonDeliveryReasons.Sent);
        Assert.DoesNotContain(user.Email.Value, logger.Information[0].Message, StringComparison.Ordinal);
        var sent = Assert.Single(email.Sent);
        Assert.Equal("ana@example.com", sent.To);
        Assert.Contains(task.Title, sent.Subject, StringComparison.Ordinal);
        Assert.Equal(1, logs.Inserts);
        Assert.Equal(task.Id, logs.LastInserted!.TaskId);
        Assert.Equal(NotificationEventType.DueSoon, logs.LastInserted.EventType);
        Assert.Equal(DueInstant.NormalizeUtc(message.DueDate), logs.LastInserted.DueDate);
    }

    private static DateTime DueInWindow() => Now.UtcDateTime.AddHours(23).AddMinutes(10);

    private static (DueSoonMessage Message, DomainTask Task) OwnedTask(
        DateTime due,
        DomainTaskStatus status = DomainTaskStatus.Pending,
        string title = "Prazo",
        Guid? ownerId = null)
    {
        var task = new DomainTask(ownerId ?? Guid.NewGuid(), title, "x", DomainTaskStatus.Pending, due);
        if (status == DomainTaskStatus.Completed)
            Assert.True(task.TryChangeStatusTo(DomainTaskStatus.Completed, out _));

        return (new DueSoonMessage(task.Id, task.UserId, due), task);
    }

    private static void AssertDeliveryLog(
        RecordingLogger<DueSoonReminderDelivery> logger,
        DueSoonMessage message,
        string reason)
    {
        AssertDeliveryLogs(logger, (message, reason));
    }

    private static void AssertDeliveryLogs(
        RecordingLogger<DueSoonReminderDelivery> logger,
        params (DueSoonMessage Message, string Reason)[] expected)
    {
        Assert.Equal(expected.Length, logger.Information.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            var logged = logger.Information[i];
            Assert.Equal(expected[i].Message.TaskId, logged.Values["TaskId"]);
            Assert.Equal(DueInstant.NormalizeUtc(expected[i].Message.DueDate), logged.Values["DueDate"]);
            Assert.Equal(expected[i].Reason, logged.Values["Result"]);
            Assert.DoesNotContain("@", logged.Message, StringComparison.Ordinal);
        }
    }

    private static DueSoonReminderDelivery Delivery(
        FakeTaskReminderQuery tasks,
        FakeNotificationLogRepository logs,
        FakeEmailSender email,
        FakeUserRepository users,
        RecordingLogger<DueSoonReminderDelivery> logger) =>
        new(tasks, logs, users, email, logger);

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utc;

        public FixedTimeProvider(DateTimeOffset utc) => _utc = utc;

        public override DateTimeOffset GetUtcNow() => _utc;
    }

    private sealed class FakeTaskReminderQuery : ITaskReminderRepository
    {
        private readonly IReadOnlyList<DomainTask> _tasks;
        private readonly DomainTask? _task;

        public FakeTaskReminderQuery(IReadOnlyList<DomainTask> tasks, DomainTask? task = null)
        {
            _tasks = tasks;
            _task = task;
        }

        public DateTime WindowStart { get; private set; }
        public DateTime WindowEnd { get; private set; }

        public System.Threading.Tasks.Task<IReadOnlyList<DomainTask>> ListInWindowAsync(
            DateTime windowStartInclusiveUtc,
            DateTime windowEndExclusiveUtc,
            CancellationToken cancellationToken = default)
        {
            WindowStart = windowStartInclusiveUtc;
            WindowEnd = windowEndExclusiveUtc;
            return System.Threading.Tasks.Task.FromResult(_tasks);
        }

        public System.Threading.Tasks.Task<DomainTask?> FindByIdAsync(Guid taskId, CancellationToken cancellationToken = default) =>
            System.Threading.Tasks.Task.FromResult(_task is not null && _task.Id == taskId ? _task : null);
    }

    private sealed class FakeNotificationLogRepository : INotificationLogRepository
    {
        private readonly HashSet<(Guid TaskId, DateTime Due)> _existing = [];

        public int Inserts { get; private set; }
        public NotificationLog? LastInserted { get; private set; }

        public void Seed(Guid taskId, DateTime due) => _existing.Add((taskId, DueInstant.NormalizeUtc(due)));

        public System.Threading.Tasks.Task<bool> ExistsAsync(
            Guid taskId,
            NotificationEventType eventType,
            DateTime dueDate,
            CancellationToken cancellationToken = default) =>
            System.Threading.Tasks.Task.FromResult(
                eventType == NotificationEventType.DueSoon &&
                _existing.Contains((taskId, DueInstant.NormalizeUtc(dueDate))));

        public System.Threading.Tasks.Task InsertAsync(
            NotificationLog log,
            CancellationToken cancellationToken = default)
        {
            Inserts++;
            LastInserted = log;
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }

    private sealed class FakeDueSoonPublisher : IDueSoonPublisher
    {
        public List<DueSoonMessage> Messages { get; } = [];

        public System.Threading.Tasks.Task PublishAsync(DueSoonMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public bool Accepts { get; set; } = true;
        public List<DueReminderEmail> Sent { get; } = [];

        public System.Threading.Tasks.Task<bool> SendAsync(DueReminderEmail email, CancellationToken cancellationToken = default)
        {
            Sent.Add(email);
            return System.Threading.Tasks.Task.FromResult(Accepts);
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private User? _user;

        public void Seed(User user) => _user = user;

        public System.Threading.Tasks.Task AddAsync(User user, CancellationToken cancellationToken = default) =>
            System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            System.Threading.Tasks.Task.FromResult(_user is not null && _user.Id == userId ? _user : null);

        public System.Threading.Tasks.Task<User?> GetByEmailAsync(Domain.ValueObjects.Email email, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public System.Threading.Tasks.Task<bool> ExistsByEmailAsync(Domain.ValueObjects.Email email, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<CapturedLog> Information { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel != LogLevel.Information || state is not System.Collections.IEnumerable items)
                return;

            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item is KeyValuePair<string, object> pair && pair.Key != "{OriginalFormat}")
                    values[pair.Key] = pair.Value;
            }

            Information.Add(new CapturedLog(formatter(state, exception), values));
        }

        public readonly record struct CapturedLog(string Message, IReadOnlyDictionary<string, object?> Values);
    }
}

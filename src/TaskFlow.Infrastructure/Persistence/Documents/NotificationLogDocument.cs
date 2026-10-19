using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Infrastructure.Persistence.Documents;

public sealed class NotificationLogDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid TaskId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public NotificationEventType EventType { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime ProcessedAt { get; set; }
}

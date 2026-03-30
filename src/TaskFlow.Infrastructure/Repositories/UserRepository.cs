using MongoDB.Driver;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Persistence.Documents;
using TaskFlow.Infrastructure.Persistence.Mappers;

namespace TaskFlow.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IMongoCollection<UserDocument> _users;

    public UserRepository(TaskFlowMongoContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _users = context.Users;
    }

    public async System.Threading.Tasks.Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _users.InsertOneAsync(UserDocumentMapper.ToDocument(user), cancellationToken: cancellationToken);
    }

    public async System.Threading.Tasks.Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var document = await _users.Find(x => x.Id == userId).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : UserDocumentMapper.ToDomain(document);
    }

    public async System.Threading.Tasks.Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        var document = await _users.Find(x => x.Email == email.Value).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : UserDocumentMapper.ToDomain(document);
    }

    public async System.Threading.Tasks.Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        var count = await _users.CountDocumentsAsync(x => x.Email == email.Value, cancellationToken: cancellationToken);
        return count > 0;
    }
}

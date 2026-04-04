using EncorelyApplication.Interfaces;
using EncorelyDomain.Entities;
using EncorelyInfrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EncorelyApplication.Services;

public class UserService : IUserService
{
    private readonly EncorelyDbContext _dbContext;

    public UserService(EncorelyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) throw new KeyNotFoundException("User not found");
        return user;
    }

    public async Task UpdateSettingsAsync(Guid userId, ConcertMood mood, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, ct);
        if (user == null) throw new KeyNotFoundException("User not found");

        user.Mood = mood;
        await _dbContext.SaveChangesAsync(ct);
    }
}

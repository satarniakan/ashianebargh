using Microsoft.EntityFrameworkCore;
using Ashianebargh.Domain.Entities;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure.Data;

namespace Ashianebargh.Infrastructure.Repositories;

public class OtpThrottleRepository : IOtpThrottleRepository
{
    private readonly AshianebarghDbContext _context;

    public OtpThrottleRepository(AshianebarghDbContext context) => _context = context;

    public Task<OtpThrottle?> GetByPhoneAsync(string phoneNumber) =>
        _context.OtpThrottles.FirstOrDefaultAsync(t => t.PhoneNumber == phoneNumber);

    public async Task AddAsync(OtpThrottle throttle)
    {
        await _context.OtpThrottles.AddAsync(throttle);
    }

    public Task<int> DeleteStaleAsync(DateTime cutoffUtc) =>
        _context.OtpThrottles
            .Where(t => (t.FailedWindowStartUtc == null || t.FailedWindowStartUtc < cutoffUtc)
                     && (t.GenerationWindowStartUtc == null || t.GenerationWindowStartUtc < cutoffUtc))
            .ExecuteDeleteAsync();
}

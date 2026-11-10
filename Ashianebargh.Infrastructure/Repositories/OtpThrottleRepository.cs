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

    public async Task<OtpThrottle> RegisterAttemptAsync(
        string phoneNumber, bool generation, DateTime nowUtc, TimeSpan window)
    {
        var cutoff = nowUtc - window;
        await EnsureRowAsync(phoneNumber);

        if (generation)
        {
            var reset = await _context.OtpThrottles
                .Where(t => t.PhoneNumber == phoneNumber
                         && (t.GenerationWindowStartUtc == null || t.GenerationWindowStartUtc < cutoff))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.GenerationCount, 1)
                    .SetProperty(t => t.GenerationWindowStartUtc, nowUtc));

            if (reset == 0)
            {
                await _context.OtpThrottles
                    .Where(t => t.PhoneNumber == phoneNumber)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.GenerationCount, t => t.GenerationCount + 1));
            }
        }
        else
        {
            var reset = await _context.OtpThrottles
                .Where(t => t.PhoneNumber == phoneNumber
                         && (t.FailedWindowStartUtc == null || t.FailedWindowStartUtc < cutoff))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.FailedCount, 1)
                    .SetProperty(t => t.FailedWindowStartUtc, nowUtc));

            if (reset == 0)
            {
                await _context.OtpThrottles
                    .Where(t => t.PhoneNumber == phoneNumber)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.FailedCount, t => t.FailedCount + 1));
            }
        }

        return await _context.OtpThrottles
            .AsNoTracking()
            .SingleAsync(t => t.PhoneNumber == phoneNumber);
    }

    public Task ClearFailedAttemptsAsync(string phoneNumber) =>
        _context.OtpThrottles
            .Where(t => t.PhoneNumber == phoneNumber)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.FailedCount, 0)
                .SetProperty(t => t.FailedWindowStartUtc, t => (DateTime?)null));

    /// <summary>
    /// درج رکورد اولیه. دو درخواست همزمان برای شمارهٔ بدون رکورد ممکن است هر دو
    /// «وجود ندارد» ببینند؛ بازندهٔ درج نادیده گرفته می‌شود چون برنده رکورد را ساخته.
    /// </summary>
    private async Task EnsureRowAsync(string phoneNumber)
    {
        if (await _context.OtpThrottles.AnyAsync(t => t.PhoneNumber == phoneNumber))
            return;

        var row = new OtpThrottle { PhoneNumber = phoneNumber };
        await _context.OtpThrottles.AddAsync(row);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _context.Entry(row).State = EntityState.Detached;
            if (!await _context.OtpThrottles.AnyAsync(t => t.PhoneNumber == phoneNumber))
                throw;
        }
    }

    public Task<int> DeleteStaleAsync(DateTime cutoffUtc) =>
        _context.OtpThrottles
            .Where(t => (t.FailedWindowStartUtc == null || t.FailedWindowStartUtc < cutoffUtc)
                     && (t.GenerationWindowStartUtc == null || t.GenerationWindowStartUtc < cutoffUtc))
            .ExecuteDeleteAsync();
}

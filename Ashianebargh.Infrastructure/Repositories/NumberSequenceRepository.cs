using Microsoft.EntityFrameworkCore;
using Ashianebargh.Domain.Entities;
using Ashianebargh.Domain.Interfaces;
using Ashianebargh.Infrastructure.Data;

namespace Ashianebargh.Infrastructure.Repositories;

public class NumberSequenceRepository : INumberSequenceRepository
{
    private readonly AshianebarghDbContext _context;

    public NumberSequenceRepository(AshianebarghDbContext context) => _context = context;

    public Task<NumberSequence?> GetAsync(string name) =>
        _context.NumberSequences.FirstOrDefaultAsync(s => s.Name == name);

    public async Task AddAsync(NumberSequence sequence)
    {
        await _context.NumberSequences.AddAsync(sequence);
    }
}

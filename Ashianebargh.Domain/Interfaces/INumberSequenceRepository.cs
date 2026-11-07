using Ashianebargh.Domain.Entities;

namespace Ashianebargh.Domain.Interfaces;

public interface INumberSequenceRepository
{
    Task<NumberSequence?> GetAsync(string name);

    Task AddAsync(NumberSequence sequence);
}

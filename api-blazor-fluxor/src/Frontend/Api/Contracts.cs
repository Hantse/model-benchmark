using System.Collections.Immutable;
namespace Benchmark.Frontend.Api;
public sealed record WorkItemView(Guid Id, string Title, string Status, int Version);
public interface IWorkItemApi
{
    Task<string> LoginAsync(string email, string password);
    Task<ImmutableArray<WorkItemView>> ListAsync(string token);
    Task<WorkItemView> AddAsync(string token, string title, string status);
    Task<WorkItemView> UpdateAsync(string token, Guid id, string title, string status, int expectedVersion);
    Task DeleteAsync(string token, Guid id, int expectedVersion);
}
public sealed class ApiFailureException(int status, string message) : Exception(message) { public int Status { get; } = status; }

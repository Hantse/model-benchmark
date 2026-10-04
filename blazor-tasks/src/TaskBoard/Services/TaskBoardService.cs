namespace TaskBoard.Services;
// Stub intentionally compiles; behavior is the task for the model.
public sealed class TaskBoardService : ITaskBoardService
{
    public BoardUser? CurrentUser => null;
    public Task<bool> LoginAsync(string email, string password) => Task.FromResult(false);
    public void Logout() { }
    public Task<IReadOnlyList<TaskItem>> ListAsync(TaskFilter filter = TaskFilter.All) => Task.FromResult<IReadOnlyList<TaskItem>>([]);
    public Task<TaskItem> AddAsync(string title) => throw new NotImplementedException();
    public Task<TaskItem> SetCompletedAsync(Guid id, bool completed) => throw new NotImplementedException();
    public Task DeleteAsync(Guid id) => throw new NotImplementedException();
}

namespace TaskBoard.Services;
public sealed record BoardUser(string Email);
public sealed record TaskItem(Guid Id, string Title, bool Completed);
public enum TaskFilter { All, Active, Completed }
public interface ITaskBoardService
{
    BoardUser? CurrentUser { get; }
    Task<bool> LoginAsync(string email, string password);
    void Logout();
    Task<IReadOnlyList<TaskItem>> ListAsync(TaskFilter filter = TaskFilter.All);
    Task<TaskItem> AddAsync(string title);
    Task<TaskItem> SetCompletedAsync(Guid id, bool completed);
    Task DeleteAsync(Guid id);
}

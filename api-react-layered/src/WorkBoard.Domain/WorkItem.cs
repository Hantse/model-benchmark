namespace WorkBoard.Domain;

public enum WorkStatus { Todo, InProgress, Done }
public sealed record WorkItem(Guid Id, string Owner, string Title, WorkStatus Status, int Version);

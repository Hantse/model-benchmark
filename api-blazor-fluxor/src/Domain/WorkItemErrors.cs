namespace Benchmark.Domain;

public sealed class VersionConflictException : Exception
{
    public VersionConflictException() : base("The item changed. Reload it before retrying.") { }
}

public sealed class ItemNotFoundException : Exception
{
    public ItemNotFoundException() : base("Work item not found.") { }
}

namespace Claims.Application;

public interface IAuditWriter
{
    ValueTask AuditClaimAsync(string id, AuditOperation operation);
    ValueTask AuditCoverAsync(string id, AuditOperation operation);
}

public enum AuditOperation
{
    Create,
    Delete
}

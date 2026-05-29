namespace ContourAI.Entities.Workspace;

public enum ChangeSetStatus
{
    Draft            = 0,
    Ready            = 1,
    PartiallyApplied = 2,
    Applied          = 3,
    Rejected         = 4,
    RolledBack       = 5
}

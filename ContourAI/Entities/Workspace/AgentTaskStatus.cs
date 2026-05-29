namespace ContourAI.Entities.Workspace;

public enum AgentTaskStatus
{
    Pending         = 0,
    Running         = 1,
    GeneratingFiles = 2,
    Ready           = 3,
    Applied         = 4,
    Failed          = 5,
    RolledBack      = 6
}

/// <summary>
/// Флаги прав доступа к папке.
/// JSON: "None" / "Read" / "Edit" / "Delete" / "Read, Edit" / "Read, Edit, Delete" и т.д.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Projects;

[Flags]
public enum FolderPermission
{
    None   = 0,
    Read   = 1,
    Edit   = 2,
    Delete = 4
}

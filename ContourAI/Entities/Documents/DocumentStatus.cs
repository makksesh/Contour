/// <summary>
/// Статус документа на сервере.
/// JSON: "Uploaded" / "Pending" / "Processing" / "Indexed" / "Failed".
/// Проект: DevAssistant / ContourAI.
/// </summary>

namespace ContourAI.Entities.Documents;

public enum DocumentStatus
{
    Uploaded   = 0,
    Pending    = 1,
    Processing = 2,
    Indexed    = 3,
    Failed     = 4
}

/// <summary>
/// Запрос переименования проекта.
/// Используется внутри клиента: перед отправкой на сервер Shell читает
/// текущие настройки проекта и формирует полный UpdateProjectSettingsRequest.
/// Проект: DevAssistant / ContourAI.
/// </summary>

namespace ContourAI.Entities.Projects;

/// <summary>
/// Лёгкий record — только новое имя.
/// AuthenticatedShellViewModel преобразует его в UpdateProjectSettingsRequest
/// (подставляя дефолты) перед вызовом PATCH /api/projects/{id}/settings.
/// </summary>
public sealed record RenameProjectRequest(string Name);

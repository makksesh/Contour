/// <summary>
/// DTO dashboard для получения последних проектов, чатов и документов с сервера.
/// Контракт соответствует GET /api/dashboard/recent.
/// Проект: DevAssistant / ContourAI.
/// </summary>
using System;
using System.Collections.Generic;

namespace ContourAI.Entities.Dashboard;

public sealed record RecentDashboardResponse(
    IReadOnlyList<RecentItemResponse> Projects,
    IReadOnlyList<RecentItemResponse> Chats,
    IReadOnlyList<RecentItemResponse> Documents);

public sealed record RecentItemResponse(
    Guid Id,
    string Title,
    DateTime UpdatedAt);

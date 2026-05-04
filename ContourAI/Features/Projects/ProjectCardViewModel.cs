/// <summary>
/// ViewModel одной карточки проекта в списке.
/// Хранит данные для отображения и ID для открытия проекта.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Features.Projects;

public sealed class ProjectCardViewModel
{
    public ProjectCardViewModel(Guid id, string name, string? description, string updatedAtText)
    {
        Id = id;
        Name = name;
        Description = string.IsNullOrWhiteSpace(description) ? "Без описания" : description;
        UpdatedAtText = updatedAtText;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string UpdatedAtText { get; }
}

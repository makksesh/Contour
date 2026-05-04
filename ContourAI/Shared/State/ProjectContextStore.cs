/// <summary>
/// Хранит контекст выбранного проекта.
/// Все экраны (Documents, Indexing, RAG Search) читают SelectedProjectId отсюда.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ContourAI.Shared.State;

public sealed class ProjectContextStore : INotifyPropertyChanged
{
    private Guid? _selectedProjectId;
    private string _selectedProjectName = string.Empty;

    /// <summary>ID текущего открытого проекта. Null — проект не выбран.</summary>
    public Guid? SelectedProjectId
    {
        get => _selectedProjectId;
        private set { _selectedProjectId = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelectedProject)); }
    }

    /// <summary>Отображаемое имя текущего проекта.</summary>
    public string SelectedProjectName
    {
        get => _selectedProjectName;
        private set { _selectedProjectName = value; OnPropertyChanged(); }
    }

    /// <summary>True если проект выбран.</summary>
    public bool HasSelectedProject => _selectedProjectId.HasValue;

    /// <summary>Устанавливает выбранный проект.</summary>
    public void Select(Guid projectId, string projectName)
    {
        SelectedProjectId = projectId;
        SelectedProjectName = projectName;
    }

    /// <summary>Сбрасывает контекст проекта (например, при logout).</summary>
    public void Clear()
    {
        SelectedProjectId = null;
        SelectedProjectName = string.Empty;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

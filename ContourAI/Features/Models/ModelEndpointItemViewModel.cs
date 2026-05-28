using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ContourAI.Entities.Models;

namespace ContourAI.Features.Models;

public sealed partial class ModelEndpointItemViewModel : ObservableObject
{
    [ObservableProperty] private Guid _id;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _modelName = string.Empty;
    [ObservableProperty] private string _baseUrl = string.Empty;
    [ObservableProperty] private string _modelType = string.Empty;
    [ObservableProperty] private int _contextWindowTokens;
    [ObservableProperty] private bool _isEnabled;

    public string StatusLabel => IsEnabled ? "Включена" : "Выключена";
    public string ToggleActionLabel => IsEnabled ? "Выключить" : "Включить";

    public ModelEndpointItemViewModel(ModelEndpointDto dto) => UpdateFrom(dto);

    public void UpdateFrom(ModelEndpointDto dto)
    {
        Id = dto.Id;
        DisplayName = dto.DisplayName;
        ModelName = dto.ModelName;
        BaseUrl = dto.BaseUrl;
        ModelType = dto.ModelType;
        ContextWindowTokens = dto.ContextWindowTokens;
        IsEnabled = dto.IsEnabled;
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(ToggleActionLabel));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(ToggleActionLabel));
    }
}

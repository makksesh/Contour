using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContourAI.Entities.Models;
using ContourAI.Shared.Api;

namespace ContourAI.Features.Models;

public sealed partial class ModelsViewModel : ObservableObject
{
    private readonly ModelsService _modelsService;
    private CancellationTokenSource? _loadCts;

    public ObservableCollection<ModelEndpointItemViewModel> Endpoints { get; } = new();
    public ObservableCollection<string> FilterOptions { get; } = new(["Все", "Chat", "Embedding"]);
    public ObservableCollection<string> TypeOptions { get; } = new(["Chat", "Embedding"]);

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _emptyMessage = "Список endpoint'ов пуст.";
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _selectedFilter = "Все";
    [ObservableProperty] private ModelEndpointItemViewModel? _selectedEndpoint;
    [ObservableProperty] private bool _isEditingExisting;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _modelName = string.Empty;
    [ObservableProperty] private string _baseUrl = string.Empty;
    [ObservableProperty] private string _selectedModelType = "Chat";
    [ObservableProperty] private int _contextWindowTokens = 8192;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _statusMessage = "Выберите endpoint или создайте новый.";

    public ModelsViewModel(ModelsService modelsService)
    {
        _modelsService = modelsService;
    }

    public bool HasSelection => SelectedEndpoint is not null;
    public string SaveButtonText => IsEditingExisting ? "Сохранить endpoint" : "Создать endpoint";
    public string FormTitle => IsEditingExisting ? "Редактирование endpoint" : "Новый endpoint";

    partial void OnSelectedFilterChanged(string value) => _ = LoadAsync();

    partial void OnSelectedEndpointChanged(ModelEndpointItemViewModel? value)
    {
        FillFormFromSelection(value);
        OnPropertyChanged(nameof(HasSelection));
    }

    public async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var filter = SelectedFilter == "Все" ? null : SelectedFilter;
            var list = await _modelsService.GetModelsAsync(filter, _loadCts.Token);
            if (list == null)
                return;

            Endpoints.Clear();
            foreach (var dto in list.OrderBy(x => x.ModelType).ThenBy(x => x.DisplayName))
                Endpoints.Add(new ModelEndpointItemViewModel(dto));

            IsEmpty = Endpoints.Count == 0;
            EmptyMessage = filter is null
                ? "Список endpoint'ов пуст."
                : $"Для типа {filter} endpoint'ы не найдены.";

            if (SelectedEndpoint is not null)
            {
                SelectedEndpoint = Endpoints.FirstOrDefault(x => x.Id == SelectedEndpoint.Id);
                if (SelectedEndpoint is null)
                    ResetForm();
            }
            else if (IsEditingExisting)
            {
                ResetForm();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshSelectedAsync()
    {
        if (SelectedEndpoint == null)
            return;

        try
        {
            var dto = await _modelsService.GetModelAsync(SelectedEndpoint.Id);
            if (dto == null)
                return;

            SelectedEndpoint.UpdateFrom(dto);
            FillForm(dto);
            StatusMessage = "Endpoint обновлён с сервера.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void CreateNew()
    {
        SelectedEndpoint = null;
        ResetForm();
        StatusMessage = "Заполните форму для нового endpoint.";
    }

    [RelayCommand]
    private void SelectEndpoint(ModelEndpointItemViewModel? endpoint)
    {
        if (endpoint == null)
            return;

        SelectedEndpoint = endpoint;
        StatusMessage = $"Открыт endpoint «{endpoint.DisplayName}».";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!Validate())
            return;

        IsSaving = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var normalizedApiKey = string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey.Trim();

            if (IsEditingExisting && SelectedEndpoint is not null)
            {
                var updated = await _modelsService.UpdateAsync(
                    SelectedEndpoint.Id,
                    new UpdateModelEndpointRequest(
                        DisplayName.Trim(),
                        ModelName.Trim(),
                        BaseUrl.Trim(),
                        ContextWindowTokens,
                        normalizedApiKey));

                if (updated == null)
                    return;

                SelectedEndpoint.UpdateFrom(updated);
                FillForm(updated);
                StatusMessage = "Endpoint сохранён.";
            }
            else
            {
                var created = await _modelsService.CreateAsync(
                    new CreateModelEndpointRequest(
                        DisplayName.Trim(),
                        ModelName.Trim(),
                        BaseUrl.Trim(),
                        SelectedModelType,
                        ContextWindowTokens,
                        normalizedApiKey));

                if (created == null)
                    return;

                var item = new ModelEndpointItemViewModel(created);
                Endpoints.Insert(0, item);
                SelectedEndpoint = item;
                IsEmpty = false;
                StatusMessage = "Endpoint создан.";
            }

            await LoadAsync();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
            OnPropertyChanged(nameof(SaveButtonText));
            OnPropertyChanged(nameof(FormTitle));
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedEndpoint == null)
            return;

        IsSaving = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var deleted = await _modelsService.DeleteAsync(SelectedEndpoint.Id);
            if (!deleted)
                return;

            var deletedName = SelectedEndpoint.DisplayName;
            Endpoints.Remove(SelectedEndpoint);
            SelectedEndpoint = null;
            ResetForm();
            IsEmpty = Endpoints.Count == 0;
            StatusMessage = $"Endpoint «{deletedName}» удалён.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task ToggleEnabledAsync(ModelEndpointItemViewModel? endpoint)
    {
        if (endpoint == null)
            return;

        try
        {
            var nextState = !endpoint.IsEnabled;
            var ok = await _modelsService.SetEnabledAsync(endpoint.Id, nextState);
            if (!ok)
                return;

            endpoint.IsEnabled = nextState;
            if (SelectedEndpoint?.Id == endpoint.Id)
                StatusMessage = nextState ? "Endpoint включён." : "Endpoint выключен.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
    }

    private bool Validate()
    {
        HasError = false;
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(DisplayName))
            return FailValidation("Укажите отображаемое имя endpoint.");
        if (string.IsNullOrWhiteSpace(ModelName))
            return FailValidation("Укажите имя модели.");
        if (string.IsNullOrWhiteSpace(BaseUrl))
            return FailValidation("Укажите Base URL.");
        if (!Uri.TryCreate(BaseUrl.Trim(), UriKind.Absolute, out _))
            return FailValidation("Base URL должен быть валидным абсолютным адресом.");
        if (ContextWindowTokens < 1 || ContextWindowTokens > 200000)
            return FailValidation("Окно контекста должно быть в диапазоне 1–200000.");

        return true;
    }

    private bool FailValidation(string message)
    {
        HasError = true;
        ErrorMessage = message;
        return false;
    }

    private void FillFormFromSelection(ModelEndpointItemViewModel? endpoint)
    {
        if (endpoint == null)
        {
            ResetForm();
            return;
        }

        FillForm(new ModelEndpointDto(
            endpoint.Id,
            endpoint.DisplayName,
            endpoint.ModelName,
            endpoint.BaseUrl,
            endpoint.ModelType,
            endpoint.ContextWindowTokens,
            endpoint.IsEnabled));
    }

    private void FillForm(ModelEndpointDto dto)
    {
        IsEditingExisting = true;
        DisplayName = dto.DisplayName;
        ModelName = dto.ModelName;
        BaseUrl = dto.BaseUrl;
        SelectedModelType = dto.ModelType;
        ContextWindowTokens = dto.ContextWindowTokens;
        ApiKey = dto.ApiKey ?? string.Empty;
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(FormTitle));
    }

    private void ResetForm()
    {
        IsEditingExisting = false;
        DisplayName = string.Empty;
        ModelName = string.Empty;
        BaseUrl = string.Empty;
        SelectedModelType = "Chat";
        ContextWindowTokens = 8192;
        ApiKey = string.Empty;
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(FormTitle));
    }
}

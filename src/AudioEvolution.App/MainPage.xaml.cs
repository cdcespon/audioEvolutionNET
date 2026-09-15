using AudioEvolution.Core.Model;
using AudioEvolution.Data.Repositories;

namespace AudioEvolution.App;

public sealed record ProjectListItem(Guid Id, string Name, DateTimeOffset ModifiedAt);

public partial class MainPage : ContentPage
{
    private readonly ProjectRepository _repository;

    public MainPage(ProjectRepository repository)
    {
        InitializeComponent();
        _repository = repository;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshProjectListAsync();
    }

    private async Task RefreshProjectListAsync()
    {
        var projects = await _repository.ListAsync();
        ProjectsList.ItemsSource = projects
            .Select(p => new ProjectListItem(p.Id, p.Name, p.ModifiedAt))
            .ToList();
    }

    private async void OnNewProjectClicked(object? sender, EventArgs e)
    {
        string? name = await DisplayPromptAsync("Nuevo proyecto", "Nombre del proyecto:");
        if (string.IsNullOrWhiteSpace(name)) return;

        var project = new Project { Name = name, SampleRate = 48000, BitDepth = 24 };
        await _repository.SaveAsync(project);
        await RefreshProjectListAsync();
    }

    private async void OnProjectSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ProjectListItem item) return;
        ((CollectionView)sender!).SelectedItem = null;

        // Multitrack editor (Task #6) is not built yet — this is the wiring point where it
        // will be pushed once it exists.
        await DisplayAlertAsync("Proyecto", $"Abrir '{item.Name}' — editor multipista pendiente.", "OK");
    }
}

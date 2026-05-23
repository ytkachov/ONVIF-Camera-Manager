using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OnvifManager.ViewModels;

// Base for the editable parameter tabs. Tracks whether the user has changed any editable
// field since the last load/save so the shared "Применить" button can be enabled only
// when there is something to apply. Only properties in TrackedProperties count; loads and
// programmatic field fills must be wrapped in SuspendTracking() so they don't mark dirty.
public abstract partial class ConfigEditorViewModel : ObservableObject
{
    private int _suspend;

    [ObservableProperty] private bool _hasChanges;

    // Editable property names that represent real user edits (allowlist). Selection,
    // status, loading flags and collections are excluded.
    protected abstract IReadOnlySet<string> TrackedProperties { get; }

    public event Action? ChangesChanged;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_suspend == 0 && e.PropertyName != null && TrackedProperties.Contains(e.PropertyName))
            HasChanges = true;
    }

    partial void OnHasChangesChanged(bool value) => ChangesChanged?.Invoke();

    protected void ResetChanges() => HasChanges = false;

    // Wrap loads / selection-driven field fills so they don't register as user edits.
    protected IDisposable SuspendTracking() => new Suspender(this);

    private sealed class Suspender : IDisposable
    {
        private readonly ConfigEditorViewModel _owner;
        public Suspender(ConfigEditorViewModel owner) { _owner = owner; _owner._suspend++; }
        public void Dispose() => _owner._suspend--;
    }
}

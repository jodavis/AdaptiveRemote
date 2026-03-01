using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace AdaptiveRemote.Mvvm;

public abstract class MvvmComponent<ViewModelType> : ComponentBase, IDisposable
    where ViewModelType : MvvmObject
{
    private bool _disposedValue;

    [Parameter]
    public ViewModelType? ViewModel { get; set; } = default;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) => _ = InvokeAsync(StateHasChanged);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                if (ViewModel is not null)
                {
                    ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                }
            }

            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~MvvmComponent()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

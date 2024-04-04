using Microsoft.AspNetCore.Components;

namespace AdaptiveRemote.Mvvm;

public abstract class MvvmComponent : ComponentBase
{
    private readonly MvvmObject _bindings = new MvvmObject();

    protected MvvmComponent()
    {
        _bindings.PropertyChanged += OnPropertyChanged;
    }

    protected PropertyType GetValue<PropertyType>(MvvmProperty<PropertyType> property)
        => _bindings.GetValue(property);
    protected void SetValue<PropertyType>(MvvmProperty<PropertyType> property, PropertyType value)
        => _bindings.SetValue(property, value);
    protected IDisposable Bind<PropertyType>(MvvmProperty<PropertyType> targetProperty, MvvmObject source, MvvmProperty<PropertyType> sourceProperty)
        => _bindings.Bind(targetProperty, source, sourceProperty);

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => StateHasChanged();
}


using System.ComponentModel;

namespace AdaptiveRemote.Mvvm;

public class MvvmObject : INotifyPropertyChanging, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event PropertyChangingEventHandler? PropertyChanging;

    private Dictionary<object, object?> _values = new();

    public IDisposable Bind<PropertyType>(MvvmProperty<PropertyType> targetProperty, MvvmObject source, MvvmProperty<PropertyType> sourceProperty)
        => new Binding<PropertyType>(source, sourceProperty, this, targetProperty);

    public PropertyType GetValue<PropertyType>(MvvmProperty<PropertyType> property)
    {
        if (!_values.TryGetValue(property, out object? value) || value is null)
        {
            return property.DefaultValue;
        }
        else if (value is PropertyType typedValue)
        {
            return typedValue;
        }
        else
        {
            throw new InvalidOperationException($"Value for property {property.Name} has the wrong type");
        }
    }

    public void SetValue<PropertyType>(MvvmProperty<PropertyType> property, PropertyType value)
    {
        PropertyChanging?.Invoke(this, new(property.Name));
        _values[property] = value;
        PropertyChanged?.Invoke(this, new(property.Name));
    }

    private sealed class Binding<PropertyType> : IDisposable
    {
        private readonly MvvmObject _source;
        private readonly MvvmProperty<PropertyType> _sourceProperty;
        private readonly MvvmObject _target;
        private readonly MvvmProperty<PropertyType> _targetProperty;

        internal Binding(
            MvvmObject source,
            MvvmProperty<PropertyType> sourceProperty,
            MvvmObject target,
            MvvmProperty<PropertyType> targetProperty)
        {
            _source = source;
            _sourceProperty = sourceProperty;
            _target = target;
            _targetProperty = targetProperty;

            _source.PropertyChanged += OnPropertyChanged;
            _target.SetValue(_targetProperty, _source.GetValue(_sourceProperty));
        }

        public void Dispose()
        {
            _source.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (ReferenceEquals(sender, _source) &&
                e.PropertyName == _sourceProperty.Name)
            {
                _target.SetValue(_targetProperty, _source.GetValue(_sourceProperty));
            }
        }
    }
}

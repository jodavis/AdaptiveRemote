using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Models;

public abstract class Command : RemoteLayoutElement
{
    public static readonly MvvmProperty<bool> IsActiveProperty = new(nameof(IsActive));
    public static readonly MvvmProperty<bool> IsEnabledProperty = new(nameof(IsEnabled));
    public static readonly MvvmProperty<ExecuteDelegate?> ExecuteAsyncProperty = new(nameof(ExecuteAsync));

    public delegate Task ExecuteDelegate(CancellationToken cancellationToken);

    protected Command(
        string name,
        string? placement,
        string? label,
        string? cssid,
        string? glyph)
        : base(cssid ?? name.ToUpperInvariant(), placement)
    {
        Name = name;
        Label = label ?? name;
        Glyph = glyph;
    }

    public string Name { get; }
    public string Label { get; }
    public string? Glyph { get; }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool IsEnabled
    {
        get => GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    public ExecuteDelegate? ExecuteAsync
    {
        get => GetValue(ExecuteAsyncProperty);
        set => SetValue(ExecuteAsyncProperty, value);
    }

    public override string ToString() => $"{GetType().Name} '{Name}'";
}

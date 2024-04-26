using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Models;

public abstract class Command : RemoteLayoutElement
{
    public static readonly MvvmProperty<bool> IsActiveProperty = new(nameof(IsActive));
    public static readonly MvvmProperty<bool> IsVisibleProperty = new(nameof(IsVisible));

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

    public bool IsVisible
    {
        get => GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    public override string ToString() => $"{GetType().Name} '{Name}'";
}

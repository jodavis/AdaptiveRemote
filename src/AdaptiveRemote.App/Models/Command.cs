using AdaptiveRemote.Mvvm;

namespace AdaptiveRemote.Models;

public abstract class Command : RemoteLayoutElement
{
    public static readonly MvvmProperty<bool> IsActiveProperty = new(nameof(IsActive));
    public static readonly MvvmProperty<bool> IsEnabledProperty = new(nameof(IsEnabled));
    public static readonly MvvmProperty<ExecuteDelegate?> ExecuteAsyncProperty = new(nameof(ExecuteAsync));
    public static readonly MvvmProperty<ExecuteDelegate?> ProgramAsyncProperty = new(nameof(ProgramAsync));

    public delegate Task ExecuteDelegate(CancellationToken cancellationToken);

    protected Command(
        string name,
        string? placement,
        string? label,
        string? cssid,
        string? glyph,
        string? reverse,
        string? speakPhrase)
        : base(cssid ?? name.ToUpperInvariant(), placement)
    {
        Name = name;
        Label = label ?? name;
        Glyph = glyph;
        SpeakPhrase = speakPhrase ?? name;
        Reverse = reverse;
    }

    public string Name { get; }
    public string Label { get; }
    public string? Glyph { get; }
    public string SpeakPhrase { get; }
    public string? Reverse { get; }

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

    public ExecuteDelegate? ProgramAsync
    {
        get => GetValue(ProgramAsyncProperty);
        set => SetValue(ProgramAsyncProperty, value);
    }

    public override string ToString() => $"{GetType().Name} '{Name}'";
}

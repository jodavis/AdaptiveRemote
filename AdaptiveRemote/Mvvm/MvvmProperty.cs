namespace AdaptiveRemote.Mvvm;

public class MvvmProperty<PropertyType>
{
    public MvvmProperty(string name, PropertyType defaultValue = default!)
    {
        Name = name;
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public PropertyType DefaultValue { get; }
}

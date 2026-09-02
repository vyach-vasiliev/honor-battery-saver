using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using HonorBatterySaver.Core;

namespace HonorBatterySaver.Tray;

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocalizedExtension(string key) : MarkupExtension
{
    public string Key { get; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => new System.Windows.Data.Binding($"[{Key}]")
    {
        Source = LocalizedStrings.Instance,
        Mode = BindingMode.OneWay
    }.ProvideValue(serviceProvider);
}

public sealed class LocalizedStrings : INotifyPropertyChanged
{
    private LocalizedStrings() => Strings.CultureChanged += (_, _) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));

    public static LocalizedStrings Instance { get; } = new();

    public string this[string key] => Strings.Get(key);

    public event PropertyChangedEventHandler? PropertyChanged;
}

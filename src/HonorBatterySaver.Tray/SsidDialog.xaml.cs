using System.Windows;
using HonorBatterySaver.Core;
using MessageBox = System.Windows.MessageBox;

namespace HonorBatterySaver.Tray;

public sealed record WifiNetworkChoiceView(string Ssid, string Status);

public partial class SsidDialog : Window
{
    public SsidDialog(
        string ssid,
        BatteryMode mode,
        IReadOnlyList<ModeChoice> modeChoices,
        WifiCatalogSnapshot catalog,
        bool isEditing)
    {
        InitializeComponent();
        ThemeManager.Attach(this);
        DialogTitle.Text = Strings.Get(isEditing ? "WifiRule_EditTitle" : "WifiRule_AddTitle");
        var choices = catalog.Networks.Select(network => new WifiNetworkChoiceView(
            network.Ssid,
            GetStatus(network))).ToArray();
        NetworkComboBox.ItemsSource = choices;
        ModeComboBox.ItemsSource = modeChoices;
        ModeComboBox.SelectedValue = mode;
        NetworkListHint.Text = catalog.AccessDenied
            ? Strings.Get("WifiRule_ListDenied")
            : choices.Length == 0
                ? Strings.Get("WifiRule_ListEmpty")
                : Strings.Get("WifiRule_ListOrder");

        Loaded += (_, _) =>
        {
            NetworkComboBox.Text = ssid;
            NetworkComboBox.Focus();
            if (string.IsNullOrEmpty(ssid) && choices.Length > 0)
            {
                NetworkComboBox.IsDropDownOpen = true;
            }
        };
    }

    public string Ssid => NetworkComboBox.Text;
    public BatteryMode Mode => ModeComboBox.SelectedValue is BatteryMode mode ? mode : BatteryMode.Home;

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(NetworkComboBox.Text))
        {
            MessageBox.Show(this, Strings.Get("WifiRule_SelectNetwork"), Strings.Get("WifiRule_Title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private static string GetStatus(WifiNetworkCandidate network) => network switch
    {
        { IsConnected: true } => Strings.Get("WifiRule_Connected"),
        { IsAvailable: true, IsKnown: true } => Strings.Get("WifiRule_AvailableKnown"),
        { IsAvailable: true } => Strings.Get("WifiRule_Available"),
        _ => Strings.Get("WifiRule_Known")
    };
}

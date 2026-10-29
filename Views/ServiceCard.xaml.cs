using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ElectroERP.Manager.Views;

public partial class ServiceCard : UserControl
{
    public event RoutedEventHandler? PrimaryActionClick;
    public event RoutedEventHandler? SecondaryActionClick;
    public event RoutedEventHandler? LinkActionClick;

    public ServiceCard()
    {
        InitializeComponent();
    }

    public string CardTitle
    {
        get => CardTitleText.Text;
        set => CardTitleText.Text = value;
    }

    public string Address
    {
        get => AddressText.Text;
        set => AddressText.Text = value;
    }

    public string LinkText
    {
        get => LinkActionButton.Content?.ToString() ?? string.Empty;
        set => LinkActionButton.Content = value;
    }

    public bool ShowSecondaryButton
    {
        get => SecondaryButton.Visibility == Visibility.Visible;
        set => SecondaryButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public string SecondaryButtonText
    {
        get => SecondaryButton.Content?.ToString() ?? string.Empty;
        set => SecondaryButton.Content = value;
    }

    public void UpdateCardState(
        string statusLabel,
        Brush dotBrush,
        Brush statusTextBrush,
        string primaryButtonLabel,
        Style primaryButtonStyle,
        bool isPrimaryEnabled,
        bool isSecondaryEnabled,
        bool isLinkEnabled,
        string dependencyHint = "")
    {
        StatusDot.Fill = dotBrush;
        StatusText.Text = statusLabel;
        StatusText.Foreground = statusTextBrush;

        PrimaryButton.Content = primaryButtonLabel;
        PrimaryButton.Style = primaryButtonStyle;
        PrimaryButton.IsEnabled = isPrimaryEnabled;

        SecondaryButton.IsEnabled = isSecondaryEnabled;
        LinkActionButton.IsEnabled = isLinkEnabled;

        DependencyHintText.Text = dependencyHint;
    }

    private void OnPrimaryButtonClick(object sender, RoutedEventArgs e)
    {
        PrimaryActionClick?.Invoke(this, e);
    }

    private void OnSecondaryButtonClick(object sender, RoutedEventArgs e)
    {
        SecondaryActionClick?.Invoke(this, e);
    }

    private void OnLinkActionButtonClick(object sender, RoutedEventArgs e)
    {
        LinkActionClick?.Invoke(this, e);
    }
}

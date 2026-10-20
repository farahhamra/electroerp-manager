using System.Text.RegularExpressions;
using System.Windows;
using ElectroERP.Manager.Services;

namespace ElectroERP.Manager.Views;

public partial class ChangePinDialog : Window
{
    private readonly PinAuthService _pinAuthService;

    public ChangePinDialog(PinAuthService pinAuthService)
    {
        InitializeComponent();
        _pinAuthService = pinAuthService;

        Loaded += (_, _) => CurrentPinBox.Focus();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var currentPin = CurrentPinBox.Password;
        var newPin = NewPinBox.Password;
        var confirmPin = ConfirmPinBox.Password;

        if (string.IsNullOrEmpty(currentPin) || string.IsNullOrEmpty(newPin) || string.IsNullOrEmpty(confirmPin))
        {
            ShowError("All PIN fields are required.");
            return;
        }

        if (newPin != confirmPin)
        {
            ShowError("New PIN and confirmation do not match.");
            return;
        }

        if (!Regex.IsMatch(newPin, @"^[0-9]{6}$"))
        {
            ShowError("New PIN must be exactly 6 digits.");
            return;
        }

        var (success, error) = _pinAuthService.UpdatePin(currentPin, newPin);
        if (!success)
        {
            ShowError(error ?? "Failed to update PIN.");
            return;
        }

        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.Visibility = Visibility.Visible;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

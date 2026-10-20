using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ElectroERP.Manager.Services;

namespace ElectroERP.Manager.Views;

public partial class PinDialog : Window
{
    private readonly PinAuthService _pinAuthService;
    private readonly StringBuilder _pinBuffer = new();
    private readonly TextBlock[] _digitBoxes;
    private readonly Rectangle[] _carets;

    private readonly DoubleAnimation _pulseAnimation = new()
    {
        From = 1.0,
        To = 0.08,
        Duration = new Duration(TimeSpan.FromMilliseconds(450)),
        AutoReverse = true,
        RepeatBehavior = RepeatBehavior.Forever,
        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
    };

    public PinDialog(PinAuthService pinAuthService, string? prompt = null, string? title = null)
    {
        InitializeComponent();
        _pinAuthService = pinAuthService;

        _digitBoxes = new[] { Digit0, Digit1, Digit2, Digit3, Digit4, Digit5 };
        _carets = new[] { Caret0, Caret1, Caret2, Caret3, Caret4, Caret5 };

        if (!string.IsNullOrWhiteSpace(title))
        {
            TitleLabel.Text = title;
        }

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            PromptLabel.Text = prompt;
        }

        Loaded += (_, _) =>
        {
            Focus();
            UpdateSlots();
        };

        Activated += (_, _) =>
        {
            UpdateSlots();
        };

        MouseDown += (_, _) => Focus();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            return;
        }

        if (e.Key == Key.Back)
        {
            if (_pinBuffer.Length > 0)
            {
                _pinBuffer.Length--;
                ResetErrorState();
                UpdateSlots();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            ValidateAndSubmit();
            e.Handled = true;
            return;
        }

        int? digit = GetDigitFromKey(e.Key);
        if (digit.HasValue)
        {
            if (_pinBuffer.Length < 6)
            {
                _pinBuffer.Append(digit.Value);
                ResetErrorState();
                UpdateSlots();

                if (_pinBuffer.Length == 6)
                {
                    ValidateAndSubmit();
                }
            }
            e.Handled = true;
        }
    }

    private void UpdateSlots()
    {
        for (int i = 0; i < 6; i++)
        {
            if (i < _pinBuffer.Length)
            {
                _digitBoxes[i].Text = _pinBuffer[i].ToString();
                _carets[i].BeginAnimation(UIElement.OpacityProperty, null);
                _carets[i].Opacity = 0.0;
                _carets[i].Visibility = Visibility.Collapsed;
            }
            else if (i == _pinBuffer.Length)
            {
                _digitBoxes[i].Text = string.Empty;
                _carets[i].Visibility = Visibility.Visible;
                _carets[i].BeginAnimation(UIElement.OpacityProperty, _pulseAnimation);
            }
            else
            {
                _digitBoxes[i].Text = string.Empty;
                _carets[i].BeginAnimation(UIElement.OpacityProperty, null);
                _carets[i].Opacity = 0.0;
                _carets[i].Visibility = Visibility.Collapsed;
            }
        }
    }

    private void ResetErrorState()
    {
        ErrorLabel.Visibility = Visibility.Hidden;
        PinBoxBorder.BorderBrush = (Brush)FindResource("BorderFocusBrush");
        FloatingLabel.Foreground = (Brush)FindResource("AccentBrush");
    }

    private void ValidateAndSubmit()
    {
        if (_pinBuffer.Length != 6)
        {
            ShowError("Please enter all 6 digits.");
            return;
        }

        if (_pinAuthService.VerifyPin(_pinBuffer.ToString()))
        {
            PinBoxBorder.BorderBrush = (Brush)FindResource("StatusRunningBrush");
            FloatingLabel.Foreground = (Brush)FindResource("StatusRunningBrush");
            DialogResult = true;
            Close();
        }
        else
        {
            ShowError("Incorrect PIN. Please try again.");
            PinBoxBorder.BorderBrush = (Brush)FindResource("ActionStopBrush");
            FloatingLabel.Foreground = (Brush)FindResource("ActionStopBrush");

            _pinBuffer.Clear();
            UpdateSlots();

            if (FindResource("ShakeAnimation") is Storyboard sb)
            {
                sb.Begin(DialogContainer);
            }
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.Visibility = Visibility.Visible;
    }

    private static int? GetDigitFromKey(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => key - Key.D0,
        >= Key.NumPad0 and <= Key.NumPad9 => key - Key.NumPad0,
        _ => null
    };

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        ValidateAndSubmit();
    }
}

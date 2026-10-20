using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ElectroERP.Manager.Models;

namespace ElectroERP.Manager.Services;

public class PinAuthService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;
    private const string DefaultPin = "123456";

    private readonly SettingsService _settingsService;
    private AppConfig _config;

    public PinAuthService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _config = _settingsService.LoadSettings();

        if (string.IsNullOrEmpty(_config.PinHash) || string.IsNullOrEmpty(_config.PinSalt))
        {
            SetPinInternal(DefaultPin);
        }
    }

    public bool VerifyPin(string inputPin)
    {
        if (string.IsNullOrWhiteSpace(inputPin) || inputPin.Length != 6)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(_config.PinSalt);
            var expectedHash = Convert.FromBase64String(_config.PinHash);
            var computedHash = ComputeHash(inputPin, salt);

            return CryptographicOperations.FixedTimeEquals(expectedHash, computedHash);
        }
        catch
        {
            return false;
        }
    }

    public (bool Success, string? ErrorMessage) UpdatePin(string currentPin, string newPin)
    {
        if (!VerifyPin(currentPin))
        {
            return (false, "Current PIN is incorrect.");
        }

        if (string.IsNullOrWhiteSpace(newPin) || !Regex.IsMatch(newPin, @"^[0-9]{6}$"))
        {
            return (false, "New PIN must consist of exactly 6 numeric digits.");
        }

        SetPinInternal(newPin);
        return (true, null);
    }

    private void SetPinInternal(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(pin, salt);

        _config.PinSalt = Convert.ToBase64String(salt);
        _config.PinHash = Convert.ToBase64String(hash);

        _settingsService.SaveSettings(_config);
    }

    private static byte[] ComputeHash(string pin, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(pin, salt, Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(HashSize);
    }
}

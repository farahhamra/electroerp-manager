namespace ElectroERP.Manager.Models;

public class AppConfig
{
    public string PinHash { get; set; } = string.Empty;
    public string PinSalt { get; set; } = string.Empty;
    public string ServerDirectory { get; set; } = string.Empty;
    public string FrontendDirectory { get; set; } = string.Empty;
    public string LaunchCommand { get; set; } = "node dist/index.js";
    public bool LaunchOnWindowsStartup { get; set; } = false;
    public bool AutoRestartOnCrash { get; set; } = true;
}

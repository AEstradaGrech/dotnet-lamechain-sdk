namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums
{
    public enum EScriptShell
    {
        Auto = 0,         // PowerShell on Windows, Bash anywhere else (configuration only, executors are always Bash or PowerShell)
        Bash = 1,         // .sh scripts run with bash
        PowerShell = 2,   // .ps1 scripts run with powershell.exe (Windows PowerShell 5.1) or pwsh (PowerShell 7)
    }
}

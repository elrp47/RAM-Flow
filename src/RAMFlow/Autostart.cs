using System.Diagnostics;
using System.Security;
using System.Text;

namespace RAMFlow;

/// <summary>
/// Автозапуск через Планировщик заданий, а не через ключ Run: программе нужны
/// права администратора, и задача с «наивысшими правами» стартует без окна UAC.
/// </summary>
public static class Autostart
{
    private const string TaskName = "RAMFlow";

    public static bool IsEnabled() => Schtasks($"/Query /TN \"{TaskName}\"") == 0;

    public static bool Set(bool enable)
    {
        if (!enable)
            return Schtasks($"/Delete /TN \"{TaskName}\" /F") == 0;

        string exe = Environment.ProcessPath ?? Application.ExecutablePath;
        string user = SecurityElement.Escape($"{Environment.UserDomainName}\\{Environment.UserName}");
        string xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <Triggers>
                <LogonTrigger><Enabled>true</Enabled><UserId>{user}</UserId><Delay>PT10S</Delay></LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{SecurityElement.Escape(exe)}</Command>
                  <Arguments>--tray</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;

        string tmp = Path.Combine(Path.GetTempPath(), "ramflow_task.xml");
        File.WriteAllText(tmp, xml, Encoding.Unicode);
        try { return Schtasks($"/Create /TN \"{TaskName}\" /XML \"{tmp}\" /F") == 0; }
        finally { try { File.Delete(tmp); } catch { } }
    }

    private static int Schtasks(string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p == null) return -1;
            p.WaitForExit(10000);
            return p.ExitCode;
        }
        catch { return -1; }
    }
}

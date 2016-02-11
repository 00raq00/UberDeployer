using UberDeployer.Common.SyntaxSugar;

namespace UberDeployer.Core.Domain
{
  public class TerminalServerMachine
  {
    public TerminalServerMachine(string machineName, string appsBaseDirPath, string appsShortcutFolder)
    {
      Guard.NotNullNorEmpty(machineName, "machineName");
      Guard.NotNullNorEmpty(appsBaseDirPath, "appsBaseDirPath");
      Guard.NotNullNorEmpty(appsShortcutFolder, "appsShortcutFolder");

      MachineName = machineName;
      AppsBaseDirPath = appsBaseDirPath;
      AppsShortcutFolder = appsShortcutFolder;
    }

    public string MachineName { get; private set; }

    public string AppsBaseDirPath { get; private set; }

    public string AppsShortcutFolder { get; private set; }
  }
}

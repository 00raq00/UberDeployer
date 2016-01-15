using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UberDeployer.Common.SyntaxSugar;

namespace UberDeployer.Core.Domain
{
  public class EnvironmentInfo
  {
    private static readonly Regex _DriveLetterRegex = new Regex(@"^(?<DriveLetter>[a-z]):\\", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly List<String> _webServerMachines;
    private readonly List<String> _schedulerServerTasksMachineNames;
    private readonly List<String> _schedulerServerBinariesMachineNames;
    private readonly Dictionary<string, EnvironmentUser> _environmentUsersByIdDict;
    private readonly Dictionary<string, IisAppPoolInfo> _appPoolInfosByNameDict;
    private readonly Dictionary<string, DatabaseServer> _databaseServersByIdDict;
    private readonly Dictionary<string, ProjectToFailoverClusterGroupMapping> _projectToFailoverClusterGroupMappingsDict;
    private readonly Dictionary<string, WebAppProjectConfigurationOverride> _webAppProjectConfigurationOverridesDict;
    private readonly Dictionary<string, DbProjectConfigurationOverride> _dbProjectConfigurationOverridesDict;
    private readonly Dictionary<string, CustomEnvMachine> _customEnvMachinesDict;

    public EnvironmentInfo(
      string name,
      bool isVisibleToClients,
      string configurationTemplateName,
      string appServerMachineName,
      string failoverClusterMachineName,
      IEnumerable<string> webServerMachineNames,
      IEnumerable<TerminalServerMachine> terminalServerMachines,
      IEnumerable<string> schedulerServerTasksMachineNames,
      IEnumerable<string> schedulerServerBinariesMachineNames,
      string ntServicesBaseDirPath,
      string webAppsBaseDirPath,
      string schedulerAppsBaseDirPath,
      bool enableFailoverClusteringForNtServices,      
      IEnumerable<EnvironmentUser> environmentUsers,
      IEnumerable<IisAppPoolInfo> appPoolInfos,
      IEnumerable<DatabaseServer> databaseServers,
      IEnumerable<ProjectToFailoverClusterGroupMapping> projectToFailoverClusterGroupMappings,
      IEnumerable<WebAppProjectConfigurationOverride> webAppProjectConfigurationOverrides,
      IEnumerable<DbProjectConfigurationOverride> dbProjectConfigurations,      
      string manualDeploymentPackageDirPath,
      string domainName,
      IEnumerable<CustomEnvMachine>  customEnvMachines)
    {
      Guard.NotNullNorEmpty(name, "name");
      Guard.NotNull(configurationTemplateName, "configurationTemplateName");
      Guard.NotNullNorEmpty(appServerMachineName, "appServerMachineName");
      Guard.NotNull(webServerMachineNames, "webServerMachineNames");
      Guard.NotNull(schedulerServerTasksMachineNames, "schedulerServerBinariesMachineNames");
      Guard.NotNull(schedulerServerBinariesMachineNames, "schedulerServerBinariesMachineNames");
      Guard.NotNull(terminalServerMachines, "terminalServerMachines");
      Guard.NotNullNorEmpty(ntServicesBaseDirPath, "ntServicesBaseDirPath");
      Guard.NotNullNorEmpty(webAppsBaseDirPath, "webAppsBaseDirPath");
      Guard.NotNullNorEmpty(schedulerAppsBaseDirPath, "schedulerAppsBaseDirPath");
      Guard.NotNull(environmentUsers, "environmentUsers");
      Guard.NotNull(appPoolInfos, "appPoolInfos");
      Guard.NotNull(webAppProjectConfigurationOverrides, "webAppProjectConfigurationOverrides");
      Guard.NotNull(projectToFailoverClusterGroupMappings, "projectToFailoverClusterGroupMappings");
      Guard.NotNull(dbProjectConfigurations, "dbProjectConfigurations");
      Guard.NotNullNorEmpty(domainName, "domainName");
      Guard.NotNullNorEmpty(name, "terminalAppShortcutPath");

      if (enableFailoverClusteringForNtServices && string.IsNullOrEmpty(failoverClusterMachineName))
      {
        throw new ArgumentException("If enableFailoverClusteringForNtServices is set, failoverClusterMachineName must not be empty.", "enableFailoverClusteringForNtServices");
      }

      Name = name;
      IsVisibleToClients = isVisibleToClients;
      ConfigurationTemplateName = configurationTemplateName;
      AppServerMachineName = appServerMachineName;
      FailoverClusterMachineName = failoverClusterMachineName;
      _webServerMachines = webServerMachineNames.ToList();
      
      TerminalServerMachines = terminalServerMachines;
      if (TerminalServerMachines.Any() == false)
      {
        throw new ArgumentException("At least one terminal server machine must be present.", "terminalServerMachines");
      }

      _schedulerServerTasksMachineNames = schedulerServerTasksMachineNames.ToList();

      if (_schedulerServerTasksMachineNames.Count == 0)
      {
        throw new ArgumentException("At least one scheduler server tasks machine name must be present.", "schedulerServerTasksMachineNames");
      }

      _schedulerServerBinariesMachineNames = schedulerServerBinariesMachineNames.ToList();

      if (_schedulerServerBinariesMachineNames.Count == 0)
      {
        throw new ArgumentException("At least one scheduler server binaries machine name must be present.", "schedulerServerTasksMachineNames");
      }

      NtServicesBaseDirPath = ntServicesBaseDirPath;
      WebAppsBaseDirPath = webAppsBaseDirPath;
      SchedulerAppsBaseDirPath = schedulerAppsBaseDirPath;
      EnableFailoverClusteringForNtServices = enableFailoverClusteringForNtServices;

      _environmentUsersByIdDict = environmentUsers.ToDictionary(e => e.Id);
      _appPoolInfosByNameDict = appPoolInfos.ToDictionary(e => e.Name);
      _databaseServersByIdDict = databaseServers.ToDictionary(e => e.Id);

      _projectToFailoverClusterGroupMappingsDict = projectToFailoverClusterGroupMappings.ToDictionary(e => e.ProjectName);

      _webAppProjectConfigurationOverridesDict = webAppProjectConfigurationOverrides.ToDictionary(e => e.ProjectName);
      _dbProjectConfigurationOverridesDict = dbProjectConfigurations.ToDictionary(e => e.ProjectName);

      ManualDeploymentPackageDirPath = manualDeploymentPackageDirPath;      
      DomainName = domainName;

      if (customEnvMachines != null)
      {
        _customEnvMachinesDict = customEnvMachines.ToDictionary(m => m.Id);
      }
    }

    public static string GetNetworkPath(string machineName, string absoluteLocalPath)
    {
      Guard.NotNullNorEmpty(machineName, "machineName");
      Guard.NotNullNorEmpty(absoluteLocalPath, "absoluteLocalPath");

      if (absoluteLocalPath.StartsWith(@"\\"))
      {
        throw new ArgumentException(string.Format("The path is already a network path. Absolute local path: '{0}'.", absoluteLocalPath), "absoluteLocalPath");
      }

      Match driveLetterMatch = _DriveLetterRegex.Match(absoluteLocalPath);

      if (!driveLetterMatch.Success)
      {
        throw new ArgumentException(string.Format("The path is not an absolute local path, ie. it doesn't start with a drive letter followed by a colon and a backslash. Absolute local path: '{0}'.", absoluteLocalPath), "absoluteLocalPath");
      }

      string driveLetter = driveLetterMatch.Groups["DriveLetter"].Value;

      return
        string.Format(
          "\\\\{0}\\{1}$\\{2}",
          machineName,
          driveLetter,
          absoluteLocalPath.Substring(driveLetterMatch.Length));
    }

    public string GetAppServerNetworkPath(string absoluteLocalPath)
    {
      Guard.NotNullNorEmpty(absoluteLocalPath, "absoluteLocalPath");

      return GetNetworkPath(AppServerMachineName, absoluteLocalPath);
    }

    public string GetSchedulerServerNetworkPath(string schedulerServerMachineName, string absoluteLocalPath)
    {
      Guard.NotNullNorEmpty(absoluteLocalPath, "absoluteLocalPath");

      return GetNetworkPath(schedulerServerMachineName, absoluteLocalPath);
    }

    public string GetWebServerNetworkPath(string webServerMachineName, string absoluteLocalPath)
    {
      Guard.NotNullNorEmpty(webServerMachineName, "webServerMachineName");
      Guard.NotNullNorEmpty(absoluteLocalPath, "absoluteLocalPath");

      return GetNetworkPath(webServerMachineName, absoluteLocalPath);
    }

    public EnvironmentUser GetEnvironmentUser(string userId)
    {
      Guard.NotNullNorEmpty(userId, "userId");

      EnvironmentUser environmentUser;

      if (_environmentUsersByIdDict.TryGetValue(userId, out environmentUser))
      {
        return environmentUser;
      }

      throw new InvalidOperationException(string.Format("There's no environment user with id '{0}' defined in environment named '{1}'.", userId, Name));
    }

    public string GetFailoverClusterGroupNameForProject(string projectName)
    {
      Guard.NotNullNorEmpty(projectName, "projectName");

      ProjectToFailoverClusterGroupMapping mapping;

      if (_projectToFailoverClusterGroupMappingsDict.TryGetValue(projectName, out mapping))
      {
        return mapping.ClusterGroupName;
      }

      return null;
    }

    public WebAppProjectConfiguration GetWebAppProjectConfiguration(WebAppProjectInfo webAppProjectInfo)
    {
      Guard.NotNull(webAppProjectInfo, "webAppProjectInfo");

      string projectName = webAppProjectInfo.Name;
      string appPoolId = webAppProjectInfo.AppPoolId;
      string webSiteName = webAppProjectInfo.WebSiteName;
      string webAppDirName = webAppProjectInfo.WebAppDirName;
      string webAppName = webAppProjectInfo.WebAppName;

      WebAppProjectConfigurationOverride webAppProjectConfigurationOverride =
        FindWebAppProjectConfigurationOverride(projectName);

      if (webAppProjectConfigurationOverride != null)
      {
        if (webAppProjectConfigurationOverride.AppPoolId != null)
        {
          appPoolId = webAppProjectConfigurationOverride.AppPoolId;
        }

        if (webAppProjectConfigurationOverride.WebSiteName != null)
        {
          webSiteName = webAppProjectConfigurationOverride.WebSiteName;
        }

        if (webAppProjectConfigurationOverride.WebAppDirName != null)
        {
          webAppDirName = webAppProjectConfigurationOverride.WebAppDirName;
        }

        if (webAppProjectConfigurationOverride.WebAppName != null)
        {
          webAppName = webAppProjectConfigurationOverride.WebAppName;
        }
      }

      return
        new WebAppProjectConfiguration(
          projectName,
          appPoolId,
          webSiteName,
          webAppDirName,
          webAppName);
    }

    public DbProjectConfiguration GetDbProjectConfiguration(DbProjectInfo dbProjectInfo)
    {
      Guard.NotNull(dbProjectInfo, "dbProjectInfo");

      string projectName = dbProjectInfo.Name;
      string databaseServerId = dbProjectInfo.DatabaseServerId;

      DbProjectConfigurationOverride dbProjectConfigurationOverride =
        FindDbProjectConfigurationOverride(projectName);

      if (dbProjectConfigurationOverride != null)
      {
        if (!string.IsNullOrEmpty(databaseServerId))
        {
          databaseServerId = dbProjectConfigurationOverride.DatabaseServerId;
        }
      }

      return
        new DbProjectConfiguration(
          projectName,
          databaseServerId);
    }    

    public IisAppPoolInfo GetAppPoolInfo(string appPoolId)
    {
      Guard.NotNullNorEmpty(appPoolId, "appPoolId");

      IisAppPoolInfo iisAppPoolInfo;

      if (_appPoolInfosByNameDict.TryGetValue(appPoolId, out iisAppPoolInfo))
      {
        return iisAppPoolInfo;
      }

      throw new ArgumentException(string.Format("App pool with id '{0}' is not defined for environment '{1}'.", appPoolId, Name));
    }

    public DatabaseServer GetDatabaseServer(string databaseServerId)
    {
      Guard.NotNullNorEmpty(databaseServerId, "databaseServerId");

      DatabaseServer databaseServer;

      if (_databaseServersByIdDict.TryGetValue(databaseServerId, out databaseServer))
      {
        return databaseServer;
      }

      throw new ArgumentException(string.Format("Database server with id '{0}' is not defined for environment '{1}'.", databaseServerId, Name));
    }

    private WebAppProjectConfigurationOverride FindWebAppProjectConfigurationOverride(string projectName)
    {
      Guard.NotNullNorEmpty(projectName, "projectName");

      WebAppProjectConfigurationOverride configuration;

      return
        _webAppProjectConfigurationOverridesDict.TryGetValue(projectName, out configuration)
          ? configuration
          : null;
    }

    private DbProjectConfigurationOverride FindDbProjectConfigurationOverride(string projectName)
    {
      Guard.NotNullNorEmpty(projectName, "projectName");

      DbProjectConfigurationOverride configurationOverride;

      return
        _dbProjectConfigurationOverridesDict.TryGetValue(projectName, out configurationOverride)
          ? configurationOverride
          : null;
    }

    public CustomEnvMachine GetCustomEnvMachine(string customEnvMachineId)
    {
      Guard.NotNullNorEmpty(customEnvMachineId, "customEnvMachineId");

      CustomEnvMachine customEnvMachine;

      if (_customEnvMachinesDict.TryGetValue(customEnvMachineId, out customEnvMachine))
      {
        return customEnvMachine;
      }

      throw new ArgumentException(string.Format("Custom environment machine with id '{0}' is not defined for environment '{1}'.", customEnvMachineId, Name));
    }

    public string Name { get; private set; }

    public bool IsVisibleToClients { get; private set; }

    public string ConfigurationTemplateName { get; private set; }

    public string AppServerMachineName { get; private set; }

    public string FailoverClusterMachineName { get; private set; }

    public IEnumerable<string> WebServerMachineNames
    {
      get { return _webServerMachines.AsReadOnly(); }
    }

    public IEnumerable<TerminalServerMachine> TerminalServerMachines { get; private set; }

    public IEnumerable<string> SchedulerServerTasksMachineNames
    {
      get { return _schedulerServerTasksMachineNames.AsReadOnly(); }
    }

    public IEnumerable<string> SchedulerServerBinariesMachineNames
    {
      get { return _schedulerServerBinariesMachineNames.AsReadOnly(); }
    }

    public string NtServicesBaseDirPath { get; private set; }

    public string WebAppsBaseDirPath { get; private set; }

    public string SchedulerAppsBaseDirPath { get; private set; }

    public bool EnableFailoverClusteringForNtServices { get; private set; }

    public string ManualDeploymentPackageDirPath { get; private set; }

    public string DomainName { get; private set; }

    public IEnumerable<EnvironmentUser> EnvironmentUsers
    {
      get { return _environmentUsersByIdDict.Values; }
    }

    public IEnumerable<IisAppPoolInfo> AppPoolInfos
    {
      get { return _appPoolInfosByNameDict.Values; }
    }

    public IEnumerable<DatabaseServer> DatabaseServers
    {
      get { return _databaseServersByIdDict.Values; }
    }

    public IEnumerable<ProjectToFailoverClusterGroupMapping> ProjectToFailoverClusterGroupMappings
    {
      get { return _projectToFailoverClusterGroupMappingsDict.Values; }
    }

    public IEnumerable<WebAppProjectConfigurationOverride> WebAppProjectConfigurationOverrides
    {
      get { return _webAppProjectConfigurationOverridesDict.Values; }
    }

    public IEnumerable<DbProjectConfigurationOverride> DbProjectConfigurationOverrides
    {
      get { return _dbProjectConfigurationOverridesDict.Values; }
    }

    public IEnumerable<CustomEnvMachine> CustomEnvMachines
    {
      get { return _customEnvMachinesDict.Values; }
    }
  }
}
 
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UberDeployer.Core.DataAccess.Xml.EnvironmentInfo.XmlModel;
using UberDeployer.Core.Domain;

namespace UberDeployer.Core.DataAccess.Xml.EnvironmentInfo
{
  public class XmlEnvironmentInfoRepository : IEnvironmentInfoRepository
  {
    private readonly string _xmlFilesDirPath;

    private Dictionary<string, Domain.EnvironmentInfo> _environmentInfosByName;

    public XmlEnvironmentInfoRepository(string xmlFilesDirPath)
    {
      if (string.IsNullOrEmpty(xmlFilesDirPath))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "xmlFilesDirPath");
      }

      _xmlFilesDirPath = xmlFilesDirPath;
    }

    public IEnumerable<Domain.EnvironmentInfo> GetAll()
    {
      LoadXmlFilesIfNeeded();

      return
        _environmentInfosByName.Values
          .OrderBy(ei => ei.Name);
    }

    public Domain.EnvironmentInfo FindByName(string environmentName)
    {
      if (string.IsNullOrEmpty(environmentName))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "environmentName");
      }

      LoadXmlFilesIfNeeded();

      Domain.EnvironmentInfo environmentInfo;

      if (!_environmentInfosByName.TryGetValue(environmentName, out environmentInfo))
      {
        return null;
      }

      return environmentInfo;
    }

    private void LoadXmlFilesIfNeeded()
    {
      if (_environmentInfosByName != null)
      {
        return;
      }

      _environmentInfosByName = new Dictionary<string, Domain.EnvironmentInfo>();

      var xmlSerializer = new XmlSerializer(typeof(EnvironmentInfoXml));

      foreach (string xmlFilePath in Directory.GetFiles(_xmlFilesDirPath, "EnvironmentInfo_*.xml", SearchOption.TopDirectoryOnly))
      {
        EnvironmentInfoXml environmentInfoXml;

        using (var fs = File.OpenRead(xmlFilePath))
        {
          environmentInfoXml = (EnvironmentInfoXml)xmlSerializer.Deserialize(fs);
        }

        Domain.EnvironmentInfo environmentInfo =
          ConvertToEnvironmentInfo(environmentInfoXml);

        _environmentInfosByName.Add(
          environmentInfo.Name,
          environmentInfo);
      }
    }

    private static Domain.EnvironmentInfo ConvertToEnvironmentInfo(EnvironmentInfoXml environmentInfoXml)
    {
      return
        new Domain.EnvironmentInfo(
          environmentInfoXml.Name,
          environmentInfoXml.IsVisibleToClients,
          environmentInfoXml.ConfigurationTemplateName,
          environmentInfoXml.AppServerMachineName,
          environmentInfoXml.FailoverClusterMachineName,
          environmentInfoXml.WebServerMachineNames,
          environmentInfoXml.TerminalServerMachines.Select(
            x => new TerminalServerMachine(
              x.MachineName,
              x.AppsBaseDirPath,
              x.AppsShortcutFolder)
          ),
          environmentInfoXml.SchedulerServerTasksMachineNames,
          environmentInfoXml.SchedulerServerBinariesMachineNames,
          environmentInfoXml.NtServicesBaseDirPath,
          environmentInfoXml.WebAppsBaseDirPath,
          environmentInfoXml.SchedulerAppsBaseDirPath,
          environmentInfoXml.EnableFailoverClusteringForNtServices,
          environmentInfoXml.EnvironmentUsers.Select(
            e =>
              new EnvironmentUser(
                e.Id,
                e.UserName)),
          environmentInfoXml.AppPoolInfos.Select(
            e =>
              new IisAppPoolInfo(
                e.Name,
                e.Version,
                e.Mode)),
          environmentInfoXml.DatabaseServers.Select(
            e =>
              new DatabaseServer(
                e.Id,
                e.MachineName,
                e.DataDirPath,
                e.LogDirPath,
                ConvertSqlPakcageVariables(e.SqlPackageVariables))),
          environmentInfoXml.ProjectToFailoverClusterGroupMappings.Select(
            e =>
              new ProjectToFailoverClusterGroupMapping(
                e.ProjectName,
                e.ClusterGroupName)),
          environmentInfoXml.WebAppProjectConfigurationOverrides.Select(
            e =>
              new WebAppProjectConfigurationOverride(
                e.ProjectName,
                e.AppPoolId,
                e.WebSiteName,
                e.WebAppDirName,
                e.WebAppName)),
          environmentInfoXml.DbProjectConfigurationOverrides.Select(
            e =>
              new DbProjectConfigurationOverride(
                e.ProjectName,
                e.DatabaseServerId)),
          environmentInfoXml.ManualDeploymentPackageDirPath,
          environmentInfoXml.DomainName,
          environmentInfoXml.CustomEnvMachines.Select(
            e =>
              new CustomEnvMachine(
                e.Id,
                e.MachineName)));
    }

    private static Dictionary<string, string> ConvertSqlPakcageVariables(IEnumerable<Variable> sqlPackageVariables)
    {
      if (sqlPackageVariables == null)
      {
        return null;
      }

      return sqlPackageVariables.ToDictionary(x => x.Name, x => x.Value);
    }
  }
}

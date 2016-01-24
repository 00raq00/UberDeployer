using System.Collections.Generic;

namespace UberDeployer.Core.DataAccess.Xml.EnvironmentInfo.XmlModel
{
  public class EnvironmentInfoXml
  {
    public string Name { get; set; }

    public bool IsVisibleToClients { get; set; }

    public string ConfigurationTemplateName { get; set; }

    public string AppServerMachineName { get; set; }

    public string FailoverClusterMachineName { get; set; }

    public List<string> WebServerMachineNames { get; set; }

    public List<TerminalServerMachineXml> TerminalServerMachines { get; set; }

    public List<string> SchedulerServerTasksMachineNames { get; set; }

    public List<string> SchedulerServerBinariesMachineNames { get; set; }

    public string NtServicesBaseDirPath { get; set; }

    public string WebAppsBaseDirPath { get; set; }

    public string SchedulerAppsBaseDirPath { get; set; }

    public bool EnableFailoverClusteringForNtServices { get; set; }

    public List<EnvironmentUserXml> EnvironmentUsers { get; set; }

    public List<AppPoolInfoXml> AppPoolInfos { get; set; }

    public List<DatabaseServerXml> DatabaseServers { get; set; }

    public List<WebAppProjectConfigurationOverrideXml> WebAppProjectConfigurationOverrides { get; set; }

    public List<ProjectToFailoverClusterGroupMappingXml> ProjectToFailoverClusterGroupMappings { get; set; }

    public List<DbProjectConfigurationOverrideXml> DbProjectConfigurationOverrides { get; set; }

    public string ManualDeploymentPackageDirPath { get; set; }

    public string DomainName { get; set; }

    public List<CustomEnvMachineXml> CustomEnvMachines { get; set; }
  }
}

using System.Collections.Generic;

using UberDeployer.Core.Domain;

namespace UberDeployer.Tests.Core.Generators
{
  public class DeploymentDataGenerator
  {
    public static EnvironmentInfo GetEnvironmentInfo()
    {
      return GetEnvironmentInfo(new[] { new EnvironmentUser("id", "user_name"), });
    }

    public static EnvironmentInfo GetClusteredEnvironmentInfo()
    {
      return GetClusteredEnvironmentInfo(new[] { new EnvironmentUser("id", "user_name"), });
    }

    public static EnvironmentInfo GetEnvironmentInfo(IEnumerable<EnvironmentUser> users)
    {
      return
        new EnvironmentInfo(
          "env_name",
          true,
          "config_template_name",
          "app_server_machine_name",
          "failover_cluster_machine_name",
          new[] { "web_server_machine_name" },
          TestData.TerminalServerMachines,
          new[] { "schedulerServerTasksMachineName1", "schedulerServerTasksMachineName2", },
          new[] { "schedulerServerBinariesMachineName1", "schedulerServerBinariesMachineName2", },
          "nt_service_base_dir_path",
          "web_apps_base_dir_path",
          "X:\\scheduler_apps_base_dir_path",
          false,
          users,
          TestData.AppPoolInfos,
          TestData.DatabaseServers,
          TestData.ProjectToFailoverClusterGroupMappings,
          TestData.WebAppProjectConfigurationOverrides,
          TestData.DbProjectConfigurationOverrides,
          "X:\\artifacts_deployment_dir_path",
          "domain-name",
          TestData.CustomEnvMachines);
    }

    public static EnvironmentInfo GetClusteredEnvironmentInfo(IEnumerable<EnvironmentUser> users)
    {
      return
        new EnvironmentInfo(
          "env_name",
          true,
          "config_template_name",
          "app_server_machine_name",
          "failover_cluster_machine_name",
          new[] { "web_server_machine_name" },
          TestData.TerminalServerMachines,
          new[] { "schedulerServerTasksMachineName1", "schedulerServerTasksMachineName2", },
          new[] { "schedulerServerBinariesMachineName1", "schedulerServerBinariesMachineName2", },
          "nt_service_base_dir_path",
          "web_apps_base_dir_path",
          "X:\\scheduler_apps_base_dir_path",
          true,
          users,
          TestData.AppPoolInfos,
          TestData.DatabaseServers,
          TestData.ProjectToFailoverClusterGroupMappings,
          TestData.WebAppProjectConfigurationOverrides,
          TestData.DbProjectConfigurationOverrides,
          "X:\\artifacts_deployment_dir_path",
          "domain-name",
          TestData.CustomEnvMachines);
    }


    public static ProjectInfo GetTerminalAppProjectInfo()
    {
      return new TerminalAppProjectInfo("project_name", "artifactsRepositoryName", new[] { "env_name" }, "artifactsRepositoryDirName", false, "terminalAppName", "terminalAppDirName", "terminalAppExeName");
    }

    public static ExtensionProjectInfo GetExtensionProjectInfo()
    {
      return new ExtensionProjectInfo("project_name", "artifactsRepositoryName", new[] { "env_name" }, "artifactsRepositoryDirName", false, "extendedProjectName");
    }

    public static NtServiceProjectInfo GetNtServiceProjectInfo()
    {
      return new NtServiceProjectInfo("project_name", "artifactsRepositoryName", new[] { "env_name" }, "artifactsRepositoryDirName", false, "ntsvcprj", "ntServiceDirName", "ntServiceDisplayName", "ntServiceExeName", "ntServiceUserId", "extensionsDirName");
    }
  }
}

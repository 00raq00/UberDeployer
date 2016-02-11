using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UberDeployer.Core.DataAccess.Xml.ProjectInfo.XmlModel;
using UberDeployer.Core.Domain;

namespace UberDeployer.Core.DataAccess.Xml.ProjectInfo
{
  public class XmlProjectInfoRepository : IProjectInfoRepository
  {
    private readonly string _xmlFilePath;

    private ProjectInfosXml _projectInfosXml;
    private Dictionary<string, Domain.ProjectInfo> _projectInfosByName;

    public XmlProjectInfoRepository(string xmlFilePath)
    {
      if (string.IsNullOrEmpty(xmlFilePath))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "xmlFilePath");
      }

      _xmlFilePath = xmlFilePath;
    }

    public IEnumerable<Domain.ProjectInfo> GetAll()
    {
      LoadXmlIfNeeded();

      return
        _projectInfosByName.Values
          .OrderBy(pi => pi.Name);
    }

    public Domain.ProjectInfo FindByName(string name)
    {
      if (string.IsNullOrEmpty(name))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "name");
      }

      LoadXmlIfNeeded();

      Domain.ProjectInfo projectInfo;

      if (!_projectInfosByName.TryGetValue(name, out projectInfo))
      {
        return null;
      }

      return projectInfo;
    }

    public List<Domain.ProjectInfo> FindProjectNameWithDependencies(string name)
    {
      return FindProjectNameWithDependencies(FindByName(name));
    }

    public List<Domain.ProjectInfo> FindProjectNameWithDependencies(Domain.ProjectInfo projectInfo)
    {
      return FindProjectNameWithDependencies(projectInfo, new List<string>());
    }

    public List<Domain.ProjectInfo> CreateDependentProjects(string name)
    {
      // TODO MARIO: Move dependency resolving from repo to separate class
      return CreateDependentProjects(FindByName(name));
    }

    public List<Domain.ProjectInfo> CreateDependentProjects(Domain.ProjectInfo info)
    {
      var output = FindProjectNameWithDependencies(info);
      output.Remove(info);
      return output;
    }

    private List<Domain.ProjectInfo> FindProjectNameWithDependencies(Domain.ProjectInfo projectInfo, List<string> usedProjectNames)
    {
      if (projectInfo == null)
        return new List<Domain.ProjectInfo>();

      var output = new List<Domain.ProjectInfo>();

      foreach (var project in projectInfo.DependendProjectNames.Where(x => !usedProjectNames.Contains(x)))
      {
        usedProjectNames.Add(project);
        output.AddRange(FindProjectNameWithDependencies(FindByName(project), usedProjectNames));
      }

      if (!output.Contains(projectInfo))
      {
        output.Add(projectInfo);
        usedProjectNames.Add(projectInfo.Name);
      }

      return output.ToList();
    }

    private static Domain.ProjectInfo CreateProjectInfo(ProjectInfoXml projectInfoXml)
    {
      List<string> allowedEnvironmentNames =
        (projectInfoXml.AllowedEnvironments ?? "")
          .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
          .ToList();

      var uberDeployerAgentProjectInfoXml = projectInfoXml as UberDeployerAgentProjectInfoXml;

      if (uberDeployerAgentProjectInfoXml != null)
      {
        return
          new UberDeployerAgentProjectInfo(
            uberDeployerAgentProjectInfoXml.Name,
            uberDeployerAgentProjectInfoXml.ArtifactsRepositoryName,
            allowedEnvironmentNames,
            uberDeployerAgentProjectInfoXml.ArtifactsRepositoryDirName,
            uberDeployerAgentProjectInfoXml.ArtifactsAreNotEnvironmentSpecific,
            uberDeployerAgentProjectInfoXml.NtServiceName,
            uberDeployerAgentProjectInfoXml.NtServiceDirName,
            uberDeployerAgentProjectInfoXml.NtServiceDisplayName,
            uberDeployerAgentProjectInfoXml.NtServiceExeName,
            uberDeployerAgentProjectInfoXml.NtServiceUserId);
      }

      var ntServiceProjectInfoXml = projectInfoXml as NtServiceProjectInfoXml;

      if (ntServiceProjectInfoXml != null)
      {
        return
          new NtServiceProjectInfo(
            ntServiceProjectInfoXml.Name,
            ntServiceProjectInfoXml.ArtifactsRepositoryName,
            allowedEnvironmentNames,
            ntServiceProjectInfoXml.ArtifactsRepositoryDirName,
            ntServiceProjectInfoXml.ArtifactsAreNotEnvironmentSpecific,
            ntServiceProjectInfoXml.NtServiceName,
            ntServiceProjectInfoXml.NtServiceDirName,
            ntServiceProjectInfoXml.NtServiceDisplayName,
            ntServiceProjectInfoXml.NtServiceExeName,
            ntServiceProjectInfoXml.NtServiceUserId,
            ntServiceProjectInfoXml.ExtensionsDirName,
            ntServiceProjectInfoXml.DependentProjects);
      }

      var webAppProjectInfoXml = projectInfoXml as WebAppProjectInfoXml;

      if (webAppProjectInfoXml != null)
      {
        return
          new WebAppProjectInfo(
            webAppProjectInfoXml.Name,
            webAppProjectInfoXml.ArtifactsRepositoryName,
            allowedEnvironmentNames,
            webAppProjectInfoXml.ArtifactsRepositoryDirName,
            webAppProjectInfoXml.ArtifactsAreNotEnvironmentSpecific,
            webAppProjectInfoXml.AppPoolId,
            webAppProjectInfoXml.WebSiteName,
            webAppProjectInfoXml.WebAppDirName,
            webAppProjectInfoXml.WebAppName,
            webAppProjectInfoXml.DependentProjects);
      }

      var schedulerAppProjectInfoXml = projectInfoXml as SchedulerAppProjectInfoXml;

      if (schedulerAppProjectInfoXml != null)
      {
        return
          new SchedulerAppProjectInfo(
            schedulerAppProjectInfoXml.Name,
            schedulerAppProjectInfoXml.ArtifactsRepositoryName,
            allowedEnvironmentNames,
            schedulerAppProjectInfoXml.ArtifactsRepositoryDirName,
            schedulerAppProjectInfoXml.ArtifactsAreNotEnvironmentSpecific,
            schedulerAppProjectInfoXml.SchedulerAppDirName,
            schedulerAppProjectInfoXml.SchedulerAppExeName,
            schedulerAppProjectInfoXml.SchedulerAppTasks
              .Select(
                x =>
                  new SchedulerAppTask(
                    x.Name,
                    x.ExecutableName,
                    x.UserId,
                    x.ScheduledHour,
                    x.ScheduledMinute,
                    x.ExecutionTimeLimitInMinutes,
                    x.Repetition.Enabled
                      ? Repetition.CreateEnabled(
                        TimeSpan.Parse(x.Repetition.Interval),
                        TimeSpan.Parse(x.Repetition.Duration),
                        x.Repetition.StopAtDurationEnd)
                      : Repetition.CreatedDisabled())),
                      schedulerAppProjectInfoXml.DependentProjects);
      }

      var terminalAppProjectInfoXml = projectInfoXml as TerminalAppProjectInfoXml;

      if (terminalAppProjectInfoXml != null)
      {
        return
          new TerminalAppProjectInfo(
            terminalAppProjectInfoXml.Name,
            terminalAppProjectInfoXml.ArtifactsRepositoryName,
            allowedEnvironmentNames,
            terminalAppProjectInfoXml.ArtifactsRepositoryDirName,
            terminalAppProjectInfoXml.ArtifactsAreNotEnvironmentSpecific,
            terminalAppProjectInfoXml.TerminalAppName,
            terminalAppProjectInfoXml.TerminalAppDirName,
            terminalAppProjectInfoXml.TerminalAppExeName,
            terminalAppProjectInfoXml.DependentProjects);
      }

      var dbProjectInfoXml = projectInfoXml as DbProjectInfoXml;

      if (dbProjectInfoXml != null)
      {
        return
          new DbProjectInfo(
            dbProjectInfoXml.Name,
            dbProjectInfoXml.ArtifactsRepositoryName,
            allowedEnvironmentNames,
            dbProjectInfoXml.ArtifactsRepositoryDirName,
            dbProjectInfoXml.ArtifactsAreNotEnvironmentSpecific,
            dbProjectInfoXml.DbName,
            dbProjectInfoXml.DatabaseServerId,
            dbProjectInfoXml.IsTransacional,
            dbProjectInfoXml.DacpacFile,
            dbProjectInfoXml.Users,
            dbProjectInfoXml.DependentProjects);
      }

      var extensionProjectXml = projectInfoXml as ExtensionProjectInfoXml;

      if (extensionProjectXml != null)
      {
        return
          new ExtensionProjectInfo(
            extensionProjectXml.Name,
            extensionProjectXml.ArtifactsRepositoryName,
            allowedEnvironmentNames,
            extensionProjectXml.ArtifactsRepositoryDirName,
            true,
            extensionProjectXml.ExtendedProjectName,
            extensionProjectXml.DependentProjects);
      }

      var powerShellScriptProjectInfoXml = projectInfoXml as PowerShellScriptProjectInfoXml;

      if (powerShellScriptProjectInfoXml != null)
      {
        return
          new PowerShellScriptProjectInfo(
            powerShellScriptProjectInfoXml.Name,
            powerShellScriptProjectInfoXml.ArtifactsRepositoryName,
            allowedEnvironmentNames,
            powerShellScriptProjectInfoXml.ArtifactsRepositoryDirName,
            powerShellScriptProjectInfoXml.ArtifactsAreNotEnvironmentSpecific,
            ConvertTargetMachine(powerShellScriptProjectInfoXml.TargetMachine),
            powerShellScriptProjectInfoXml.ScriptName,
            powerShellScriptProjectInfoXml.IsRemote,
            powerShellScriptProjectInfoXml.DependentProjects);
      }

      throw new NotSupportedException(string.Format("Project type '{0}' is not supported.", projectInfoXml.GetType()));
    }

    private static TargetMachine ConvertTargetMachine(TargetMachineXml targetMachine)
    {
      if (targetMachine == null)
      {
        return null;
      }

      if (targetMachine is AppServerTargetMachineXml)
      {
        return new AppServerTargetMachine();
      }

      if (targetMachine is WebServerTargetMachinesXml)
      {
        return new WebServerTargetMachines();
      }

      if (targetMachine is TerminalServerTargetMachineXml)
      {
        return new TerminalServerTargetMachine();
      }

      if (targetMachine is SchedulerServerTargetMachinesXml)
      {
        return new SchedulerServerTargetMachines();
      }

      var databaseServerTargetMachineXml = targetMachine as DatabaseServerTargetMachineXml;
      if (databaseServerTargetMachineXml != null)
      {
        return new DatabaseServerTargetMachine
        {
          DatabaseServerId = databaseServerTargetMachineXml.DatabaseServerId
        };
      }

      var customEnvTargetMachineXml = targetMachine as CustomEnvTargetMachineXml;
      if (customEnvTargetMachineXml != null)
      {
        return new CustomEnvTargetMachine
        {
          CustomEnvMachineId = customEnvTargetMachineXml.CustomEnvMachineId,
        };
      }

      throw new NotSupportedException(string.Format("TargetMachine with type [{0}] is not supported", targetMachine.GetType().FullName));
    }

    private void LoadXmlIfNeeded()
    {
      if (_projectInfosXml != null)
      {
        return;
      }

      var xmlSerializer = new XmlSerializer(typeof(ProjectInfosXml));

      using (var fs = File.OpenRead(_xmlFilePath))
      {
        _projectInfosXml = (ProjectInfosXml)xmlSerializer.Deserialize(fs);
      }

      _projectInfosByName =
        _projectInfosXml.ProjectInfos
          .Select(CreateProjectInfo)
          .ToDictionary(pi => pi.Name);
    }
  }
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading;

using log4net;

using UberDeployer.Agent.Proxy;
using UberDeployer.Agent.Proxy.Dto.TeamCity;
using UberDeployer.Agent.Proxy.Faults;
using UberDeployer.Agent.Service.Diagnostics;
using UberDeployer.Common;
using UberDeployer.Common.SyntaxSugar;
using UberDeployer.CommonConfiguration;
using UberDeployer.Core.Configuration;
using UberDeployer.Core.Deployment;
using UberDeployer.Core.Deployment.Pipeline;
using UberDeployer.Core.Deployment.Pipeline.Modules;
using UberDeployer.Core.Deployment.Steps;
using UberDeployer.Core.Deployment.Tasks;
using UberDeployer.Core.Domain;
using UberDeployer.Core.Management.Metadata;
using UberDeployer.Core.TeamCity;
using UberDeployer.Core.TeamCity.ApiModels;

using DbScriptsToRunSelection = UberDeployer.Core.Deployment.DbScriptsToRunSelection;
using DeploymentInfo = UberDeployer.Agent.Proxy.Dto.DeploymentInfo;
using DeploymentRequest = UberDeployer.Core.Deployment.Pipeline.Modules.DeploymentRequest;
using DiagnosticMessage = UberDeployer.Core.Deployment.DiagnosticMessage;
using DiagnosticMessageType = UberDeployer.Core.Deployment.DiagnosticMessageType;
using EnvironmentInfo = UberDeployer.Core.Domain.EnvironmentInfo;
using MachineSpecificProjectVersion = UberDeployer.Agent.Proxy.Dto.Metadata.MachineSpecificProjectVersion;
using ProjectInfo = UberDeployer.Core.Domain.ProjectInfo;
using UberDeployerAgentProjectInfo = UberDeployer.Core.Domain.UberDeployerAgentProjectInfo;
using WebAppProjectInfo = UberDeployer.Core.Domain.WebAppProjectInfo;

namespace UberDeployer.Agent.Service
{
  public class AgentService : IAgentService
  {
    private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private readonly IDeploymentPipeline _deploymentPipeline;

    private readonly IProjectInfoRepository _projectInfoRepository;

    private readonly IEnvironmentInfoRepository _environmentInfoRepository;

    private readonly ITeamCityRestClient _teamCityClient;

    private readonly IDeploymentRequestRepository _deploymentRequestRepository;

    private readonly IDiagnosticMessagesLogger _diagnosticMessagesLogger;

    private readonly IProjectMetadataExplorer _projectMetadataExplorer;

    private readonly IDirPathParamsResolver _dirPathParamsResolver;

    private readonly IApplicationConfiguration _applicationConfiguration;

    public AgentService(
      IDeploymentPipeline deploymentPipeline,
      IProjectInfoRepository projectInfoRepository,
      IEnvironmentInfoRepository environmentInfoRepository,
      ITeamCityRestClient teamCityClient,
      IDeploymentRequestRepository deploymentRequestRepository,
      IDiagnosticMessagesLogger diagnosticMessagesLogger,
      IProjectMetadataExplorer projectMetadataExplorer,
      IDirPathParamsResolver dirPathParamsResolver, 
      IApplicationConfiguration applicationConfiguration)
    {
      Guard.NotNull(deploymentPipeline, "deploymentPipeline");
      Guard.NotNull(projectInfoRepository, "projectInfoRepository");
      Guard.NotNull(environmentInfoRepository, "environmentInfoRepository");
      Guard.NotNull(teamCityClient, "teamCityClient");
      Guard.NotNull(deploymentRequestRepository, "deploymentRequestRepository");
      Guard.NotNull(diagnosticMessagesLogger, "diagnosticMessagesLogger");
      Guard.NotNull(dirPathParamsResolver, "dirPathParamsResolver");
      Guard.NotNull(applicationConfiguration, "applicationConfiguration");

      _projectInfoRepository = projectInfoRepository;
      _environmentInfoRepository = environmentInfoRepository;
      _teamCityClient = teamCityClient;
      _deploymentPipeline = deploymentPipeline;
      _deploymentRequestRepository = deploymentRequestRepository;
      _diagnosticMessagesLogger = diagnosticMessagesLogger;
      _projectMetadataExplorer = projectMetadataExplorer;
      _dirPathParamsResolver = dirPathParamsResolver;
      _applicationConfiguration = applicationConfiguration;
    }

    public AgentService()
      : this(
        ObjectFactory.Instance.CreateDeploymentPipeline(),
        ObjectFactory.Instance.CreateProjectInfoRepository(),
        ObjectFactory.Instance.CreateEnvironmentInfoRepository(),
        ObjectFactory.Instance.CreateTeamCityRestClient(),
        ObjectFactory.Instance.CreateDeploymentRequestRepository(),
        InMemoryDiagnosticMessagesLogger.Instance,
        ObjectFactory.Instance.CreateProjectMetadataExplorer(),
        ObjectFactory.Instance.CreateDirPathParamsResolver(), 
        ObjectFactory.Instance.CreateApplicationConfiguration())
    {
    }

    public void Deploy(Guid deploymentId, Guid uniqueClientId, string requesterIdentity, DeploymentInfo deploymentInfo)
    {
      try
      {
        Guard.NotEmpty(deploymentId, "deploymentId");
        Guard.NotEmpty(uniqueClientId, "uniqueClientId");
        Guard.NotNullNorEmpty(requesterIdentity, "requesterIdentity");
        Guard.NotNull(deploymentInfo, "DeploymentInfo");

        ProjectInfo projectInfo =
          _projectInfoRepository.FindByName(deploymentInfo.ProjectName);

        if (projectInfo == null)
        {
          throw new FaultException<ProjectNotFoundFault>(new ProjectNotFoundFault { ProjectName = deploymentInfo.ProjectName });
        }

        DoDeploy(uniqueClientId, requesterIdentity, deploymentInfo, projectInfo);
      }
      catch (Exception exc)
      {
        HandleDeploymentException(exc, uniqueClientId);
      }
    }

    public void DeployAsync(Guid deploymentId, Guid uniqueClientId, string requesterIdentity, DeploymentInfo deploymentInfo)
    {
      try
      {
        Guard.NotEmpty(deploymentId, "deploymentId");
        Guard.NotEmpty(uniqueClientId, "uniqueClientId");
        Guard.NotNullNorEmpty(requesterIdentity, "requesterIdentity");
        Guard.NotNull(deploymentInfo, "deploymentInfo");

        ProjectInfo projectInfo =
          _projectInfoRepository.FindByName(deploymentInfo.ProjectName);

        if (projectInfo == null)
        {
          throw new FaultException<ProjectNotFoundFault>(new ProjectNotFoundFault { ProjectName = deploymentInfo.ProjectName });
        }

        ThreadPool.QueueUserWorkItem(
          state =>
          {
            try
            {
              DoDeploy(uniqueClientId, requesterIdentity, deploymentInfo, projectInfo);
            }
            catch (Exception exc)
            {
              HandleDeploymentException(exc, uniqueClientId);
            }
          });
      }
      catch (Exception exc)
      {
        HandleDeploymentException(exc, uniqueClientId);
      }
    }

    public void CreatePackageAsync(Guid deploymentId, Guid uniqueClientId, string requesterIdentity, DeploymentInfo deploymentInfo, string packageDirPath)
    {
      try
      {
        Guard.NotEmpty(deploymentId, "deploymentId");
        Guard.NotEmpty(uniqueClientId, "uniqueClientId");
        Guard.NotNullNorEmpty(requesterIdentity, "requesterIdentity");
        Guard.NotNull(deploymentInfo, "deploymentInfo");

        ProjectInfo projectInfo =
          _projectInfoRepository.FindByName(deploymentInfo.ProjectName);

        if (projectInfo == null)
        {
          throw new FaultException<ProjectNotFoundFault>(new ProjectNotFoundFault { ProjectName = deploymentInfo.ProjectName });
        }

        ThreadPool.QueueUserWorkItem(
          state =>
          {
            try
            {
              DoCreatePackage(uniqueClientId, requesterIdentity, deploymentInfo, projectInfo, packageDirPath);
            }
            catch (Exception exc)
            {
              HandleDeploymentException(exc, uniqueClientId);
            }
          });
      }
      catch (Exception exc)
      {
        HandleDeploymentException(exc, uniqueClientId);
      }
    }

    public List<Proxy.Dto.ProjectInfo> GetProjectInfos(Proxy.Dto.ProjectFilter projectFilter)
    {
      if (projectFilter == null)
      {
        throw new ArgumentNullException("projectFilter");
      }

      IEnumerable<ProjectInfo> projectInfos =
        _projectInfoRepository.GetAll();

      if (!string.IsNullOrEmpty(projectFilter.Name))
      {
        projectInfos =
          projectInfos
            .Where(pi => !string.IsNullOrEmpty(pi.Name) && pi.Name.IndexOf(projectFilter.Name, StringComparison.CurrentCultureIgnoreCase) > -1);
      }

      return
        projectInfos
          .Select(DtoMapper.Map<ProjectInfo, Proxy.Dto.ProjectInfo>)
          .ToList();
    }

    public List<Proxy.Dto.EnvironmentInfo> GetEnvironmentInfos()
    {
      IEnumerable<EnvironmentInfo> environmentInfos =
        _environmentInfoRepository.GetAll();

      return
        environmentInfos
          .Where(x => x.IsVisibleToClients)
          .Select(DtoMapper.Map<EnvironmentInfo, Proxy.Dto.EnvironmentInfo>)
          .ToList();
    }

    public List<string> GetWebMachineNames(string environmentName)
    {
      if (string.IsNullOrEmpty(environmentName))
      {
        throw new ArgumentException("Environment name can't be null or empty", "environmentName");
      }

      EnvironmentInfo environmentInfo = _environmentInfoRepository.FindByName(environmentName);

      if (environmentInfo == null)
      {
        throw new FaultException<EnvironmentNotFoundFault>(
          new EnvironmentNotFoundFault
          {
            EnvironmentName = environmentName
          });
      }

      return environmentInfo.WebServerMachineNames.ToList();
    }

    public List<ProjectConfiguration> GetProjectConfigurations(string projectName)
    {
      Guard.NotNullNorEmpty(projectName, "projectName");

      ProjectInfo projectInfo = _projectInfoRepository.FindByName(projectName);

      List<TeamCityBuildType> projectConfigurations = _teamCityClient.GetBuildTypesWithBranches(projectInfo.ArtifactsRepositoryName).ToList();

      return projectConfigurations.Select(DtoMapper.Map<TeamCityBuildType, ProjectConfiguration>).ToList();
    }

    public List<ProjectConfigurationBuild> GetProjectConfigurationBuilds(string projectName, string projectConfigurationName, string branchName, int maxCount)
    {
      Guard.NotNullNorEmpty(projectName, "projectName");
      Guard.NotNullNorEmpty(projectConfigurationName, "projectConfigurationName");

      ProjectInfo projectInfo = _projectInfoRepository.FindByName(projectName);

      TeamCityBuildType teamCityBuildType = _teamCityClient.GetBuildTypes(projectInfo.ArtifactsRepositoryName).FirstOrDefault(x => x.Name == projectConfigurationName);

      if (teamCityBuildType == null)
      {
        return new List<ProjectConfigurationBuild>();
      }

      IEnumerable<TeamCityBuild> projectConfigurationBuilds = _teamCityClient.GetBuilds(teamCityBuildType.Id, branchName, 0, maxCount, true);

      return projectConfigurationBuilds.Select(DtoMapper.Map<TeamCityBuild, ProjectConfigurationBuild>).ToList();
    }

    public List<string> GetWebAppProjectTargetUrls(string projectName, string environmentName)
    {
      if (string.IsNullOrEmpty(projectName))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "projectName");
      }

      if (string.IsNullOrEmpty(environmentName))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "environmentName");
      }

      WebAppProjectInfo webAppProjectInfo =
        _projectInfoRepository.FindByName(projectName) as WebAppProjectInfo;

      if (webAppProjectInfo == null)
      {
        throw new FaultException<ProjectNotFoundFault>(new ProjectNotFoundFault { ProjectName = projectName });
      }

      EnvironmentInfo environmentInfo =
        _environmentInfoRepository.FindByName(environmentName);

      if (environmentInfo == null)
      {
        throw new FaultException<EnvironmentNotFoundFault>(new EnvironmentNotFoundFault { EnvironmentName = environmentName });
      }

      List<string> targetUrls =
        webAppProjectInfo.GetTargetUrls(environmentInfo)
          .ToList();

      return targetUrls;
    }

    public List<string> GetProjectTargetFolders(string projectName, string environmentName)
    {
      if (string.IsNullOrEmpty(projectName))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "projectName");
      }

      if (string.IsNullOrEmpty(environmentName))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "environmentName");
      }

      ProjectInfo projectInfo =
        _projectInfoRepository.FindByName(projectName);

      if (projectInfo == null)
      {
        throw new FaultException<ProjectNotFoundFault>(new ProjectNotFoundFault { ProjectName = projectName });
      }

      EnvironmentInfo environmentInfo =
        _environmentInfoRepository.FindByName(environmentName);

      if (environmentInfo == null)
      {
        throw new FaultException<EnvironmentNotFoundFault>(new EnvironmentNotFoundFault { EnvironmentName = environmentName });
      }

      List<string> targetFolders =
        projectInfo.GetTargetFolders(ObjectFactory.Instance, environmentInfo)
          .ToList();

      return targetFolders;
    }

    public List<Proxy.Dto.DeploymentRequest> GetDeploymentRequests(int startIndex, int maxCount)
    {
      return
        _deploymentRequestRepository.GetDeploymentRequests(startIndex, maxCount)
          .Select(DtoMapper.Map<DeploymentRequest, Proxy.Dto.DeploymentRequest>)
          .ToList();
    }

    public List<Proxy.Dto.DiagnosticMessage> GetDiagnosticMessages(Guid uniqueClientId, long lastSeenMaxMessageId, Proxy.Dto.DiagnosticMessageType minMessageType)
    {
      if (uniqueClientId == Guid.Empty)
      {
        throw new ArgumentException("Argument can't be Guid.Empty.", "uniqueClientId");
      }

      return
        _diagnosticMessagesLogger.GetMessages(uniqueClientId, lastSeenMaxMessageId)
          .Select(DtoMapper.Map<DiagnosticMessage, Proxy.Dto.DiagnosticMessage>)
          .Where(dm => dm.Type >= minMessageType)
          .ToList();
    }

    public Proxy.Dto.Metadata.ProjectMetadata GetProjectMetadata(string projectName, string environmentName)
    {
      Guard.NotNullNorEmpty(projectName, "projectName");
      Guard.NotNullNorEmpty(environmentName, "environmentName");

      try
      {
        ProjectMetadata projectMetadata =
          _projectMetadataExplorer.GetProjectMetadata(projectName, environmentName);

        return
          new Proxy.Dto.Metadata.ProjectMetadata
          {
            ProjectName = projectMetadata.ProjectName,
            EnvironmentName = projectMetadata.EnvironmentName,
            ProjectVersions =
              projectMetadata.ProjectVersions
                .Select(
                  pv =>
                  new MachineSpecificProjectVersion
                  {
                    MachineName = pv.MachineName,
                    ProjectVersion = pv.ProjectVersion,
                  }).ToList(),
          };
      }
      catch (Exception exc)
      {
        _log.ErrorIfEnabled(() => "Unhandled exception.", exc);

        throw;
      }
    }

    public void SetCollectedCredentialsForAsynchronousWebCredentialsCollector(Guid deploymentId, string password)
    {
      Guard.NotEmpty(deploymentId, "deploymentId");
      Guard.NotNullNorEmpty(password, "password");

      AsynchronousWebPasswordCollector.SetCollectedCredentials(deploymentId, password);
    }

    public void SetSelectedDbScriptsToRun(Guid deploymentId, Proxy.Dto.DbScriptsToRunSelection scriptsToRunSelection)
    {
      ScriptsToRunWebSelector.SetSelectedScriptsToRun(
        deploymentId,
        DtoMapper.Map<Proxy.Dto.DbScriptsToRunSelection, DbScriptsToRunSelection>(scriptsToRunSelection));
    }

    public void CancelDbScriptsSelection(Guid deploymentId)
    {
      ScriptsToRunWebSelector.CancelDbScriptsSelection(deploymentId);
    }

    public string GetDefaultPackageDirPath(string environmentName, string projectName)
    {
      EnvironmentInfo environmentInfo = _environmentInfoRepository.FindByName(environmentName);

      if (environmentInfo == null)
      {
        throw new FaultException<EnvironmentNotFoundFault>(new EnvironmentNotFoundFault { EnvironmentName = environmentName });
      }

      if (string.IsNullOrEmpty(environmentInfo.ManualDeploymentPackageDirPath))
      {
        return null;
      }

      return _dirPathParamsResolver.ResolveParams(environmentInfo.ManualDeploymentPackageDirPath, projectName);
    }

    private void HandleDeploymentException(Exception exception, Guid uniqueClientId)
    {
      const string errorMessage = "Unhandled exception.";

      _diagnosticMessagesLogger
        .LogMessage(
          uniqueClientId,
          DiagnosticMessageType.Error,
          string.Format("{0}{1}", errorMessage, (exception != null ? Environment.NewLine + exception : " (no exception info)")));

      _log.ErrorIfEnabled(() => errorMessage, exception);
    }

    private void DoDeploy(Guid uniqueClientId, string requesterIdentity, DeploymentInfo deploymentInfoDto, ProjectInfo projectInfo)
    {
      DeploymentTask deploymentTask = projectInfo.CreateDeploymentTask(ObjectFactory.Instance);

      StartTask(deploymentTask, uniqueClientId, requesterIdentity, deploymentInfoDto, projectInfo);
    }

    private void DoCreatePackage(Guid uniqueClientId, string requesterIdentity, DeploymentInfo deploymentInfoDto, ProjectInfo projectInfo, string packageDirPath)
    {
      var deploymentTask =
        new CreateManualDeploymentPackageDeploymentTask(
          ObjectFactory.Instance.CreateProjectInfoRepository(),
          ObjectFactory.Instance.CreateEnvironmentInfoRepository(),
          ObjectFactory.Instance.CreateArtifactsRepository(),
          ObjectFactory.Instance.CreateDirectoryAdapter(),
          ObjectFactory.Instance.CreateFileAdapter(),
          ObjectFactory.Instance.CreateZipFileAdapter(),
          packageDirPath);

      StartTask(deploymentTask, uniqueClientId, requesterIdentity, deploymentInfoDto, projectInfo);
    }

    private void StartTask(DeploymentTask deploymentTask, Guid uniqueClientId, string requesterIdentity, DeploymentInfo deploymentInfoDto, ProjectInfo projectInfo)
    {
      Core.Domain.DeploymentInfo deploymentInfo =
        DtoMapper.ConvertDeploymentInfo(deploymentInfoDto, projectInfo);

      

      if (projectInfo is UberDeployerAgentProjectInfo)
      {
        deploymentInfo = new Core.Domain.DeploymentInfo(
          deploymentInfo.DeploymentId,
          deploymentInfo.IsSimulation,
          deploymentInfo.ProjectName,
          deploymentInfo.ProjectConfigurationName,
          deploymentInfo.ProjectConfigurationBuildId,
          _applicationConfiguration.AgentServiceEnvironmentName,
          deploymentInfo.InputParams);
      }

      var deploymentContext =
        new DeploymentContext(requesterIdentity);

      EventHandler<DiagnosticMessageEventArgs> deploymentPipelineDiagnosticMessageAction =
        (eventSender, tmpArgs) =>
        {
          _log.DebugIfEnabled(() => string.Format("{0}: {1}", tmpArgs.MessageType, tmpArgs.Message));

          _diagnosticMessagesLogger.LogMessage(uniqueClientId, tmpArgs.MessageType, tmpArgs.Message);
        };

      try
      {
        _deploymentPipeline.DiagnosticMessagePosted += deploymentPipelineDiagnosticMessageAction;

        _deploymentPipeline.StartDeployment(deploymentInfo, deploymentTask, deploymentContext);
      }
      finally
      {
        _deploymentPipeline.DiagnosticMessagePosted -= deploymentPipelineDiagnosticMessageAction;
      }
    }
  }
}
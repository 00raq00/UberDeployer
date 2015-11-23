using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading;

using log4net;

using UberDeployer.Agent.Proxy;
using UberDeployer.Agent.Proxy.Dto.EnvDeployment;
using UberDeployer.Agent.Proxy.Dto.TeamCity;
using UberDeployer.Agent.Proxy.Faults;
using UberDeployer.Agent.Service.Diagnostics;
using UberDeployer.Common;
using UberDeployer.Common.SyntaxSugar;
using UberDeployer.CommonConfiguration;
using UberDeployer.Core.Configuration;
using UberDeployer.Core.DataAccess.WebClient;
using UberDeployer.Core.Deployment;
using UberDeployer.Core.Deployment.Pipeline;
using UberDeployer.Core.Deployment.Pipeline.Modules;
using UberDeployer.Core.Deployment.Steps;
using UberDeployer.Core.Deployment.Tasks;
using UberDeployer.Core.Domain;
using UberDeployer.Core.Domain.Input;
using UberDeployer.Core.ExternalDataCollectors;
using UberDeployer.Core.ExternalDataCollectors.DependentProjectsSelection;
using UberDeployer.Core.Management.Metadata;
using UberDeployer.Core.TeamCity;
using UberDeployer.Core.TeamCity.ApiModels;

using DbScriptsToRunSelection = UberDeployer.Core.Deployment.DbScriptsToRunSelection;
using DependentProject = UberDeployer.Agent.Proxy.Dto.DependentProject;
using DeploymentInfo = UberDeployer.Agent.Proxy.Dto.DeploymentInfo;
using DeploymentRequest = UberDeployer.Core.Deployment.Pipeline.Modules.DeploymentRequest;
using DiagnosticMessage = UberDeployer.Core.Deployment.DiagnosticMessage;
using DiagnosticMessageType = UberDeployer.Core.Deployment.DiagnosticMessageType;
using EnvironmentInfo = UberDeployer.Core.Domain.EnvironmentInfo;
using MachineSpecificProjectVersion = UberDeployer.Agent.Proxy.Dto.Metadata.MachineSpecificProjectVersion;
using Project = UberDeployer.Core.TeamCity.Models.Project;
using ProjectInfo = UberDeployer.Core.Domain.ProjectInfo;
using ProjectType = UberDeployer.Core.Domain.ProjectType;
using UberDeployerAgentProjectInfo = UberDeployer.Core.Domain.UberDeployerAgentProjectInfo;
using WebAppProjectInfo = UberDeployer.Core.Domain.WebAppProjectInfo;

namespace UberDeployer.Agent.Service
{
  public class AgentService : IAgentService
  {
    private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private readonly IDeploymentPipeline _deploymentPipeline;

    private readonly IEnvDeploymentPipeline _envDeploymentPipeline;

    private readonly IProjectInfoRepository _projectInfoRepository;

    private readonly IEnvironmentInfoRepository _environmentInfoRepository;

    private readonly IEnvironmentDeployInfoRepository _environmentDeployInfoRepository;

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
      IApplicationConfiguration applicationConfiguration, 
      IEnvironmentDeployInfoRepository environmentDeployInfoRepository, 
      IEnvDeploymentPipeline envDeploymentPipeline)
    {
      Guard.NotNull(deploymentPipeline, "deploymentPipeline");
      Guard.NotNull(projectInfoRepository, "projectInfoRepository");
      Guard.NotNull(environmentInfoRepository, "environmentInfoRepository");
      Guard.NotNull(teamCityClient, "teamCityClient");
      Guard.NotNull(deploymentRequestRepository, "deploymentRequestRepository");
      Guard.NotNull(diagnosticMessagesLogger, "diagnosticMessagesLogger");
      Guard.NotNull(dirPathParamsResolver, "dirPathParamsResolver");
      Guard.NotNull(applicationConfiguration, "applicationConfiguration");
      Guard.NotNull(environmentDeployInfoRepository, "environmentDeployInfoRepository");
      Guard.NotNull(envDeploymentPipeline, "envDeploymentPipeline");

      _projectInfoRepository = projectInfoRepository;
      _environmentInfoRepository = environmentInfoRepository;
      _teamCityClient = teamCityClient;
      _deploymentPipeline = deploymentPipeline;
      _deploymentRequestRepository = deploymentRequestRepository;
      _diagnosticMessagesLogger = diagnosticMessagesLogger;
      _projectMetadataExplorer = projectMetadataExplorer;
      _dirPathParamsResolver = dirPathParamsResolver;
      _applicationConfiguration = applicationConfiguration;
      _environmentDeployInfoRepository = environmentDeployInfoRepository;
      _envDeploymentPipeline = envDeploymentPipeline;
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
        ObjectFactory.Instance.CreateApplicationConfiguration(),
        ObjectFactory.Instance.CreateEnvironmentDeployInfoRepository(),
        ObjectFactory.Instance.CrateEnvDeploymentPipeline())
    {
    }

    public void Deploy(Guid deploymentId, Guid uniqueClientId, string requesterIdentity, DeploymentInfo deploymentInfoDto)
    {
      try
      {
        Guard.NotEmpty(deploymentId, "deploymentId");
        Guard.NotEmpty(uniqueClientId, "uniqueClientId");
        Guard.NotNullNorEmpty(requesterIdentity, "requesterIdentity");
        Guard.NotNull(deploymentInfoDto, "DeploymentInfo");

        ProjectInfo projectInfo =
          _projectInfoRepository.FindByName(deploymentInfoDto.ProjectName);

        if (projectInfo == null)
        {
          throw new FaultException<ProjectNotFoundFault>(new ProjectNotFoundFault { ProjectName = deploymentInfoDto.ProjectName });
        }

        Core.Domain.DeploymentInfo deploymentInfo =
          DtoMapper.ConvertDeploymentInfo(deploymentInfoDto, projectInfo);

        DoDeploy(uniqueClientId, requesterIdentity, deploymentInfo, projectInfo);
      }
      catch (Exception exc)
      {
        HandleDeploymentException(exc, uniqueClientId);
      }
    }

    public void DeployAsync(Guid deploymentId, Guid uniqueClientId, string requesterIdentity, DeploymentInfo deploymentInfoDto)
    {
      try
      {
        Guard.NotEmpty(deploymentId, "deploymentId");
        Guard.NotEmpty(uniqueClientId, "uniqueClientId");
        Guard.NotNullNorEmpty(requesterIdentity, "requesterIdentity");
        Guard.NotNull(deploymentInfoDto, "deploymentInfo");

        ProjectInfo projectInfo =
          _projectInfoRepository.FindByName(deploymentInfoDto.ProjectName);

        if (projectInfo == null)
        {
          throw new FaultException<ProjectNotFoundFault>(new ProjectNotFoundFault { ProjectName = deploymentInfoDto.ProjectName });
        }

        Core.Domain.DeploymentInfo deploymentInfo = DtoMapper.ConvertDeploymentInfo(deploymentInfoDto, projectInfo);

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

    public void DeployEnvironmentAsync(Guid uniqueClientId, string requesterIdentity, string targetEnvironment, List<ProjectToDeploy> projects)
    {
      Guard.NotEmpty(uniqueClientId, "uniqueClientId");
      Guard.NotNullNorEmpty(requesterIdentity, "requesterIdentity");
      Guard.NotNullNorEmpty(targetEnvironment, "targetEnvironment");

      EnvironmentDeployInfo environmentDeployInfo = _environmentDeployInfoRepository.FindByName(targetEnvironment);

      if (environmentDeployInfo == null)
      {
        throw new FaultException<EnvironmentDeployConfigurationNotFoundFault>(new EnvironmentDeployConfigurationNotFoundFault { EnvironmentName = targetEnvironment });
      }

      List<ProjectDeploymentData> projectsToDeploy = CreateProjectEnvironmentDeployments(uniqueClientId, environmentDeployInfo, projects).ToList();

      ThreadPool.QueueUserWorkItem(
        state =>
        {
          EventHandler<DiagnosticMessageEventArgs> deploymentPipelineDiagnosticMessageAction =
            (eventSender, tmpArgs) => LogMessage(uniqueClientId, tmpArgs.MessageType, tmpArgs.Message);

          EventHandler<DiagnosticMessageGroupEventArgs> deploymentPipelineDiagnosticMessageGroupOpened =
            (eventSender, tmpArgs) => OpenLogGroup(uniqueClientId, tmpArgs.GroupName);

          EventHandler deploymentPipelineDiagnosticMessageGroupClosed =
              (eventSender, tmpArgs) => CloseLogGroup(uniqueClientId);
          try
          {
            _envDeploymentPipeline.DiagnosticMessagePosted += deploymentPipelineDiagnosticMessageAction;            
            _envDeploymentPipeline.DiagnosticMessageGroupOpened += deploymentPipelineDiagnosticMessageGroupOpened;            
            _envDeploymentPipeline.DiagnosticMessageGroupClosed += deploymentPipelineDiagnosticMessageGroupClosed;

            _envDeploymentPipeline.StartDeployment(targetEnvironment, projectsToDeploy, new DeploymentContext(requesterIdentity));
          }
          finally
          {
            _envDeploymentPipeline.DiagnosticMessagePosted -= deploymentPipelineDiagnosticMessageAction;
          }
        });
    }

    public void SetSelectedDependentProjectsToDeploy(Guid deploymentId, List<DependentProject> dependenciesToDeploy)
    {
      DependentProjectsToDeploySelection dependentProjectsToDeploySelection = new DependentProjectsToDeploySelection()
      {
        SelectedProjects = new List<Core.ExternalDataCollectors.DependentProjectsSelection.DependentProject>(DtoMapper.Map<List<Proxy.Dto.DependentProject>, List<Core.ExternalDataCollectors.DependentProjectsSelection.DependentProject>>(dependenciesToDeploy))
      };

      DependentProjectsToDeployWebSelector
        .SetSelectedProjectsToDeploy(deploymentId, dependentProjectsToDeploySelection);
    }

    public void SkipDependentProjectsSelection(Guid deploymentId)
    {
      DependentProjectsToDeployWebSelector.SkipDependentProjectsSelection(deploymentId);
    }

    public void CancelDependentProjectsSelection(Guid deploymentId)
    {
      DependentProjectsToDeployWebSelector.CancelDependentProjectsSelection(deploymentId);
    }

    private IEnumerable<ProjectDeploymentData> CreateProjectEnvironmentDeployments(Guid uniqueClientId, EnvironmentDeployInfo environmentDeployInfo, IEnumerable<ProjectToDeploy> projects)
    {
      var projectDeployments = new List<ProjectDeploymentData>();
      var priorityProjectDeplyoments = new List<ProjectDeploymentData>();

      EnvironmentInfo environmentInfo = _environmentInfoRepository.FindByName(environmentDeployInfo.TargetEnvironment);

      if (environmentInfo == null)
      {
        throw new FaultException<EnvironmentNotFoundFault>(new EnvironmentNotFoundFault { EnvironmentName = environmentDeployInfo.TargetEnvironment });
      }

      foreach (var projectToDeploy in projects)
      {
        try
        {
          ProjectInfo projectInfo = _projectInfoRepository.FindByName(projectToDeploy.ProjectName);

          if (projectInfo == null)
          {
            throw new DeploymentTaskException(string.Format("Not found configuration for project: {0}", projectToDeploy.ProjectName));
          }

          ProjectConfigurationBuild lastSuccessfulBuild = GetLatestSuccessfulBuild(projectToDeploy.ProjectName, environmentDeployInfo.BuildConfigurationName);

          if (lastSuccessfulBuild == null)
          {
            throw new DeploymentTaskException(string.Format("Successful build not found for project: {0} and configuration: {1}", projectToDeploy, environmentDeployInfo.BuildConfigurationName));
          }

          InputParams inputParams = BuildInputParams(projectInfo, environmentInfo);

          var deploymentInfo = new Core.Domain.DeploymentInfo(projectToDeploy.DeploymentId, false, projectToDeploy.ProjectName, environmentDeployInfo.BuildConfigurationName, lastSuccessfulBuild.Id, environmentDeployInfo.TargetEnvironment, inputParams);

          DeploymentTask deploymentTask;

          // TODO LK: could replace below code with factory
          if (projectInfo.Type == ProjectType.Db)
          {
            DeploymentTask dropDbProjectDeploymentTask = new DropDbProjectDeploymentTask(
              ObjectFactory.Instance.CreateProjectInfoRepository(),
              ObjectFactory.Instance.CreateEnvironmentInfoRepository(),
              ObjectFactory.Instance.CreateDbManagerFactory());

            priorityProjectDeplyoments.Add(new ProjectDeploymentData(deploymentInfo, projectInfo, dropDbProjectDeploymentTask));

            deploymentTask =
              new DeployDbProjectDeploymentTask(
                ObjectFactory.Instance.CreateProjectInfoRepository(),
                ObjectFactory.Instance.CreateEnvironmentInfoRepository(),
                ObjectFactory.Instance.CreateArtifactsRepository(),
                ObjectFactory.Instance.CreateDbScriptRunnerFactory(),
                ObjectFactory.Instance.CreateDbVersionProvider(),
                ObjectFactory.Instance.CreateFileAdapter(),
                ObjectFactory.Instance.CreateZipFileAdapter(),
                ObjectFactory.Instance.CreateScriptsToRunWebSelectorForEnvironmentDeploy(),
                ObjectFactory.Instance.CreateMsSqlDatabasePublisher(),
                ObjectFactory.Instance.CreateDbManagerFactory(),
                ObjectFactory.Instance.CreateUserNameNormalizer(),
                ObjectFactory.Instance.CreateDirectoryAdapter());
          }
          else if (projectInfo.Type == ProjectType.NtService)
          {
            deploymentTask = new DeployNtServiceDeploymentTask(
              ObjectFactory.Instance.CreateProjectInfoRepository(),
              ObjectFactory.Instance.CreateEnvironmentInfoRepository(),
              ObjectFactory.Instance.CreateArtifactsRepository(),
              ObjectFactory.Instance.CreateNtServiceManager(),
              ObjectFactory.Instance.CreatePasswordCollector(),
              ObjectFactory.Instance.CreateFailoverClusterManager(),
              ObjectFactory.Instance.CreateDirectoryAdapter(),
              ObjectFactory.Instance.CreateFileAdapter(),
              ObjectFactory.Instance.CreateZipFileAdapter())
            {
              UseLocalSystemUser = true
            };
          }
          else
          {
            deploymentTask = projectInfo.CreateDeploymentTask(ObjectFactory.Instance);
          }

          projectDeployments.Add(new ProjectDeploymentData(deploymentInfo, projectInfo, deploymentTask));
        }
        catch (Exception e)
        {
          LogMessage(uniqueClientId, DiagnosticMessageType.Error, e.Message);
        }
      }

      priorityProjectDeplyoments.AddRange(projectDeployments);

      return priorityProjectDeplyoments;
    }

    private static InputParams BuildInputParams(ProjectInfo projectInfo, EnvironmentInfo environmentInfo)
    {
      switch (projectInfo.Type)
      {
        case ProjectType.WebApp:
          // deploy to all web server machines
          return new WebAppInputParams(environmentInfo.WebServerMachineNames);

        case ProjectType.Db:
          return new DbInputParams();

        case ProjectType.NtService:
          return new NtServiceInputParams();

        case ProjectType.SchedulerApp:
          return new SsdtInputParams();

        case ProjectType.TerminalApp:
          return new SsdtInputParams();

        case ProjectType.WebService:
          return new SsdtInputParams();

        case ProjectType.Extension:
          return new ExtensionInputParams();

        case ProjectType.PowerShellScript:
          return new PowerShellInputParams();

        default:
          throw new DeploymentTaskException(string.Format("Project type: {0} is not supported in environment deployment.", projectInfo.Type));
      }      
    }

    private ProjectConfigurationBuild GetLatestSuccessfulBuild(string projectName, string projectConfigurationName)
    {
      Guard.NotNullNorEmpty(projectName, "projectName");
      Guard.NotNullNorEmpty(projectConfigurationName, "projectConfigurationName");

      ProjectInfo projectInfo = _projectInfoRepository.FindByName(projectName);

      TeamCityBuildType teamCityBuildType = _teamCityClient.GetBuildTypes(projectInfo.ArtifactsRepositoryName).FirstOrDefault(x => x.Name == projectConfigurationName);      
      
      if (teamCityBuildType == null)
      {
        return null;
      }

      TeamCityBuild lastSuccessfulBuild = _teamCityClient.GetLastSuccessfulBuild(teamCityBuildType.Id);

      ProjectConfigurationBuild projectConfigurationOfLastBuild = DtoMapper.Map<TeamCityBuild, ProjectConfigurationBuild>(lastSuccessfulBuild);

      return projectConfigurationOfLastBuild;
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
      ScriptsToRunSelector.SetSelectedScriptsToRun(
        deploymentId,
        DtoMapper.Map<Proxy.Dto.DbScriptsToRunSelection, DbScriptsToRunSelection>(scriptsToRunSelection));
    }

    public void CancelDbScriptsSelection(Guid deploymentId)
    {
      ScriptsToRunSelector.CancelDbScriptsSelection(deploymentId);
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

    public List<string> GetProjectsForEnvironmentDeploy(string environmentName)
    {
      EnvironmentDeployInfo environmentDeployInfo =
        _environmentDeployInfoRepository.FindByName(environmentName);

      if (environmentDeployInfo == null)
      {
        return null;
      }      

      return environmentDeployInfo.ProjectsToDeploy;
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

    private void DoDeploy(Guid uniqueClientId, string requesterIdentity, Core.Domain.DeploymentInfo deploymentInfo, ProjectInfo projectInfo)
    {
      DeploymentTask deploymentTask = projectInfo.CreateDeploymentTask(ObjectFactory.Instance);

      Core.Domain.DeploymentInfo deployInfo = OverwriteConfigurationIfSelfDeployment(deploymentInfo, projectInfo);
      
      StartTask(deploymentTask, uniqueClientId, requesterIdentity, deployInfo);
    }

    private Core.Domain.DeploymentInfo OverwriteConfigurationIfSelfDeployment(Core.Domain.DeploymentInfo deploymentInfo, ProjectInfo projectInfo)
    {
      if (projectInfo is UberDeployerAgentProjectInfo)
      {
        return new Core.Domain.DeploymentInfo(
          deploymentInfo.DeploymentId,
          deploymentInfo.IsSimulation,
          deploymentInfo.ProjectName,
          deploymentInfo.ProjectConfigurationName,
          deploymentInfo.ProjectConfigurationBuildId,
          _applicationConfiguration.AgentServiceEnvironmentName,
          deploymentInfo.InputParams);
      }

      return deploymentInfo;
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

      Core.Domain.DeploymentInfo deploymentInfo =
        DtoMapper.ConvertDeploymentInfo(deploymentInfoDto, projectInfo);

      StartTask(deploymentTask, uniqueClientId, requesterIdentity, deploymentInfo);
    }

    private void StartTask(DeploymentTask deploymentTask, Guid uniqueClientId, string requesterIdentity, Core.Domain.DeploymentInfo deploymentInfo)
    {     
      var deploymentContext =
        new DeploymentContext(requesterIdentity);

      EventHandler<DiagnosticMessageEventArgs> deploymentPipelineDiagnosticMessageAction =
        (eventSender, tmpArgs) => LogMessage(uniqueClientId, tmpArgs.MessageType, tmpArgs.Message);

      try
      {        
        _deploymentPipeline.DiagnosticMessagePosted += deploymentPipelineDiagnosticMessageAction;

        _deploymentPipeline.StartDeployment(deploymentInfo, deploymentTask, deploymentContext, _applicationConfiguration.DeployDependentProjects);
      }
      finally
      {
        _deploymentPipeline.DiagnosticMessagePosted -= deploymentPipelineDiagnosticMessageAction;
      }
    }

    private void LogMessage(Guid uniqueClientId, DiagnosticMessageType messageType, string message)
    {
      _log.DebugIfEnabled(() => string.Format("{0}: {1}", messageType, message));

      _diagnosticMessagesLogger.LogMessage(uniqueClientId, messageType, message);
    }

    private void OpenLogGroup(Guid uniqueClientId, string groupId)
    {
      _log.InfoIfEnabled(() => string.Format("Open log group [{0}] for client [{1}]", groupId, uniqueClientId));

      _diagnosticMessagesLogger.OpenLogGroup(uniqueClientId, groupId);
    }

    private void CloseLogGroup(Guid uniqueClientId)
    {
      _log.DebugIfEnabled(() => string.Format("Close log group for client [{0}]", uniqueClientId));

      _diagnosticMessagesLogger.CloseLogGroup(uniqueClientId);
    }
  }
}
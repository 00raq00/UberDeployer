﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text.RegularExpressions;
using System.Web.Mvc;

using UberDeployer.Agent.Proxy;
using UberDeployer.Agent.Proxy.Dto;
using UberDeployer.Agent.Proxy.Dto.Input;
using UberDeployer.Agent.Proxy.Dto.Metadata;
using UberDeployer.Agent.Proxy.Dto.TeamCity;
using UberDeployer.Agent.Proxy.Faults;
using UberDeployer.Common;
using UberDeployer.Common.SyntaxSugar;
using UberDeployer.WebApp.Core.Infrastructure;
using UberDeployer.WebApp.Core.Models.Api;
using UberDeployer.WebApp.Core.Services;
using UberDeployer.WebApp.Core.Utils;

namespace UberDeployer.WebApp.Core.ApiControllers
{
  public class ApiController : UberDeployerWebAppController
  {
    private const string _AppSettingsKey_MaxProjectConfigurationBuildsCount = "MaxProjectConfigurationBuildsCount";
    private const string _AppSettingsKey_VisibleEnvironments = "VisibleEnvironments";
    private const string _AppSettingsKey_DeployableEnvironments = "DeployableEnvironments";
    private const string _AppSettingsKey_AllowedProjectConfigurations = "AllowedProjectConfigurations";

    private static readonly ISet<string> _visibleEnvironments;
    private static readonly ISet<string> _deployableEnvironments;
    private static readonly ISet<string> _allowedProjectConfigurations;
    private static readonly int _maxProjectConfigurationBuildsCount;

    private readonly ISessionService _sessionService;
    private readonly IAgentService _agentService;
    private readonly IDeploymentStateProvider _deploymentStateProvider;

    static ApiController()
    {
      string visibleEnvironmentsStr = AppSettingsUtils.ReadAppSettingString(_AppSettingsKey_VisibleEnvironments);
      string deployableEnvironmentsStr = AppSettingsUtils.ReadAppSettingString(_AppSettingsKey_DeployableEnvironments);
      string allowedProjectConfigurationsStr = AppSettingsUtils.ReadAppSettingString(_AppSettingsKey_AllowedProjectConfigurations);

      _visibleEnvironments = ParseAppSettingSet(visibleEnvironmentsStr);
      _deployableEnvironments = ParseAppSettingSet(deployableEnvironmentsStr);
      _allowedProjectConfigurations = ParseAppSettingSet(allowedProjectConfigurationsStr);
      _maxProjectConfigurationBuildsCount = AppSettingsUtils.ReadAppSettingInt(_AppSettingsKey_MaxProjectConfigurationBuildsCount);
    }

    public ApiController(ISessionService sessionService, IAgentService agentService, IDeploymentStateProvider deploymentStateProvider)
    {
      Guard.NotNull(sessionService, "sessionService");
      Guard.NotNull(agentService, "agentService");
      Guard.NotNull(deploymentStateProvider, "deploymentStateProvider");

      _sessionService = sessionService;
      _agentService = agentService;
      _deploymentStateProvider = deploymentStateProvider;
    }

    public ApiController()
      : this(new SessionService(), new AgentServiceClient(), new DeploymentStateProvider())
    {
    }

    [HttpGet]
    public ActionResult GetEnvironments()
    {
      List<EnvironmentViewModel> environmentViewModels =
        _agentService.GetEnvironmentInfos()
          .Where(pi => _visibleEnvironments.Count == 0 || _visibleEnvironments.Any(ae => Regex.IsMatch(pi.Name, ae, RegexOptions.IgnoreCase)))
          .Select(pi =>
            new EnvironmentViewModel
            {
              Name = pi.Name,
              IsDeployable = _deployableEnvironments.Count == 0 || _deployableEnvironments.Any(reg => Regex.IsMatch(pi.Name, reg, RegexOptions.IgnoreCase))
            })
          .ToList();

      return
        Json(
          new { environments = environmentViewModels },
          JsonRequestBehavior.AllowGet);
    }

    [HttpGet]
    public ActionResult GetProjects(string environmentName, bool onlyDeployable)
    {
      var projectFilter =
        new ProjectFilter
        {
          EnvironmentName = environmentName,
        };

      if (onlyDeployable && !_deployableEnvironments.Any(reg => Regex.IsMatch(environmentName, reg, RegexOptions.IgnoreCase)))
      {
        return Json(null, JsonRequestBehavior.AllowGet);
      }

      var projectInfos = _agentService.GetProjectInfos(projectFilter);

      List<ProjectViewModel> projectViewModels =
        projectInfos
          .Where(x => !onlyDeployable || x.AllowedEnvironmentNames.Contains(environmentName))
          .Select(CreateProjectViewModel)
          .ToList();

      return
        Json(
          new { projects = projectViewModels },
          JsonRequestBehavior.AllowGet);
    }

    [HttpGet]
    public ActionResult GetProjectConfigurations(string projectName)
    {
      if (string.IsNullOrEmpty(projectName))
      {
        return BadRequest();
      }

      List<ProjectConfiguration> projectConfigurations = _agentService.GetProjectConfigurations(projectName)
        .Where(pc => _allowedProjectConfigurations.Count == 0 || _allowedProjectConfigurations.Any(apc => Regex.IsMatch(pc.Name, apc, RegexOptions.IgnoreCase))).ToList();

      var projectConfigurationViewModels = new List<ProjectConfigurationViewModel>();

      foreach (var projectConfiguration in projectConfigurations)
      {
        if (projectConfiguration.HasBranches())
        {
          ProjectConfiguration configuration = projectConfiguration;
          projectConfiguration.GetBranches().ForEach(
            branchName => projectConfigurationViewModels.Add(
              new ProjectConfigurationViewModel
              {
                BranchName = branchName,
                Name = configuration.Name
              }));
        }
        else
        {
          projectConfigurationViewModels.Add(
            new ProjectConfigurationViewModel
            {
              Name = projectConfiguration.Name
            });
        }
      }

      return
        Json(
          new { projectConfigurations = projectConfigurationViewModels },
          JsonRequestBehavior.AllowGet);
    }

    [HttpGet]
    public ActionResult GetProjectConfigurationBuilds(string projectName, string projectConfigurationName)
    {
      if (string.IsNullOrEmpty(projectName))
      {
        return BadRequest();
      }

      if (string.IsNullOrEmpty(projectConfigurationName))
      {
        return BadRequest();
      }

      var projectConfigurationModel = new ProjectConfigurationModel(projectConfigurationName);

      List<ProjectConfigurationBuildViewModel> projectConfigurationBuildViewModels =
        _agentService.GetProjectConfigurationBuilds(
          projectName,
          projectConfigurationModel.ConfigurationName,
          projectConfigurationModel.BranchName,
          _maxProjectConfigurationBuildsCount)
          .Select(
            pcb =>
              new ProjectConfigurationBuildViewModel
              {
                Id = pcb.Id,
                Number = pcb.Number,
                StartDateStr = pcb.StartDate
              })
          .ToList();

      return
        Json(
          new { projectConfigurationBuilds = projectConfigurationBuildViewModels },
          JsonRequestBehavior.AllowGet);
    }

    [HttpGet]
    public ActionResult GetWebMachineNames(string envName)
    {
      if (string.IsNullOrWhiteSpace(envName))
      {
        return BadRequest();
      }

      try
      {
        List<string> webMachineNames =
          _agentService.GetWebMachineNames(envName);

        return Json(
          webMachineNames,
          JsonRequestBehavior.AllowGet);
      }
      catch (FaultException<EnvironmentNotFoundFault>)
      {
        return BadRequest();
      }
    }

    [HttpGet]
    public ActionResult GetDiagnosticMessages(long? lastSeenMaxMessageId)
    {
      if (!lastSeenMaxMessageId.HasValue)
      {
        return BadRequest();
      }

      List<DiagnosticMessageViewModel> diagnosticMessageViewModels =
        _agentService.GetDiagnosticMessages(
          _sessionService.UniqueClientId,
          lastSeenMaxMessageId.Value,
          DiagnosticMessageType.Trace)
          .Select(
            dm =>
              new DiagnosticMessageViewModel
              {
                MessageId = dm.MessageId,
                Message = dm.Message,
                Type = dm.Type.ToString(),
              }).ToList();

      return
        Json(
          new { messages = diagnosticMessageViewModels },
          JsonRequestBehavior.AllowGet);
    }

    [HttpPost]
    public ActionResult Deploy(string projectName, string projectConfigurationName, string projectConfigurationBuildId, string targetEnvironmentName, ProjectType? projectType, List<string> targetMachines = null)
    {
      if (!SecurityUtils.CanDeploy)
      {
        return AccessDenied();
      }

      return
        DoDeployOrSimulate(
          false,
          projectName,
          projectConfigurationName,
          projectConfigurationBuildId,
          targetEnvironmentName,
          projectType,
          targetMachines);
    }

    [HttpPost]
    public ActionResult CreatePackage(string projectName, string projectConfigurationName, string projectConfigurationBuildId, string targetEnvironmentName, ProjectType? projectType, string packageDirPath)
    {
      if (string.IsNullOrEmpty(projectName)
          || string.IsNullOrEmpty(projectConfigurationName)
          || string.IsNullOrEmpty(projectConfigurationBuildId)
          || string.IsNullOrEmpty(targetEnvironmentName)
          || !projectType.HasValue)
      {
        return BadRequest();
      }

      try
      {
        Guid deploymentId = Guid.NewGuid();

        _agentService.CreatePackageAsync(
          deploymentId,
          _sessionService.UniqueClientId,
          SecurityUtils.CurrentUsername,
          CreateDeploymentInfo(
            deploymentId,
            false,
            projectName,
            projectConfigurationName,
            projectConfigurationBuildId,
            targetEnvironmentName,
            projectType.Value),
          packageDirPath);

        return Json(new { Status = "OK" });
      }
      catch (Exception exc)
      {
        return HandleAjaxError(exc);
      }
    }

    [HttpGet]
    public ActionResult GetDefaultPackageDirPath(string environmentName, string projectName)
    {
      if (string.IsNullOrEmpty(environmentName)
          || string.IsNullOrEmpty(projectName))
      {
        return BadRequest();
      }

      try
      {
        return new ContentResult { Content = _agentService.GetDefaultPackageDirPath(environmentName, projectName) };
      }
      catch (Exception exc)
      {
        return HandleAjaxError(exc);
      }
    }

    [HttpPost]
    public ActionResult Simulate(string projectName, string projectConfigurationName, string projectConfigurationBuildId, string targetEnvironmentName, ProjectType? projectType, List<string> targetMachines = null)
    {
      return
        DoDeployOrSimulate(
          true,
          projectName,
          projectConfigurationName,
          projectConfigurationBuildId,
          targetEnvironmentName,
          projectType,
          targetMachines);
    }

    [HttpGet]
    public ActionResult GetProjectMetadata(string projectName, string environmentName)
    {
      if (string.IsNullOrEmpty(projectName))
      {
        return BadRequest();
      }

      if (string.IsNullOrEmpty(environmentName))
      {
        return BadRequest();
      }

      try
      {
        ProjectMetadata projectMetadata =
          _agentService.GetProjectMetadata(projectName, environmentName);

        var projectMetadataViewModel =
          new ProjectMetadataViewModel
          {
            ProjectName = projectMetadata.ProjectName,
            EnvironmentName = projectMetadata.EnvironmentName,
            ProjectVersions =
              projectMetadata.ProjectVersions
                .Select(
                  pv =>
                    new MachineSpecificProjectVersionViewModel
                    {
                      MachineName = pv.MachineName,
                      ProjectVersion = pv.ProjectVersion,
                    }).ToList(),
          };

        projectMetadataViewModel.Status = "OK";

        return
          Json(
            projectMetadataViewModel,
            JsonRequestBehavior.AllowGet);
      }
      catch (Exception exc)
      {
        return HandleAjaxError(exc);
      }
    }

    private static ISet<string> ParseAppSettingSet(string appSettingValue)
    {
      if (string.IsNullOrEmpty(appSettingValue))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "appSettingValue");
      }

      if (appSettingValue == "*")
      {
        return new HashSet<string>();
      }

      return new HashSet<string>(appSettingValue.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static ProjectViewModel CreateProjectViewModel(ProjectInfo pi)
    {
      return new ProjectViewModel
      {
        Name = pi.Name,
        Type = (ProjectTypeViewModel)Enum.Parse(typeof(ProjectTypeViewModel), pi.Type.ToString(), true),
        AllowedEnvironmentNames = pi.AllowedEnvironmentNames,
      };
    }

    private ActionResult DoDeployOrSimulate(bool isSimulation, string projectName, string projectConfigurationName, string projectConfigurationBuildId, string targetEnvironmentName, ProjectType? projectType, List<string> targetMachines = null)
    {
      if (string.IsNullOrEmpty(projectName))
      {
        return BadRequest();
      }

      if (string.IsNullOrEmpty(projectConfigurationName))
      {
        return BadRequest();
      }

      if (string.IsNullOrEmpty(projectConfigurationBuildId))
      {
        return BadRequest();
      }

      if (string.IsNullOrEmpty(targetEnvironmentName))
      {
        return BadRequest();
      }

      if (!projectType.HasValue)
      {
        return BadRequest();
      }

      try
      {
        Guid deploymentId = Guid.NewGuid();

        var projectConfigurationModel = new ProjectConfigurationModel(projectConfigurationName);
        
        var deploymentState =
          new DeploymentState(
            deploymentId,
            UserIdentity,
            projectName,
            projectConfigurationModel.ConfigurationName,
            targetEnvironmentName);

        _deploymentStateProvider.SetDeploymentState(
          deploymentId,
          deploymentState);

        _agentService.DeployAsync(
          deploymentId,
          _sessionService.UniqueClientId,
          SecurityUtils.CurrentUsername,
          CreateDeploymentInfo(
            deploymentId,
            isSimulation,
            projectName,
            projectConfigurationModel.ConfigurationName,
            projectConfigurationBuildId,
            targetEnvironmentName,
            projectType.Value,
            targetMachines));

        return Json(new { Status = "OK" });
      }
      catch (Exception exc)
      {
        return HandleAjaxError(exc);
      }
    }

    private ActionResult HandleAjaxError(Exception exception)
    {
      return Json(new { Status = "FAIL", ErrorMessage = exception.Message }, JsonRequestBehavior.AllowGet);
    }

    private DeploymentInfo CreateDeploymentInfo(Guid deploymentId, bool isSimulation, string projectName, string projectConfigurationName, string projectConfigurationBuildId, string targetEnvironmentName, ProjectType projectType, IEnumerable<string> targetMachines = null)
    {
      return
        new DeploymentInfo
        {
          DeploymentId = deploymentId,
          IsSimulation = isSimulation,
          ProjectName = projectName,
          ProjectConfigurationName = projectConfigurationName,
          ProjectConfigurationBuildId = projectConfigurationBuildId,
          TargetEnvironmentName = targetEnvironmentName,
          InputParams = CreateDeploymentInputParams(projectName, targetMachines),
        };
    }

    private InputParams CreateDeploymentInputParams(string projectName, IEnumerable<string> targetMachines = null)
    {
      ProjectType projectType = GetProjectType(projectName);

      switch (projectType)
      {
        case ProjectType.Db:
        {
          return new DbInputParams();
        }

        case ProjectType.NtService:
        {
          return new NtServiceInputParams();
        }

        case ProjectType.SchedulerApp:
        {
          return new SchedulerAppInputParams();
        }

        case ProjectType.TerminalApp:
        {
          return new TerminalAppInputParams();
        }

        case ProjectType.WebApp:
        {
          List<string> onlyIncludedWebMachines = null;

          if (targetMachines != null)
          {
            onlyIncludedWebMachines = new List<string>(targetMachines);

            if (onlyIncludedWebMachines.Count == 0)
            {
              throw new ArgumentException("If target machines are specified there must be at least one present.", "targetMachines");
            }
          }

          return
            new WebAppInputParams
            {
              OnlyIncludedWebMachines = onlyIncludedWebMachines,
            };
        }

        case ProjectType.WebService:
        {
          return new WebServiceInputParams();
        }

        case ProjectType.UberDeployerAgent:
        {
          return new NtServiceInputParams();
        }

        case ProjectType.Extension:
        {
          return new ExtensionInputParams();
        }

        case ProjectType.PowerShellScript:
        {
          return new PowerShellScriptInputParams();
        }

        default:
        {
          throw new NotSupportedException(string.Format("Unknown project type: '{0}'.", projectType));
        }
      }
    }

    private ProjectType GetProjectType(string projectName)
    {
      ProjectInfo projectInfo =
        _agentService.GetProjectInfos(
          new ProjectFilter { Name = projectName })
          .SingleOrDefault(pi => pi.Name == projectName);

      if (projectInfo == null)
      {
        throw new InternalException(string.Format("Unknown project: '{0}'.", projectName));
      }

      return projectInfo.Type;
    }
  }
}

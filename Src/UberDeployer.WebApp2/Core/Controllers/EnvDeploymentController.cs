using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

using UberDeployer.Agent.Proxy;
using UberDeployer.Agent.Proxy.Dto.EnvDeployment;
using UberDeployer.Common.SyntaxSugar;
using UberDeployer.WebApp2.Core.Infrastructure;
using UberDeployer.WebApp2.Core.Models.EnvDeploy;
using UberDeployer.WebApp2.Core.Services;
using UberDeployer.WebApp2.Core.Utils;

namespace UberDeployer.WebApp2.Core.Controllers
{
  public class EnvDeploymentController : UberDeployerWebAppController
  {
    //TODO MARIO: should be set from env deploy configuration, it's only shown on credentials prompt, doesn't affect deploy.
    public const string ProjectConfigurationName = "Production";

    private readonly IAgentService _agentService;

    private readonly ISessionService _sessionService;

    private readonly IDeploymentStateProvider _deploymentStateProvider;

    public EnvDeploymentController(IAgentService agentService, ISessionService sessionService, IDeploymentStateProvider deploymentStateProvider)
    {
      Guard.NotNull(agentService, "agentService");
      Guard.NotNull(sessionService, "sessionService");
      Guard.NotNull(deploymentStateProvider, "deploymentStateProvider");

      _agentService = agentService;
      _sessionService = sessionService;
      _deploymentStateProvider = deploymentStateProvider;
    }

    public EnvDeploymentController()
      :this(new AgentServiceClient(), new SessionService(), new DeploymentStateProvider())
    {
    }

    [HttpGet]
    public ActionResult Index(string env = null)
    {
      var viewModel =
        new EnvDeployViewModel
        {
          InitialTargetEnvironment = env
        };

      return View(viewModel);
    }

    [HttpGet]
    public ActionResult GetProjectsForEnvironmentDeploy(string environmentName)
    {
      List<string> projects = _agentService.GetProjectsForEnvironmentDeploy(environmentName);

      var projectViewModels = new List<EnvDeployProjectViewModel>();

      if (projects != null && projects.Any())
      {
        projectViewModels = projects.Select(proj =>
          new EnvDeployProjectViewModel
          {
            Name = proj,
          })
          .ToList();
      }

      return
        Json(
          new { projects = projectViewModels },
          JsonRequestBehavior.AllowGet);
    }

    [HttpPost]
    public ActionResult DeployAll(string environmentName, string[] projectNames)
    {
      if (string.IsNullOrWhiteSpace(environmentName))
      {
        return BadRequest();
      }

      Guid uniqueClientId = _sessionService.UniqueClientId;
      string requesterIdentity = SecurityUtils.CurrentUsername;

      var projectsToDeploy = new List<ProjectToDeploy>();

      foreach (var projectName in projectNames)
      {
        Guid deploymentId = SetDeploymentState(environmentName, ProjectConfigurationName, projectName);

        projectsToDeploy.Add(
          new ProjectToDeploy
          {
            DeploymentId = deploymentId, 
            ProjectName = projectName
          });
      }

      _agentService.DeployEnvironmentAsync(uniqueClientId, requesterIdentity, environmentName, projectsToDeploy);

      return Json(new { Status = "OK" });
    }    

    private Guid SetDeploymentState(string environmentName, string projectConfigurationName, string projectName)
    {
      Guid deploymentId = Guid.NewGuid();

      var deploymentState =
        new DeploymentState(
          deploymentId,
          UserIdentity,
          projectName,
          projectConfigurationName,
          environmentName);

      _deploymentStateProvider.SetDeploymentState(
        deploymentId,
        deploymentState);

      return deploymentId;
    }
  }
}
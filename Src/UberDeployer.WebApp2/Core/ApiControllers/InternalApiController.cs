using System;
using System.Web.Mvc;

using UberDeployer.Agent.Proxy;
using UberDeployer.Agent.Proxy.Dto;
using UberDeployer.Common.SyntaxSugar;
using UberDeployer.WebApp2.Core.Connectivity;
using UberDeployer.WebApp2.Core.Infrastructure;
using UberDeployer.WebApp2.Core.Models.Api;
using UberDeployer.WebApp2.Core.Services;

using DependentProject = UberDeployer.WebApp2.Core.Models.Api.DependentProject;

namespace UberDeployer.WebApp2.Core.ApiControllers
{
  public class InternalApiController : UberDeployerWebAppController
  {
    private readonly IAgentService _agentService;
    private readonly IDeploymentStateProvider _deploymentStateProvider;

    public InternalApiController(IAgentService agentService, IDeploymentStateProvider deploymentStateProvider)
    {
      Guard.NotNull(agentService, "agentService");
      Guard.NotNull(deploymentStateProvider, "deploymentStateProvider");

      _agentService = agentService;
      _deploymentStateProvider = deploymentStateProvider;
    }

    public InternalApiController()
      : this(new AgentServiceClient(), new DeploymentStateProvider())
    {
    }

    [HttpGet]
    public ActionResult CollectCredentials(Guid? deploymentId, string environmentName, string machineName, string username)
    {
      if (!deploymentId.HasValue)
      {
        return BadRequest();
      }

      if (string.IsNullOrEmpty(username))
      {
        return BadRequest();
      }

      DeploymentState deploymentState =
        _deploymentStateProvider.FindDeploymentState(deploymentId.Value);

      if (deploymentState == null)
      {
        return Content("FAIL");
      }

      DeploymentHub.PromptForCredentials(
        deploymentId.Value,
        deploymentState.UserIdentity,
        deploymentState.ProjectName,
        deploymentState.ProjectConfigurationName,
        environmentName,
        machineName,
        username);

      return Content("OK");
    }

    [HttpPost]
    public ActionResult OnCredentialsCollected(Guid? deploymentId, string password)
    {
      if (!deploymentId.HasValue)
      {
        return BadRequest();
      }

      if (string.IsNullOrEmpty(password))
      {
        return BadRequest();
      }

      _agentService.SetCollectedCredentialsForAsynchronousWebCredentialsCollector(
        deploymentId.Value,
        password);

      return Content("OK");
    }

    [HttpGet]
    public ActionResult OnCollectCredentialsTimedOut(Guid? deploymentId)
    {
      if (!deploymentId.HasValue)
      {
        return BadRequest();
      }

      DeploymentState deploymentState =
        _deploymentStateProvider.FindDeploymentState(deploymentId.Value);

      if (deploymentState == null)
      {
        return Content("FAIL");
      }

      DeploymentHub.CancelPromptForCredentials(deploymentState.UserIdentity);

      return Content("OK");
    }

    [HttpPost]
    public ActionResult CollectScriptsToRun(CollectScriptsToRunRequest request)
    {
      if (request == null || !request.DeploymentId.HasValue || request.ScriptsToRun == null || request.ScriptsToRun.Length == 0)
      {
        return BadRequest();
      }

      DeploymentState deploymentState =
        _deploymentStateProvider.FindDeploymentState(request.DeploymentId.Value);

      if (deploymentState == null)
      {
        return Content("FAIL");
      }

      DeploymentHub.PromptForScriptsToRun(
        request.DeploymentId.Value,
        deploymentState.UserIdentity,
        deploymentState.ProjectName,
        deploymentState.ProjectConfigurationName,
        request.ScriptsToRun);

      return Content("OK");
    }

    [HttpPost]
    public ActionResult OnScriptsToRunCollected(CollectScriptsToRunResponse response)
    {
      if (response == null || !response.DeploymentId.HasValue || response.SelectedScripts == null || response.SelectedScripts.Length == 0)
      {
        return BadRequest();
      }

      _agentService.SetSelectedDbScriptsToRun(response.DeploymentId.Value, new DbScriptsToRunSelection
      {
        SelectedScripts = response.SelectedScripts,
        DatabaseScriptToRunSelectionType = response.IsMultiselect ? DatabaseScriptToRunSelectionType.Multiselect : DatabaseScriptToRunSelectionType.LastVersion
      });

      return Content("OK");
    }

    [HttpPost]
    public ActionResult OnScriptsToRunCanceled(Guid? deploymentId)
    {
      if (!deploymentId.HasValue)
      {
        return BadRequest();
      }

      _agentService.CancelDbScriptsSelection(deploymentId.Value);

      return Content("OK");
    }

    [HttpGet]
    public ActionResult OnCollectScriptsToRunTimedOut(Guid? deploymentId)
    {
      if (!deploymentId.HasValue)
      {
        return BadRequest();
      }

      DeploymentState deploymentState =
        _deploymentStateProvider.FindDeploymentState(deploymentId.Value);

      if (deploymentState == null)
      {
        return Content("FAIL");
      }

      DeploymentHub.CancelPromptForScriptsToRun(deploymentState.UserIdentity);

      return Content("OK");
    }

    /* Project Dependencies */

    [HttpPost]
    public ActionResult CollectDependenciesToDeploy(CollectProjectDependenciesToDeployRequest request)     
    {
      if (!request.DeploymentId.HasValue)
      {
        return BadRequest();
      }      

      DeploymentState deploymentState = _deploymentStateProvider.FindDeploymentState(request.DeploymentId.Value);

      DeploymentHub.PromptForProjectDependencies(request.DeploymentId, deploymentState.UserIdentity, request.DependentProjects);

      return Content("OK");
    }

    [HttpPost]
    public ActionResult OnDependenciesToRunCollected(CollectProjectDependenciesToDeployResponse response)
    {
      if (response == null || !response.DeploymentId.HasValue )
      {
        return BadRequest();
      }

      Converter<DependentProject, Agent.Proxy.Dto.DependentProject> convertFunc =
        dp =>
          new Agent.Proxy.Dto.DependentProject()
          {
            ProjectName = dp.ProjectName,
            BranchName = dp.BranchName,
            BuildNumber = dp.BuildNumber
          };

      _agentService.SetSelectedDependentProjectsToDeploy(response.DeploymentId.Value,
        response.DependenciesToDeploy.ConvertAll(convertFunc));

      return Content("OK");
    }

    [HttpPost]
    public ActionResult OnDependenciesToRunSkipped(Guid? deploymentId)
    {
      if (!deploymentId.HasValue)
      {
        return BadRequest();
      }

      _agentService.SkipDependentProjectsSelection(deploymentId.Value);

      return Content("OK");
    }
    
    [HttpPost]
    public ActionResult OnDependenciesToRunCancelled(Guid? deploymentId)
    {
      if (!deploymentId.HasValue)
      {
        return BadRequest();
      }

      _agentService.CancelDependentProjectsSelection(deploymentId.Value);

      return Content("OK");
    }

    [HttpGet]
    public ActionResult OnCollectDependenciesToDeployTimedOut(Guid? deploymentId)
    {
      if (!deploymentId.HasValue)
      {
        return BadRequest();
      }

      DeploymentState deploymentState =
        _deploymentStateProvider.FindDeploymentState(deploymentId.Value);

      if (deploymentState == null)
      {
        return Content("FAIL");
      }

      DeploymentHub.CancelPromptForProjectDependencies(deploymentState.UserIdentity);

      return Content("OK");
    }
  }
}
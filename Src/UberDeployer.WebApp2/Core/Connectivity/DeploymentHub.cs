using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNet.SignalR;

using UberDeployer.Common.SyntaxSugar;
using UberDeployer.WebApp2.Core.Models.Api;
using UberDeployer.WebApp2.Core.Services;

namespace UberDeployer.WebApp2.Core.Connectivity
{
  public class DeploymentHub : Hub
  {
    private static readonly Dictionary<string, string> _connectionIdUserIdentityDict = new Dictionary<string, string>();

    private readonly IDeploymentStateProvider _deploymentStateProvider;

    public DeploymentHub(IDeploymentStateProvider deploymentStateProvider)
    {
      Guard.NotNull(deploymentStateProvider, "deploymentStateProvider");

      _deploymentStateProvider = deploymentStateProvider;
    }

    public DeploymentHub()
      : this(new DeploymentStateProvider())
    {
    }

    public override Task OnConnected()
    {
      _connectionIdUserIdentityDict[UserIdentity] = Context.ConnectionId;

      return base.OnConnected();
    }

    public override Task OnDisconnected()
    {
      string userIdentity = UserIdentity;

      _connectionIdUserIdentityDict.Remove(userIdentity);
      _deploymentStateProvider.RemoveAllDeploymentStates(userIdentity);

      return base.OnDisconnected();
    }

    public static void PromptForCredentials(
      Guid deploymentId,
      string userIdentity,
      string projectName,
      string projectConfigurationName,
      string targetEnvironmentName,
      string machineName,
      string username)
    {
      Guard.NotEmpty(deploymentId, "deploymentId");
      Guard.NotNullNorEmpty(userIdentity, "userIdentity");
      Guard.NotNullNorEmpty(projectName, "projectName");
      Guard.NotNullNorEmpty(projectConfigurationName, "projectConfigurationName");
      Guard.NotNullNorEmpty(targetEnvironmentName, "targetEnvironmentName");
      Guard.NotNullNorEmpty(machineName, "machineName");
      Guard.NotNullNorEmpty(username, "username");

      dynamic client = GetClient(userIdentity);

      client.promptForCredentials(
        new
        {
          deploymentId,
          projectName,
          projectConfigurationName,
          targetEnvironmentName,
          machineName,
          username,
        });
    }

    public static void CancelPromptForCredentials(string userIdentity)
    {
      Guard.NotNullNorEmpty(userIdentity, "userIdentity");

      dynamic client = GetClient(userIdentity);

      client.cancelPromptForCredentials(new object());
    }

    public static void PromptForScriptsToRun(Guid deploymentId, string userIdentity, string projectName, string projectConfigurationName, string[] scriptsToRun)
    {
      Guard.NotEmpty(deploymentId, "deploymentId");
      Guard.NotNullNorEmpty(userIdentity, "userIdentity");
      Guard.NotNullNorEmpty(projectName, "projectName");
      Guard.NotNullNorEmpty(projectConfigurationName, "projectConfigurationName");
      Guard.NotNull(scriptsToRun, "scriptsToRun");

      dynamic client = GetClient(userIdentity);

      client.promptForScriptsToRun(
        new
        {
          deploymentId,
          projectName,
          projectConfigurationName,
          scriptsToRun
        });
    }

    public static void CancelPromptForScriptsToRun(string userIdentity)
    {
      Guard.NotNullNorEmpty(userIdentity, "userIdentity");

      dynamic client = GetClient(userIdentity);

      client.cancelPromptForScriptsToRun(new object());
    }

    public static void PromptForProjectDependencies(Guid? deploymentId, string userIdentity, List<DependentProject> dependentProjects)
    {
      dynamic client = GetClient(userIdentity);

      client.promptForProjectDependencies(
        new
        {
          deploymentId,
          dependentProjects
        });
    }

    public static void CancelPromptForProjectDependencies(string userIdentity)
    {
      Guard.NotNullNorEmpty(userIdentity, "userIdentity");

      dynamic client = GetClient(userIdentity);

      client.CancelPromptForProjectDependencies(new object());
    }

    private static dynamic GetClient(string userIdentity)
    {
      Guard.NotNullNorEmpty(userIdentity, "userIdentity");

      string connectionId;

      if (!_connectionIdUserIdentityDict.TryGetValue(userIdentity, out connectionId))
      {
        throw new ClientNotConnectedException(userIdentity);
      }

      IHubContext hubContext = GlobalHost.ConnectionManager.GetHubContext<DeploymentHub>();

      dynamic client = hubContext.Clients.Client(connectionId);

      if (client == null)
      {
        throw new ClientNotConnectedException(userIdentity);
      }

      return client;
    }

    private string UserIdentity
    {
      get
      {
        string userIdentity = Context.User.With(x => x.Identity).With(x => x.Name);

        if (string.IsNullOrEmpty(userIdentity))
        {
          throw new InvalidOperationException("No user identity!");
        }

        return userIdentity;
      }
    }
  }
}
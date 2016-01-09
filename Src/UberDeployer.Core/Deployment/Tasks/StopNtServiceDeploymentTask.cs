using UberDeployer.Common.SyntaxSugar;
using UberDeployer.Core.Deployment.Steps;
using UberDeployer.Core.Domain;
using UberDeployer.Core.Management.NtServices;

namespace UberDeployer.Core.Deployment.Tasks
{
  /// <summary>
  /// Stops NtService.
  /// </summary>
  public class StopNtServiceDeploymentTask : DeploymentTask
  {
    private readonly IArtifactsRepository _artifactsRepository;

    private readonly INtServiceManager _ntServiceManager;

    public StopNtServiceDeploymentTask(IProjectInfoRepository projectInfoRepository, 
      IEnvironmentInfoRepository environmentInfoRepository,
      INtServiceManager ntServiceManager)
      : base(projectInfoRepository, environmentInfoRepository)
    {
      Guard.NotNull(ntServiceManager, "ntServiceManager");

      _ntServiceManager = ntServiceManager;
    }

    protected override void DoPrepare()
    {
      EnvironmentInfo environmentInfo = GetEnvironmentInfo();
      string appServerMachineName = environmentInfo.AppServerMachineName;
      NtServiceProjectInfo projectInfo = GetProjectInfo<NtServiceProjectInfo>();

      // check if the service is present on the target machine
      bool serviceExists =
        _ntServiceManager
          .DoesServiceExist(appServerMachineName, projectInfo.NtServiceName);

      if (serviceExists)
      {
        // create a step for stopping the service
        AddSubTask(
          new StopNtServiceDeploymentStep(
            _ntServiceManager,
            appServerMachineName,
            projectInfo.NtServiceName));
      }
    }

    public override string Description
    {
      get
      {
        return
          string.Format(
            "Stop NT service '{0} ({1}:{2})' on '{3}'.",
            DeploymentInfo.ProjectName,
            DeploymentInfo.ProjectConfigurationName,
            DeploymentInfo.ProjectConfigurationBuildId,
            DeploymentInfo.TargetEnvironmentName);
      }
    }
  }
}

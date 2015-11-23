using System;
using System.Collections.Generic;
using System.Linq;
using UberDeployer.Common.SyntaxSugar;
using UberDeployer.Core.Deployment.Tasks;
using UberDeployer.Core.Domain;

namespace UberDeployer.Core.Deployment.Pipeline
{
  public class EnvDeploymentPipeline : IEnvDeploymentPipeline
  {
    public event EventHandler<DiagnosticMessageEventArgs> DiagnosticMessagePosted;
    
    public event EventHandler<DiagnosticMessageGroupEventArgs> DiagnosticMessageGroupOpened;

    public event EventHandler DiagnosticMessageGroupClosed;

    private readonly List<IDeploymentPipelineModule> _modules;

    public EnvDeploymentPipeline()
    {
      _modules = new List<IDeploymentPipelineModule>();
    }

    public void AddModule(IDeploymentPipelineModule module)
    {
      if (module == null)
      {
        throw new ArgumentNullException("module");
      }

      _modules.Add(module);
    }

    public void StartDeployment(string targetEnvironment, List<ProjectDeploymentData> projectDeployments, DeploymentContext deploymentContext)
    {
      Guard.NotNull(projectDeployments, "projectDeploymentInfos");
      Guard.NotNull(deploymentContext, "deploymentContext");

      if (projectDeployments.Any() == false)
      {
        PostDiagnosticMessage(string.Format("No projects to deploy on environment '{0}'.", targetEnvironment), DiagnosticMessageType.Info);
        return;
      }

      PostDiagnosticMessage(string.Format("Starting deployment of environment: '{0}'.", targetEnvironment), DiagnosticMessageType.Info);
      PostDiagnosticMessage(string.Format("Projects count to deploy: '{0}'.", projectDeployments.Count), DiagnosticMessageType.Info);

      deploymentContext.DateStarted = DateTime.UtcNow;

      int successfullyDeployed = 0;

      foreach (var projectDeployment in projectDeployments)
      {
        OpenDiagnosticMessageGroup(projectDeployment.ProjectInfo.Name);

        var isPrepared = PrepareProject(projectDeployment, deploymentContext);

        if (isPrepared == false)
        {
          continue;
        }

        var isDeployed = ExecuteProjectDeployment(projectDeployment, deploymentContext);

        if (isDeployed)
        {
          successfullyDeployed++;
        }

        CloseDiagnosticMessageGroup();
      }

      deploymentContext.DateFinished = DateTime.UtcNow;
      deploymentContext.FinishedSuccessfully = projectDeployments.Count == successfullyDeployed;

      int failedCount = projectDeployments.Count - successfullyDeployed;

      PostDiagnosticMessage(string.Format("Finished deployment of environment: '{0}', successfully deployed: {1}, failed: {2}", targetEnvironment, successfullyDeployed, failedCount), DiagnosticMessageType.Info);
      PostDiagnosticMessage(
        "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -",
        DiagnosticMessageType.Info);
    }

    private bool PrepareProject(ProjectDeploymentData projectDeploymentData, DeploymentContext deploymentContext)
    {
      DeploymentInfo deploymentInfo = projectDeploymentData.DeploymentInfo;
      DeploymentTask deploymentTask = projectDeploymentData.DeploymentTask;

      PostDiagnosticMessage(string.Format("Preparing {0} '{1}'.", (deploymentInfo.IsSimulation ? " (simulation)" : ""), deploymentTask.GetType().Name), DiagnosticMessageType.Info);

      deploymentTask.DiagnosticMessagePosted += OnDeploymentTaskDiagnosticMessagePosted;

      OnDeploymentTaskStarting(deploymentInfo, deploymentTask, deploymentContext);

      try
      {
        deploymentTask.Initialize(deploymentInfo);

        deploymentTask.Prepare();

        return true;
      }
      catch (Exception exc)
      {
        PostDiagnosticMessage(string.Format("Exception while preparing: {0}", exc.Message), DiagnosticMessageType.Error);
      }
      finally
      {
        deploymentTask.DiagnosticMessagePosted -= OnDeploymentTaskDiagnosticMessagePosted;
      }

      return false;
    }

    private bool ExecuteProjectDeployment(ProjectDeploymentData projectDeploymentData, DeploymentContext deploymentContext)
    {
      DeploymentInfo deploymentInfo = projectDeploymentData.DeploymentInfo;
      DeploymentTask deploymentTask = projectDeploymentData.DeploymentTask;

      PostDiagnosticMessage(string.Format("Starting{0} '{1}'.", (deploymentInfo.IsSimulation ? " (simulation)" : ""), deploymentTask.GetType().Name), DiagnosticMessageType.Info);

      deploymentTask.DiagnosticMessagePosted += OnDeploymentTaskDiagnosticMessagePosted;

      try
      {
        deploymentTask.Execute();

        PostDiagnosticMessage(
          string.Format("Finished{0} '{1}' (\"{2}\").", (deploymentInfo.IsSimulation ? " (simulation)" : ""),
            deploymentTask.GetType().Name, deploymentTask.Description), DiagnosticMessageType.Info);
        PostDiagnosticMessage(
          "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -",
          DiagnosticMessageType.Info);

        return true;
      }
      catch (Exception ex)
      {
        PostDiagnosticMessage(string.Format("Exception: {0}", ex.Message), DiagnosticMessageType.Error);
        LogInnerException(ex);
      }
      finally
      {
        // TODO IMM HI: catch exceptions; pass them upstream using some mechanisms like DeploymentTask.DiagnosticMessagePosted event
        OnDeploymentTaskFinished(deploymentInfo, deploymentTask, deploymentContext);

        deploymentTask.DiagnosticMessagePosted -= OnDeploymentTaskDiagnosticMessagePosted;
      }

      return false;
    }

    private void LogInnerException(Exception exc)
    {
      if (exc.InnerException != null && !string.IsNullOrEmpty(exc.InnerException.Message))
      {
        PostDiagnosticMessage(string.Format("Inner Exception: {0}", exc.InnerException.Message), DiagnosticMessageType.Error);

        LogInnerException(exc.InnerException);
      }
    }

    protected void PostDiagnosticMessage(string message, DiagnosticMessageType diagnosticMessageType)
    {
      if (string.IsNullOrEmpty(message))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "message");
      }

      OnDiagnosticMessagePosted(this, new DiagnosticMessageEventArgs(diagnosticMessageType, message));
    }

    protected void OpenDiagnosticMessageGroup(string groupName)
    {
      Guard.NotNullNorEmpty(groupName, "groupName");

      OnDiagnosticMessageGroupOpened(this, new DiagnosticMessageGroupEventArgs(groupName));
    }

    protected void CloseDiagnosticMessageGroup()
    {
      OnDiagnosticMessageGroupClosed(this);
    }

    protected void OnDiagnosticMessageGroupClosed(object sender)
    {
      if (DiagnosticMessageGroupClosed != null)
      {
        DiagnosticMessageGroupClosed(sender, null);
      }
    }

    protected void OnDiagnosticMessageGroupOpened(object sender, DiagnosticMessageGroupEventArgs diagnosticMessageGroupEventArgs)
    {
      if (DiagnosticMessageGroupOpened != null)
      {
        DiagnosticMessageGroupOpened(sender, diagnosticMessageGroupEventArgs);
      }
    }

    protected void OnDiagnosticMessagePosted(object sender, DiagnosticMessageEventArgs diagnosticMessageEventArgs)
    {
      if (DiagnosticMessagePosted != null)
      {
        DiagnosticMessagePosted(sender, diagnosticMessageEventArgs);
      }
    }

    private void OnDeploymentTaskStarting(DeploymentInfo deploymentInfo, DeploymentTask deploymentTask, DeploymentContext deploymentContext)
    {
      foreach (IDeploymentPipelineModule deploymentPipelineModule in _modules)
      {
        deploymentPipelineModule.OnDeploymentTaskStarting(deploymentInfo, deploymentTask, deploymentContext);
      }
    }

    private void OnDeploymentTaskFinished(DeploymentInfo deploymentInfo, DeploymentTask deploymentTask, DeploymentContext deploymentContext)
    {
      foreach (IDeploymentPipelineModule deploymentPipelineModule in _modules)
      {
        deploymentPipelineModule.OnDeploymentTaskFinished(deploymentInfo, deploymentTask, deploymentContext);
      }
    }

    private void OnDeploymentTaskDiagnosticMessagePosted(object sender, DiagnosticMessageEventArgs e)
    {
      OnDiagnosticMessagePosted(this, e);
    }
  }
}

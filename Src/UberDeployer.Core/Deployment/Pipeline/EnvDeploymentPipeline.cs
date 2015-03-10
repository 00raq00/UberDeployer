﻿using System;
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

      List<ProjectDeploymentData> projectsPreparedSuccessfully = PrepareProjectsBeforeDeploy(projectDeployments, deploymentContext);

      List<ProjectDeploymentData> projectsDeployedSuccessfully = DeployProjects(targetEnvironment, deploymentContext, projectsPreparedSuccessfully);

      deploymentContext.DateFinished = DateTime.UtcNow;
      deploymentContext.FinishedSuccessfully = projectDeployments.Count == projectsDeployedSuccessfully.Count;

      int failedCount = projectDeployments.Count - projectsDeployedSuccessfully.Count;

      PostDiagnosticMessage(string.Format("Finished deployment of environment: '{0}', successfully deployed: {1}, failed: {2}", targetEnvironment, projectsDeployedSuccessfully.Count, failedCount), DiagnosticMessageType.Info);
      PostDiagnosticMessage(
        "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -",
        DiagnosticMessageType.Info);
    }

    private List<ProjectDeploymentData> DeployProjects(string targetEnvironment, DeploymentContext deploymentContext, List<ProjectDeploymentData> projectDeployments)
    {
      var deployedSuccessfully = new List<ProjectDeploymentData>();

      IEnumerable<ProjectDeploymentData> dbProjectsToDeploy = projectDeployments.FindAll(x => x.ProjectInfo.Type == ProjectType.Db);

      IEnumerable<ProjectDeploymentData> otherProjectsToDeploy = projectDeployments.FindAll(x => x.ProjectInfo.Type != ProjectType.Db);

      // first deploy databases
      if (dbProjectsToDeploy.Any())
      {
        PostDiagnosticMessage(string.Format("Start deploying database projects on evnfironment: '{0}'", targetEnvironment), DiagnosticMessageType.Info);

        foreach (var dbProjectDeployment in dbProjectsToDeploy)
        {
          bool executedSuccessfully = ExecuteProjectDeployment(dbProjectDeployment, deploymentContext);
          
          if(executedSuccessfully) {
            deployedSuccessfully.Add(dbProjectDeployment);
          }
        }
      }

      if (otherProjectsToDeploy.Any())
      {
        PostDiagnosticMessage(string.Format("Start deploying other projects on evnfironment: '{0}'", targetEnvironment), DiagnosticMessageType.Info);

        foreach (var otherProjectDeployment in otherProjectsToDeploy)
        {
          bool executedSuccessfully = ExecuteProjectDeployment(otherProjectDeployment, deploymentContext);

          if (executedSuccessfully)
          {
            deployedSuccessfully.Add(otherProjectDeployment);
          }
        }
      }

      return deployedSuccessfully;
    }

    private List<ProjectDeploymentData> PrepareProjectsBeforeDeploy(List<ProjectDeploymentData> projectDeployments, DeploymentContext deploymentContext)
    {
      PostDiagnosticMessage(string.Format("Start preparing projects before deploy."), DiagnosticMessageType.Info);

      var projectsPreparedSuccessfully = new List<ProjectDeploymentData>();

      foreach (var projectDeployment in projectDeployments)
      {
        bool preparedSuccessfully = PrepareProject(projectDeployment, deploymentContext);

        if (preparedSuccessfully)
        {
          projectsPreparedSuccessfully.Add(projectDeployment);
        }
      }

      PostDiagnosticMessage(
        string.Format("Finished preparing projects, successfully prepared: {0}, failed: {1}.", projectsPreparedSuccessfully.Count, projectDeployments.Count - projectsPreparedSuccessfully.Count),
        DiagnosticMessageType.Info);

      return projectsPreparedSuccessfully;
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
      }
      finally
      {        
        // TODO IMM HI: catch exceptions; pass them upstream using some mechanisms like DeploymentTask.DiagnosticMessagePosted event
        OnDeploymentTaskFinished(deploymentInfo, deploymentTask, deploymentContext);

        deploymentTask.DiagnosticMessagePosted -= OnDeploymentTaskDiagnosticMessagePosted;
      }

      return false;
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

    protected void PostDiagnosticMessage(string message, DiagnosticMessageType diagnosticMessageType)
    {
      if (string.IsNullOrEmpty(message))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "message");
      }

      OnDiagnosticMessagePosted(this, new DiagnosticMessageEventArgs(diagnosticMessageType, message));
    }

    protected void OnDiagnosticMessagePosted(object sender, DiagnosticMessageEventArgs diagnosticMessageEventArgs)
    {
      var eventHandler = DiagnosticMessagePosted;

      if (eventHandler != null)
      {
        eventHandler(sender, diagnosticMessageEventArgs);
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

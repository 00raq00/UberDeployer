using System;
using System.Collections.Generic;
using UberDeployer.Core.Domain;

namespace UberDeployer.Core.Deployment.Pipeline
{
  public interface IEnvDeploymentPipeline
  {
    event EventHandler<DiagnosticMessageEventArgs> DiagnosticMessagePosted;

    void AddModule(IDeploymentPipelineModule module);

    void StartDeployment(string targetEnvironment, List<ProjectDeploymentData> projectDeployments,DeploymentContext deploymentContext);
  }
}

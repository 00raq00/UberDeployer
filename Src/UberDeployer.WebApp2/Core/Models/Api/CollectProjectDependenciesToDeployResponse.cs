using System;
using System.Collections.Generic;

namespace UberDeployer.WebApp2.Core.Models.Api
{
  public class CollectProjectDependenciesToDeployResponse
  {
    public Guid? DeploymentId { get; set; }

    public List<DependentProject> DependenciesToDeploy { get; set; }
  }
}
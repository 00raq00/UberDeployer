using System.Collections.Generic;

namespace UberDeployer.WebApp2.Core.Models.Api
{
  public class ProjectMetadataViewModel
  {
    public string Status { get; set; }

    public string ProjectName { get; set; }

    public string EnvironmentName { get; set; }

    public List<MachineSpecificProjectVersionViewModel> ProjectVersions { get; set; }
  }
}

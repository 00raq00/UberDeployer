using System.Collections.Generic;
using System.Xml.Serialization;

namespace UberDeployer.Core.DataAccess.Xml.ProjectInfo.XmlModel
{
  public abstract class ProjectInfoXml
  {
    private string _allowedEnvironments;

    public string Name { get; set; }

    public string Type { get; set; }

    public string ArtifactsRepositoryName { get; set; }

    public string ArtifactsRepositoryDirName { get; set; }

    public bool ArtifactsAreNotEnvironmentSpecific { get; set; }

    [XmlAttribute("allowedEnvironments")]
    public string AllowedEnvironments
    {
      get { return _allowedEnvironments; }
      set { _allowedEnvironments = value; }
    }

    [XmlArrayItem("ProjectName")]
    public List<string> DependentProjects { get; set; }
  }
}

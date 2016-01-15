using UberDeployer.Core.Domain;

namespace UberDeployer.Core.DataAccess.Xml.EnvironmentInfo.XmlModel
{
  public class AppPoolInfoXml
  {
    public string Name { get; set; }

    public IisAppPoolVersion Version { get; set; }

    public IisAppPoolMode Mode { get; set; }
  }
}

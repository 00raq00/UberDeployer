using System.Collections.Generic;
using System.Xml.Serialization;

namespace UberDeployer.Core.DataAccess.Xml.ProjectInfo.XmlModel
{
  public class DbProjectInfoXml : ProjectInfoXml
  {
    public string DbName { get; set; }

    public string DatabaseServerId { get; set; }

    public bool IsTransacional { get; set; }

    public string DacpacFile { get; set; }

    [XmlArray("Users")]
    [XmlArrayItem("UserId")]
    public List<string> Users { get; set; }
  }
}

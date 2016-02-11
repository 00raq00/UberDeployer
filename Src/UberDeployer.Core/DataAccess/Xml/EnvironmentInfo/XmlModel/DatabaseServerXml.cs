using System.Collections.Generic;

namespace UberDeployer.Core.DataAccess.Xml.EnvironmentInfo.XmlModel
{
  public class DatabaseServerXml
  {
    public string Id { get; set; }

    public string MachineName { get; set; }

    public string DataDirPath { get; set; }

    public string LogDirPath { get; set; }

    public List<Variable> SqlPackageVariables { get; set; }
  }
}

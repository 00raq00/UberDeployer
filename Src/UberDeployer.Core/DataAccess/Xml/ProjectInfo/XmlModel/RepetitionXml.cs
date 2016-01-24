namespace UberDeployer.Core.DataAccess.Xml.ProjectInfo.XmlModel
{
  public class RepetitionXml
  {
    public bool Enabled { get; set; }

    public string Interval { get; set; }

    public string Duration { get; set; }

    public bool StopAtDurationEnd { get; set; }
  }
}

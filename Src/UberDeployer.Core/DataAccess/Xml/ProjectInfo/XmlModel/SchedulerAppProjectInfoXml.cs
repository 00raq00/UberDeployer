using System.Collections.Generic;

namespace UberDeployer.Core.DataAccess.Xml.ProjectInfo.XmlModel
{
  public class SchedulerAppProjectInfoXml : ProjectInfoXml
  {
    public string SchedulerAppDirName { get; set; }

    public string SchedulerAppExeName { get; set; }

    public List<SchedulerAppTaskXml> SchedulerAppTasks { get; set; }
  }
}

namespace UberDeployer.Core.DataAccess.Xml.ProjectInfo.XmlModel
{
  public class SchedulerAppTaskXml
  {
    public string Name { get; set; }

    public string ExecutableName { get; set; }

    public string UserId { get; set; }

    public int ScheduledHour { get; set; }

    public int ScheduledMinute { get; set; }

    /// <summary>
    /// 0 - no limit.
    /// </summary>
    public int ExecutionTimeLimitInMinutes { get; set; }

    public RepetitionXml Repetition { get; set; }
  }
}

using System;
using UberDeployer.Common.SyntaxSugar;

namespace UberDeployer.Core.Deployment
{
  public class DiagnosticMessageGroupEventArgs : EventArgs
  {
    public DiagnosticMessageGroupEventArgs(string groupName)
    {
      Guard.NotNullNorEmpty(groupName, "groupName");
      
      GroupName = groupName;
    }

    public string GroupName { get; private set; }
  }
}

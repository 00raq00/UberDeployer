using System;

namespace UberDeployer.WebApp2.Core
{
  [Serializable]
  public class InternalException : Exception
  {
    public InternalException(string message, Exception innerException)
      : base(message, innerException)
    {
    }

    public InternalException(string message)
      : this(message, null)
    {
    }
  }
}

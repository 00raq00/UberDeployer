using System;

namespace UberDeployer.WebApp2.Core.Services
{
  public interface ISessionService
  {
    Guid UniqueClientId { get; }
  }
}

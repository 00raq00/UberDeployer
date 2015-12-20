using System;
using System.Web;
using System.Web.SessionState;

namespace UberDeployer.WebApp2.Core.Services
{
  public class SessionService : ISessionService
  {
    private const string SessionKey_UniqueClientId = "UniqueClientId";

    public Guid UniqueClientId
    {
      get
      {
        Guid uniqueClientId;

        if (Session[SessionKey_UniqueClientId] == null)
        {
          uniqueClientId = Guid.NewGuid();
          Session[SessionKey_UniqueClientId] = uniqueClientId;
        }
        else
        {
          uniqueClientId = (Guid)Session[SessionKey_UniqueClientId];
        }

        return uniqueClientId;
      }
    }

    private static HttpSessionState Session
    {
      get { return HttpContext.Current.Session; }
    }
  }
}

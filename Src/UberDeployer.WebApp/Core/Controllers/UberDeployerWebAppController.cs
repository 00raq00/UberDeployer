using System.Security.Principal;
using System.Web.Mvc;

namespace UberDeployer.WebApp.Core.Controllers
{
  public abstract class UberDeployerWebAppController : Controller
  {
    protected ActionResult BadRequest()
    {
      Response.StatusCode = 400;

      return Content("400 - Bad Request");
    }

    protected string CurrentUsername
    {
      get
      {
        WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();

        if (windowsIdentity != null)
        {
          return windowsIdentity.Name;
        }

        throw new InternalException("Couldn't get current username.");
      }
    }
  }
}
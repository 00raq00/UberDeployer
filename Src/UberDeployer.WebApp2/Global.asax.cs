using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
using System.Web.Http;

using log4net;
using log4net.Config;

using UberDeployer.Common;
using UberDeployer.WebApp2.Core.Infrastructure;

namespace UberDeployer.WebApp2
{
  public class Global : HttpApplication
  {
    private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private void Application_Start(object sender, EventArgs e)
    {
      GlobalContext.Properties["applicationName"] = "UberDeployer.WebApp";
      XmlConfigurator.Configure();

      RouteTable.Routes.MapHubs();

      ViewEngines.Engines.Add(new UberDeployerViewEngine());

      AreaRegistration.RegisterAllAreas();

      GlobalConfiguration.Configure(WebApiConfig.Register);

      RouteConfig.RegisterRoutes(RouteTable.Routes);

      GlobalFilters.Filters.Add(new HandleErrorAttribute());

      _log.InfoIfEnabled(() => "Application has started.");
    }

    protected void Application_Error()
    {
      Exception exception = Server.GetLastError();
      HttpException httpException = exception as HttpException;

      if (httpException != null)
      {
        if (httpException.GetHttpCode() == 404)
        {
          return;
        }
      }

      _log.ErrorIfEnabled(() => "Unhandled exception.", exception);
    }
  }
}
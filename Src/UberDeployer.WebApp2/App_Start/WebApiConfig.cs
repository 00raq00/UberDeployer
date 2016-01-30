using System.Web.Http;

namespace UberDeployer.WebApp2
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
          // Disabled till ApiController will be changed to proper WebApi controller

          // Web API configuration and services

          // Web API routes
          //            config.MapHttpAttributeRoutes();
          //
          //            config.Routes.MapHttpRoute(
          //                name: "DefaultApi",
          //                routeTemplate: "api/{controller}/{id}",
          //                defaults: new { id = RouteParameter.Optional }
          //            );
        }
    }
}

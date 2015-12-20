using System.Linq;
using System.Web.Mvc;

namespace UberDeployer.WebApp2.Core.Infrastructure
{
  public class UberDeployerViewEngine : RazorViewEngine
  {
    public UberDeployerViewEngine()
    {
      var newLocationFormats = new[]
      {
        "~/Views/{1}/Popups/{0}.cshtml"
      };

      PartialViewLocationFormats = PartialViewLocationFormats.Union(newLocationFormats).ToArray();
    }
  }
}
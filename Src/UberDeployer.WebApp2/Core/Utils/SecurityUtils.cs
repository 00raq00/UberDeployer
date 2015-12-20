using System.Threading;
using System.Web;

using UberDeployer.Common;
using UberDeployer.Common.SyntaxSugar;

namespace UberDeployer.WebApp2.Core.Utils
{
  public class SecurityUtils
  {
    private const string _AppSettingKey_CanDeployRole = "CanDeployRole";

    private static readonly string _canDeployRole;

    static SecurityUtils()
    {
      _canDeployRole = AppSettingsUtils.ReadAppSettingStringOptional(_AppSettingKey_CanDeployRole);
    }

    public static string CurrentUsername
    {
      get
      {
        return HttpContext.Current.With(x => x.User).With(x => x.Identity).With(x => x.Name) ?? "?";
      }
    }

    public static bool CanDeploy
    {
      get
      {
        return string.IsNullOrEmpty(_canDeployRole) || Thread.CurrentPrincipal.IsInRole(_canDeployRole);
      }
    }
  }
}
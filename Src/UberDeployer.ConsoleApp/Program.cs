using System;
using System.IO;
using log4net;
using log4net.Config;

using System.Reflection;
using UberDeployer.CommonConfiguration;
using UberDeployer.ConsoleApp.Commander;

namespace UberDeployer.ConsoleApp
{
  internal class Program
  {
    private static int Main(string[] args)
    {
      GlobalContext.Properties["applicationName"] = "UberDeployer.ConsoleApp";
      XmlConfigurator.Configure();

      TextWriter outputWriter = Console.Out;

      try
      {
        Bootstraper.Bootstrap();

        var commandDispatcher = new CommandDispatcher(outputWriter);
        
        commandDispatcher.DiscoverCommands(Assembly.GetExecutingAssembly());

        if (args.Length == 0)
        {
          commandDispatcher.DisplayAvailableCommands();

          return 1;
        }

        return commandDispatcher.Dispatch(args);
      }
      catch (Exception exc)
      {
        outputWriter.WriteLine(exc);

        return 1;
      }
    }
  }
}

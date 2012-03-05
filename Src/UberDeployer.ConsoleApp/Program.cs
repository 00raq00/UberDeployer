﻿using System;
using System.IO;
using UberDeployer.ConsoleCommander;
using System.Reflection;
using UberDeployer.CommonConfiguration;

namespace UberDeployer.ConsoleApp
{
  internal class Program
  {
    private static void Main(string[] args)
    {
      TextWriter outputWriter = Console.Out;

      try
      {
        Bootstraper.Bootstrap();

        var commandDispatcher = new CommandDispatcher(outputWriter);
        
        commandDispatcher.DiscoverCommands(Assembly.GetExecutingAssembly());

        if (args.Length == 0)
        {
          commandDispatcher.DisplayAvailableCommands();
          return;
        }

        commandDispatcher.Dispatch(args);
      }
      catch (Exception exc)
      {
        outputWriter.WriteLine(exc);
      }
    }
  }
}

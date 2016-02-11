using System;
using System.IO;

namespace UberDeployer.ConsoleApp.Commander
{
  public abstract class ConsoleCommand
  {
    protected readonly CommandDispatcher _commandDispatcher;

    protected ConsoleCommand(CommandDispatcher commandDispatcher)
    {
      if (commandDispatcher == null)
      {
        throw new ArgumentNullException("commandDispatcher");
      }

      _commandDispatcher = commandDispatcher;
    }

    public abstract int Run(string[] args);

    public virtual void DisplayCommandUsage()
    {
      OutputWriter.WriteLine("Usage: {0}", CommandName);
    }

    public abstract string CommandName { get; }

    protected TextWriter OutputWriter
    {
      get { return _commandDispatcher.OutputWriter; }
    }
  }
}

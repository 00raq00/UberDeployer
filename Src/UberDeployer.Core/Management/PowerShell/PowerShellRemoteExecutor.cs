﻿using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using UberDeployer.Common.SyntaxSugar;
using UberDeployer.Core.Deployment.Tasks;

namespace UberDeployer.Core.Management.PowerShell
{
  public class PowerShellRemoteExecutor : IPowerShellRemoteExecutor
  {
    private readonly string _machineName;
    private readonly Action<string> _onOutput;
    private readonly Action<string> _onError;

    private int _errorCount = 0;

    public PowerShellRemoteExecutor(string machineName, Action<string> onOutput, Action<string> onError)
    {
      Guard.NotNullNorEmpty(machineName, "machineName");

      _machineName = machineName;
      _onOutput = onOutput;
      _onError = onError;
    }

    public PSObject Execute(string script)
    {
      _errorCount = 0;

      var connectionInfo = new WSManConnectionInfo
      {
        ComputerName = _machineName
      };

      using (Runspace runspace = RunspaceFactory.CreateRunspace(connectionInfo))
      {
        runspace.Open();

        using (var powerShell = System.Management.Automation.PowerShell.Create())
        {
          powerShell.Runspace = runspace;

          powerShell.AddScript(script);

          powerShell.Streams.Error.DataAdded += OnError;
          powerShell.Streams.Debug.DataAdded += OnDebug;
          powerShell.Streams.Warning.DataAdded += OnWarning;
          powerShell.Streams.Progress.DataAdded += OnProgress;
          powerShell.Streams.Verbose.DataAdded += OnVerbose;

          var outputCollection = new PSDataCollection<PSObject>();

          outputCollection.DataAdded += OnOutput;

          IAsyncResult invokeResult = powerShell.BeginInvoke<PSObject, PSObject>(null, outputCollection);

          powerShell.EndInvoke(invokeResult);

          if (_errorCount != 0)
          {
            // TODO MARIO custom exception
            throw new DeploymentTaskException("Failed executing PowerShell script");
          }

          return outputCollection.LastOrDefault();
        }
      }
    }

    private void OnOutput(object sender, DataAddedEventArgs e)
    {
      WriteOutput("PS", sender, e);
    }

    private void OnVerbose(object sender, DataAddedEventArgs e)
    {
      WriteOutput("PS VERBOSE: ", sender, e);
    }

    private void OnProgress(object sender, DataAddedEventArgs e)
    {
      WriteOutput("PS PROGRESS", sender, e);
    }

    private void OnWarning(object sender, DataAddedEventArgs e)
    {
      WriteOutput("PS WARNING", sender, e);
    }

    private void OnDebug(object sender, DataAddedEventArgs e)
    {
      WriteOutput("PS DEBUG", sender, e);
    }

    private void OnError(object sender, DataAddedEventArgs e)
    {
      _errorCount++;
      WriteError("PS ERROR", sender ,e);
    }

    private void WriteOutput(string prefix, object sender, DataAddedEventArgs e)
    {
      if (_onOutput == null)
      {
        return;
      }

      var psDataCollection = sender as PSDataCollection<PSObject>;
      if (psDataCollection != null)
      {
        PSObject psObject = psDataCollection[e.Index];
        
        _onOutput(string.Format("{0}: {1}", prefix, psObject));
      }
    }

    private void WriteError(string prefix, object sender, DataAddedEventArgs e)
    {
      if (_onError == null)
      {
        return;
      }

      var psErrorCollection = sender as PSDataCollection<ErrorRecord>;
      if (psErrorCollection != null)
      {
        ErrorRecord error = psErrorCollection[e.Index];
        _onError(string.Format("{0}: {1}", prefix, error.Exception));
      }
    }
  }
}
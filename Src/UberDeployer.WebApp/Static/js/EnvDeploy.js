var UberDeployer = UberDeployer || {};

UberDeployer.EnvDeploy = function() {
  var _initialSelection = null;

  var _projects = null;

  var _self = this;

  var _logsTreeGrid;

  var doDeployEnv = function() {
    var targetEnvironmentName = getSelectedTargetEnvironmentName();

    $.ajax({
      url: g_AppPrefix + 'EnvDeployment/DeployAll',
      type: "POST",
      data: {
        environmentName: targetEnvironmentName,        
        projectNames: Object.keys(_projects)
      },
      traditional: true,
      success: function(data) {
        handleApiErrorIfPresent(data);
      }
    });
  };

  var loadProjectsForCurrentEnvironmentDeploy = function() {
    clearProjects();

    $.getJSON(g_AppPrefix + 'EnvDeployment/GetProjectsForEnvironmentDeploy', { environmentName: getSelectedTargetEnvironmentName() })
      .done(
        function(data) {
          _projects = [];
          clearProjects();

          $.each(data.projects, function(i, val) {
            _projects[val.Name] = new Project(val.Name, val.Type, val.AllowedEnvironmentNames);

            domHelper.getProjectsElement()
              .append(
                $('<option></option>')
                  .attr('value', val.Name)
                  .text(val.Name));
          });
        });
  };
  
  var setupSignalR = function() {
    var deploymentHub = $.connection.deploymentHub;

    deploymentHub.client.connected = function() {
    };
    deploymentHub.client.disconnected = function() {
    };

    $.connection.hub.start();
  };

  var ConfirmRestoreDialog = (function () {
    function ConfirmRestoreDialog() {
      var self = this;
      $('#dlg-confirm-restore-ok')
        .click(function () {
          self.closeDialog();
          doDeployEnv();
        });
    };

    ConfirmRestoreDialog.prototype.showDialog = function (targetEnvironmentName) {
      $('#dlg-confirm-restore-target-environment-name').html(targetEnvironmentName);
      $('#dlg-confirm-restore').modal('show');
    };

    ConfirmRestoreDialog.prototype.closeDialog = function () {
      $('#dlg-confirm-restore-target-environment-name').html('');
      $('#dlg-confirm-restore').modal('hide');
    };

    return ConfirmRestoreDialog;
  }());

  return {
    initializeEnvDeploymentPage : function(initData) {
      _initialSelection = initData.initialSelection;

      setupSignalR();

      var confirmRestoreDialog = new ConfirmRestoreDialog();

      $.ajaxSetup({
        'error': function(xhr) {
          domHelper.showError(xhr);
        }
      });

      $('#btn-deployEnv').click(function () {
        confirmRestoreDialog.showDialog(getSelectedTargetEnvironmentName());
      });

      loadEnvironments(function() {
        if (g_initialSelection && g_initialSelection.targetEnvironmentName) {
          selectEnvironment(g_initialSelection.targetEnvironmentName);
        } else {
          restoreRememberedTargetEnvironmentName();
        }
      });

      domHelper.getEnvironmentsElement().change(function() {
        if (!g_initialSelection || !g_initialSelection.targetEnvironmentName) {
          rememberTargetEnvironmentName();
        }

        loadProjectsForCurrentEnvironmentDeploy();
      });

      _logsTreeGrid = UberDeployer.createLogsTreeGrid(g_AppPrefix, '#tree-logs', g_DiagnosticMessagesLoaderInterval);
      
      _logsTreeGrid.startTreeGridLogsLoader();
    }
  };
}();


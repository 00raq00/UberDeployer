var UberDeployer = UberDeployer || {};

UberDeployer.EnvDeploy = function() {
  var _initialSelection = null;

  var _projects = null;

  var _self = this;

  var _currentDiagnosticMessageGroupId = null;

  var _currentDiagnosticMessageGroupName = null;

  var _lastSeenMessageId = -1;

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
  
  function startDiagnosticMessagesTreeLoader() {
    loadNewDiagnosticMessagesToTree();

    setTimeout(
      function () {
        startDiagnosticMessagesTreeLoader();
      },
      g_DiagnosticMessagesLoaderInterval);
  }

  function loadNewDiagnosticMessagesToTree() {
    $.getJSON(
      g_AppPrefix + 'Api/GetDiagnosticMessages?lastSeenMaxMessageId=' + _lastSeenMessageId,
      function (data) {
        $.each(data.messages, function (i, val) {
          if (val.MessageId > _lastSeenMessageId) {
            _lastSeenMessageId = val.MessageId;
            logMessageTree(val.Message, val.Type, val.GroupName, val.MessageId);
          }
        });
      });
  }

  function logMessageTree(message, type, groupName, messageId) {
    var $treeLogs = $('#tree-logs');
    var $logRow = $('<tr></tr>');
    $logRow.addClass("treegrid-" + messageId);
    
    var $logCell = $("<td></td>");
    $logCell.text(">> " + message);

    $logCell.addClass('log-msg');

    setLogType($logRow, type);

    if (!groupName) {
      // add regular log
      _currentDiagnosticMessageGroupName = null;
      _currentDiagnosticMessageGroupId = null;
    }
    else if (_currentDiagnosticMessageGroupName == groupName) {
      // add to existing group
      $logRow.addClass("treegrid-parent-" + _currentDiagnosticMessageGroupId);
      
      if (type.toLowerCase() == "error") {
        var $groupRow = $('.' + 'treegrid-' + _currentDiagnosticMessageGroupId);

        var logClassPattern = /log-msg-*/;

        $groupRow[0].className.split(/\s+/)
          .filter(function(value) {
            return logClassPattern.test(value);
          }).forEach(function(logClass) {
            $groupRow.removeClass(logClass);
          });
        
        $groupRow.addClass("log-msg-error");
      }
    }
    else {
      // create new group
      _currentDiagnosticMessageGroupName = groupName;
      _currentDiagnosticMessageGroupId = messageId;

      $logRow.addClass("active");
    }

    $logRow.append($logCell);
    $treeLogs.append($logRow);
    $treeLogs.treegrid({
       "initialState":"collapsed"
    });

    //$treeLogs.scrollTop($treeLogs[0].scrollHeight - $treeLogs.height());
  }
  
  function setLogType(logRow, type) {
    var typeLower = type.toLowerCase();

    if (typeLower === 'trace') {
      logRow.addClass('log-msg-trace');
    }
    else if (typeLower == 'info') {
      logRow.addClass('log-msg-info');
    }
    else if (typeLower == 'warn') {
      logRow.addClass('log-msg-warn');
    }
    else if (typeLower == 'error') {
      logRow.addClass('log-msg-error');
    }
  }

  function clearLogs() {
    var $txtLogs = $('#txt-logs');

    $txtLogs.find('*').remove();
  }

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
  })();

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

      $('#tree-logs').treegrid();
      startDiagnosticMessagesTreeLoader();
    }
  };
}();


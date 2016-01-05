var UberDeployer = UberDeployer || {};

UberDeployer.createLogsTreeGrid = function (appPrefix, treeLogsTableSelector, logsLoaderInterval) {

  var _treeLogsTableSelector = treeLogsTableSelector;
  var _appPrefix = appPrefix;
  var _logsLoaderInterval = logsLoaderInterval;
  var _currentDiagnosticMessageGroupId = null;
  var _currentDiagnosticMessageGroupName = null;
  var _lastSeenMessageId = -1;

  function loadNewDiagnosticMessagesToTree() {
    $.getJSON(
      _appPrefix + 'Api/GetDiagnosticMessages?lastSeenMaxMessageId=' + _lastSeenMessageId,
      function(data) {
        $.each(data.messages, function(i, val) {
          if (val.MessageId > _lastSeenMessageId) {
            _lastSeenMessageId = val.MessageId;
            logMessageTree(val.Message, val.Type, val.GroupName, val.MessageId);
          }
        });
      });
  }

  function logMessageTree(message, type, groupName, messageId) {
    var $treeLogs = $(_treeLogsTableSelector);
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
    } else if (_currentDiagnosticMessageGroupName == groupName) {
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
    } else {
      // create new group
      _currentDiagnosticMessageGroupName = groupName;
      _currentDiagnosticMessageGroupId = messageId;

      $logRow.addClass("active");
    }

    $logRow.append($logCell);
    $treeLogs.append($logRow);
    $treeLogs.treegrid({
      "initialState": "collapsed"
    });

    //$treeLogs.scrollTop($treeLogs[0].scrollHeight - $treeLogs.height());
  }

  function setLogType(logRow, type) {
    var typeLower = type.toLowerCase();

    if (typeLower === 'trace') {
      logRow.addClass('log-msg-trace');
    } else if (typeLower == 'info') {
      logRow.addClass('log-msg-info');
    } else if (typeLower == 'warn') {
      logRow.addClass('log-msg-warn');
    } else if (typeLower == 'error') {
      logRow.addClass('log-msg-error');
    }
  }

  function startLogsLoader() {
    loadNewDiagnosticMessagesToTree();

    setTimeout(
      startLogsLoader,
      _logsLoaderInterval);
  }

  return {
    startTreeGridLogsLoader: function () {
      $(_treeLogsTableSelector).treegrid();
      
      startLogsLoader();
    },

    clearLogs: function () {
      $(_treeLogsTableSelector).find('*').remove();
    }
  };
};
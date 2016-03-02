var UberDeployer = UberDeployer || {};

UberDeployer.createLogsTreeGrid = function (appPrefix, treeLogsTableSelector, logsLoaderInterval) {

  var _treeLogsTableSelector = treeLogsTableSelector;
  var _appPrefix = appPrefix;
  var _logsLoaderInterval = logsLoaderInterval;
  var _currentLogGroupId = null;
  var _currentLogGroupName = null;
  var _lastSeenMessageId = -1;

  function loadNewLogsToTree() {
    $.getJSON(
      _appPrefix + "Api/GetDiagnosticMessages?lastSeenMaxMessageId=" + _lastSeenMessageId,
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

    var logRow = createLogRow(message, messageId);

    setLogType(logRow, type);

    if (!groupName) {
      _currentLogGroupName = null;
      _currentLogGroupId = null;
    }
    else if (_currentLogGroupName === groupName) {
      addLogRowToGroup(logRow, _currentLogGroupId, type);
    }
    else {
      setNewLogGroup(logRow, groupName, messageId);
    }
    
    $treeLogs.append(logRow);
    $treeLogs.treegrid({
      "initialState": "collapsed"
    });

    scrollToTheBottom($treeLogs.parent());
  }

  function setNewLogGroup(logRow, groupName, groupId){
    _currentLogGroupName = groupName;
    _currentLogGroupId = groupId;

    logRow.addClass("active");
  }

  function addLogRowToGroup(logRow, groupId, logType) {
    var groupRow;

    logRow.addClass("treegrid-parent-" + groupId);

    if (logType.toLowerCase() === "error") {
      groupRow = $("." + "treegrid-" + groupId);

      changeLogClass(groupRow, "log-msg-error");
    }
  }

  function changeLogClass(logRow, newLogClass) {
    var logClassPattern = /log-msg-*/;

    logRow[0].className.split(/\s+/)
        .filter(function (value) {
          return logClassPattern.test(value);
        }).forEach(function (logClass) {
          logRow.removeClass(logClass);
        });

    logRow.addClass(newLogClass);
  }

  function createLogRow(message, messageId) {
    var logRow = $("<tr></tr>");
    var logCell = $("<td></td>");

    logRow.addClass("treegrid-" + messageId);

    logCell.text(">> " + message);

    logCell.addClass("log-msg");

    logRow.append(logCell);

    return logRow;
  }

  function scrollToTheBottom(container) {
    container.scrollTop(container[0].scrollHeight - container.height());
  }

  function setLogType(logRow, type) {
    var typeLower = type.toLowerCase();

    if (typeLower === "trace") {
      logRow.addClass("log-msg-trace");
    }
    else if (typeLower === "info") {
      logRow.addClass("log-msg-info");
    }
    else if (typeLower === "warn") {
      logRow.addClass("log-msg-warn");
    }
    else if (typeLower === "error") {
      logRow.addClass("log-msg-error");
    }
  }

  function startLogsLoader() {
    loadNewLogsToTree();

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
      $(_treeLogsTableSelector).find("*").remove();
    }
  };
};
using System;
using System.Collections.Generic;
using Aliyun.Api.LOG.Data;

namespace Aliyun.Api.LOG.Request
{
    public class PutLogsRequest : LogRequest
    {
        private string _logstore;
        private string _topic;
        private string _source;
        private List<LogItem> _logItems;
        private IDictionary<string, string> _logTags;

        public PutLogsRequest()
        {

        }

        public PutLogsRequest(string project, string logstore)
            : base(project)
        {
            _logstore = logstore;
        }

        public PutLogsRequest(string project, string logstore, string topic, string source, List<LogItem> items, IDictionary<string, string> logTags)
            : base(project)
        {
            _logstore = logstore;
            _topic = topic;
            _source = source;
            _logItems = items;
            _logTags = logTags;
        }

        public string Logstore
        {
            get { return _logstore; }
            set { _logstore = value; }
        }

        internal bool IsSetLogstore() => _logstore != null;

        public string Topic
        {
            get { return _topic; }
            set { _topic = value; }
        }

        internal bool IsSetTopic() => _topic != null;

        public string Source
        {
            get { return _source; }
            set { _source = value; }
        }

        internal bool IsSetSource() => _source != null;

        public List<LogItem> LogItems
        {
            get { return _logItems; }
            set { _logItems = value; }
        }

        internal bool IsSetLogItems() => _logItems != null;

        public IDictionary<string, string> LogTags
        {
            get { return _logTags; }
            set { _logTags = value; }
        }

        internal bool IsSetLogTags() => _logTags != null;
    }
}
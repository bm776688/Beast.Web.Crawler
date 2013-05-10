using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading;

namespace Microsoft.Advertising.Analytics.SharedService
{
    public class CrawlerState : IComparable<CrawlerState>
    {
        internal object userState;
        internal Exception exception;
		internal string encoding;
        internal string originalRequest;
        internal byte[] rowData;
        internal int retryCount;
		internal int priority;

        public object UserState
        {
            get { return this.userState; }
        }

		public string Encoding
		{
			get { return this.encoding; }
		}

        public Exception Exception
        {
            get { return exception; }
        }

		public int Priority
		{
			get { return priority; }
		}

        public string OriginalRequest
        {
            get { return originalRequest; }
        }

        public byte[] RowData
        {
            get { return this.rowData; }
        }

        internal Crawler crawler;

        internal AsyncCallback internalCallback;
        internal WaitCallback userCallback;

        internal HttpWebRequest request;
        internal HttpWebRequest CreateRequest()
        {
            return crawler.CreateRequest(this.originalRequest);
        }

        internal bool hasDataToSend = false;
        private byte[] dataToSend;
        internal byte[] DataToSend
        {
            get
            {
                if (this.hasDataToSend && this.dataToSend == null)
                {
                    this.dataToSend = crawler.CreateRequestData(this.originalRequest);
                }
                return this.dataToSend;
            }
        }


		public int CompareTo(CrawlerState other)
		{
			return this.priority - other.priority;
		}
	}
}

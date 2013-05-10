using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Microsoft.Advertising.Analytics.SharedService.MsnSearch
{
    public class SnippetGatherState
    {
        internal object userState;
        internal Exception error;
        internal SearchResponse result;

        public object UserState
        {
            get { return this.userState; }
        }

        public Exception Error
        {
            get { return error; }
        }

        public SearchResponse Result
        {
            get { return this.result; }
        }

        internal AsyncCallback internalCallback;
        internal SearchRequest request;
        internal WaitCallback userCallback;
    }
}

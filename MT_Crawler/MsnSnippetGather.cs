using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net;

using SR = Microsoft.Advertising.Analytics.SharedService.CrawlerMessage;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Microsoft.Advertising.Analytics.SharedService.MsnSearch
{
    public class MsnSnippetGather
    {
        public class QueryResponse
        {
            internal string query;
            internal SearchResponse response;
            internal System.DateTime creationTime;

            public QueryResponse(string query, SearchResponse response, System.DateTime dt)
            {
                this.query = query;
                this.response = response;
                this.creationTime = dt;
            }
            public QueryResponse(string query, SearchResponse response)
                : this(query, response, System.DateTime.Now) { }
            public QueryResponse(string query)
                : this(query, null, System.DateTime.Now) { }
        }

        static AsyncJobQueue<SnippetGatherState> jobQueue;
        static Semaphore maxConcurrentJobControl;

        static LruCache<string, QueryResponse> lruCache;
        static TimeSpan cacheExpireTimeSpan;

        static BingService s;
        static string appID;
        static string serviceUrl;
        static string configFile;

        static MsnSnippetGather()
        {
            Init();

            //ConfigHelper config = new ConfigHelper(typeof(Crawler));
            //configFile = config.ConfigFile;
            //FileSystemWatcher watcher = new FileSystemWatcher(Path.GetDirectoryName(configFile), "*.config");
            //watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            //watcher.IncludeSubdirectories = false;
            //watcher.EnableRaisingEvents = true;
            //watcher.Changed += new FileSystemEventHandler(watcher_Changed);
        }

        static void watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.FullPath == configFile)
            {
                Init();
            }
        }

        static void Init()
        {
            //Coming from KSP V6 config
            string msnWebServiceUrl = "http://api.bing.net:80/soap.asmx";
            string appId = "67C2363425B169EE6ECD80C8AFD7C1F2387A8A47";
            string msnProxyServer = "";
            bool msnProxyBypassOnLocal = true;
            int msnSyncTimeout = 180000;
            int msnMaxQueuedJob = 50000;
            int msnMaxConcurrentJob = 5;
            bool msnEnableTpsControl = false;
            int msnTps = 8;
            int msnAsyncTimeout = 180000;
            int cacheSize = 5000;
            string cacheExpireTimeSpanString = "1.00:00:00";
            int connectionLimit = 50;

            serviceUrl = msnWebServiceUrl;
            appID = appId;
            string proxyServer = msnProxyServer;
            if (!string.IsNullOrEmpty(proxyServer))
            {
                WebProxy msnProxy = new WebProxy(proxyServer);
                try
                {
                    msnProxy.BypassProxyOnLocal = msnProxyBypassOnLocal;
                }
                catch
                {
                    msnProxy.BypassProxyOnLocal = true;
                }

                s = new BingService(serviceUrl, msnProxy);
            }
            else
            {
                s = new BingService(serviceUrl);
            }

            s.Timeout = msnSyncTimeout;

            if (jobQueue == null)
            {
                jobQueue = new AsyncJobQueue<SnippetGatherState>(ProcessJob, AbortJob);
            }
            else
            {
                jobQueue.StopProcess();
            }

            jobQueue.MaxQueuedJob = msnMaxQueuedJob;
            int maxConcurrentJob = msnMaxConcurrentJob;
            jobQueue.MaxConcurrentJob = maxConcurrentJob;
            maxConcurrentJobControl = new Semaphore(maxConcurrentJob, maxConcurrentJob);

            jobQueue.EnableTpsControl = msnEnableTpsControl;

            if (jobQueue.EnableTpsControl)
            {
                jobQueue.Tps = msnTps;
            }
            jobQueue.AsyncTimeout = msnAsyncTimeout;
            jobQueue.StartProcess();

            try
            {
                lruCache = new LruCache<string, QueryResponse>(cacheSize);
            }
            catch
            {
                lruCache = new LruCache<string, QueryResponse>(5000);
            }

            try
            {
                cacheExpireTimeSpan = TimeSpan.Parse(cacheExpireTimeSpanString);
            }
            catch
            {
                cacheExpireTimeSpan = TimeSpan.FromDays(1);
            }

            if (connectionLimit > ServicePointManager.DefaultConnectionLimit)
            {
                ServicePointManager.DefaultConnectionLimit = connectionLimit;
            }
        }

        #region default search request property
        string cultureInfo = "en-US";
        //ResultFieldMask fieldMask = ResultFieldMask.Title | ResultFieldMask.Description | ResultFieldMask.Url | ResultFieldMask.DisplayUrl;
        int snippetCount = 50;
        public string CultureInfo
        {
            get { return cultureInfo; }
            set { cultureInfo = value; }
        }
        //public ResultFieldMask FieldMask
        //{
        //    get { return fieldMask; }
        //    set { fieldMask = value; }
        //}
        public int SnippetCount
        {
            get { return snippetCount; }
            set
            {
                // Max count is 50
                if (value > 50)
                {
                    snippetCount = 50;
                }
                else
                {
                    snippetCount = value;
                }
            }
        }
        #endregion
        #region cache
        /// <summary>
        /// Make it public by fengj: because provider level will call this directly
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static SearchResponse GetFromCache(string query)
        {
            QueryResponse cache = lruCache.Search(query);
            if (cache != null && cache.creationTime.Add(cacheExpireTimeSpan) > System.DateTime.Now)
            {
                return cache.response;
            }

            return null;
        }
        static void UpdateCache(string query, SearchResponse response)
        {
            lruCache.UpdateCache(query, new QueryResponse(query, response));
        }
        #endregion
        #region public methods
        
        public void AsyncSearch(string query, WaitCallback callback, object userState)
        {
            SearchRequest request = CreateSimpleRequest(query);
            AsyncSearch(request, callback, userState);
        }
        public void AsyncSearch(SearchRequest request, WaitCallback callback, object userState)
        {
            SearchResponse cacheRespoinse = GetFromCache(request.Query);
            if (cacheRespoinse != null)
            {
                SnippetGatherState sgs = new SnippetGatherState();
                sgs.result = cacheRespoinse;
                sgs.userState = userState;

                ThreadPool.QueueUserWorkItem(callback, sgs);

                return;
            }

            request.AppId = appID;

            SnippetGatherState state = new SnippetGatherState();
            state.request = request;
            state.userState = userState;
            state.userCallback = callback;
            state.internalCallback = SnippetGatherCallback;

            jobQueue.EnqueueJob(state);
        }
        public SearchResponse Search(string query)
        {
            SearchRequest searchRequest = CreateSimpleRequest(query);
            return Search(searchRequest);
        }

        internal class MsnSyncContext
        {
            internal ManualResetEvent signal;
            internal SearchResponse data;
            internal Exception error;
            public MsnSyncContext(ManualResetEvent signal)
            {
                this.signal = signal;
            }
        }

        public SearchResponse Search(SearchRequest request)
        {
            SearchResponse cacheRespoinse = GetFromCache(request.Query);
            if (cacheRespoinse != null)
            {
                return cacheRespoinse;
            }

            request.AppId = appID;
            using (ManualResetEvent signal = new ManualResetEvent(false))
            {
                MsnSyncContext context = new MsnSyncContext(signal);
                AsyncSearch(request, MsnSyncCallback, context);
                signal.WaitOne();
                if (context.error != null)
                {
                    throw context.error;
                }
                return context.data;
            }
        }
        internal static void MsnSyncCallback(object obj)
        {
            SnippetGatherState state = obj as SnippetGatherState;
            MsnSyncContext context = (MsnSyncContext)state.UserState;
            if (state.Error != null)
            {
                context.error = state.Error;
            }
            else
            {
                context.data = state.Result;
            }
            context.signal.Set();
        }

        #endregion
        static void SnippetGatherCallback(IAsyncResult asyncResult)
        {
            SnippetGatherState state = (SnippetGatherState)asyncResult.AsyncState;
            try
            {
                lock (state)
                {
                    if (state.request == null)
                    {
                        //state.error = new KspApiException(ErrorCode.Network.TimeoutError, SR.TimeoutAbort(TimeSpan.FromMilliseconds(jobQueue.AsyncTimeout).ToString()), TraceEventType.Warning);
                        throw new Exception("Snippet Gather State is Null.");
                    }
                    else
                    {
                        state.result = s.EndSearch(asyncResult);
                        UpdateCache(state.request.Query, state.result);
                    }
                }
            }
            catch (ArgumentNullException)
            {
                // s.EndSearch() will throw ArgumentNullException 
                // when the tps exceed max allowed number.
                //state.error = new KspApiException(ErrorCode.Network.NetworkError, SR.TpsExceed, TraceEventType.Warning);
                throw;
            }
            catch (Exception e)
            {
                //TraceEventType severity = (e is WebException) ? TraceEventType.Warning : TraceEventType.Error;
                //state.error = new KspApiException(ErrorCode.Network.NetworkError, e.Message, e, severity);
                throw e;
            }
            finally
            {
                state.request = null;
                state.userCallback(state);
                maxConcurrentJobControl.Release();
            }
        }
        SearchRequest CreateSimpleRequest(string query)
        {
            SearchRequest searchRequest = new SearchRequest();
            searchRequest.AppId = appID;
            searchRequest.Version = "2.0";
            searchRequest.Market = cultureInfo;
            searchRequest.Query = XmlHelper.XmlEncoding(query);
            searchRequest.Sources = new SourceType[] { SourceType.Web };
            searchRequest.Web = new WebRequest();
            searchRequest.Web.Count = (uint)snippetCount;
            searchRequest.Web.CountSpecified = true;
            searchRequest.Web.Offset = 0;
            searchRequest.Web.OffsetSpecified = true;
            searchRequest.Web.Options = new WebSearchOption[]
                {
                    WebSearchOption.DisableHostCollapsing,
                    WebSearchOption.DisableQueryAlterations
                };            

            return searchRequest;
        }

        #region job queue stuff
        static WaitHandle ProcessJob(SnippetGatherState state)
        {
            try
            {
                maxConcurrentJobControl.WaitOne();
                IAsyncResult result = s.BeginSearch(state.request, state.internalCallback, state);
                return result.AsyncWaitHandle;
            }
            catch (Exception e)
            {
                //TraceEventType severity = (e is WebException) ? TraceEventType.Warning : TraceEventType.Error;
                //state.error = new KspApiException(ErrorCode.Network.NetworkError, e.Message, e, severity);
                //state.userCallback(state);
                //maxConcurrentJobControl.Release();
                //return null;
                throw;
            }
        }
        static void AbortJob(SnippetGatherState state)
        {
            if (state.request != null)
            {
                lock (state)
                {
                    if (state.request != null)
                    {
                        s.CancelAsync(state.UserState);
                        state.request = null;
                    }
                }
            }
        }
        #endregion
    }

    public static class XmlHelper
    {
        static Regex amp = new Regex("&(?!(amp)|(gt)|(lt)|(quot)|(apos);)", RegexOptions.Compiled);
        static Regex lt = new Regex("<", RegexOptions.Compiled);
        public static string XmlEncoding(string text)
        {
            return lt.Replace(amp.Replace(text, "&amp;"), "&lt;");
        }
    }
}

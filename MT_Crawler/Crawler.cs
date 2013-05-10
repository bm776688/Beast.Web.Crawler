using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Reflection;
using System.Configuration;
using System.Xml;
using System.Web;

using SR = Microsoft.Advertising.Analytics.SharedService.CrawlerMessage;
using System.Diagnostics;

namespace Microsoft.Advertising.Analytics.SharedService
{
    public class Crawler
    {
        public class UrlContent
        {
            internal string url;
            internal byte[] content;
            internal DateTime creationTime;

            public UrlContent(string url, byte[] content, DateTime dt)
            {
                this.url = url;
                this.content = content;
                this.creationTime = dt;
            }
            public UrlContent(string url, byte[] content)
                : this(url, content, DateTime.Now) { }
            public UrlContent(string url)
                : this(url, null, DateTime.Now) { }
        }

        static int SyncTimeout = 60 * 1000;
		static int DefaultPriority = 100;
        static WebProxy crawlerProxy;
        static int AsyncTimeout;
        static bool allowAutoRedirect = true;
        static string userAgent;
        static string defaultUserAgent = "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.0)";

        static LruCache<string, UrlContent> lruCache;
        static TimeSpan cacheExpireTimeSpan;

        static AsyncJobQueue<CrawlerState> jobQueue;
        static Semaphore maxConcurrentJobControl;
        static string configFile;

		private static readonly Crawler instance = new Crawler();

		private Crawler()
        {
        }

		public static Crawler GetInstance()
        {
			return instance;
        }

        #region static constructor
        static Crawler()
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
            //Coming from KSP V6 configs
            int crawlerMaxQueuedJob = 50000;
            int crawlerMaxConcurrentJob = 5;
            int crawlerAsyncTimeout = 18000;
            bool crawlerEnableTpsControl = true;
            int crawlerTps = 8;
            int crawlerSyncTimeout = 18000;

            string proxyServer = "";
            bool proxyBypassOnLocal = true;
            int cacheSize = 5000;
            string cacheExpireTimeSpanString = "1.00:00:00";
            int connectionLimit = 50;

            allowAutoRedirect = false;
            userAgent = defaultUserAgent;


            if (jobQueue == null)
            {
                jobQueue = new AsyncJobQueue<CrawlerState>(ProcessRequest, AbortJob);
            }
            else
            {
                jobQueue.StopProcess();
            }

            jobQueue.MaxQueuedJob = crawlerMaxQueuedJob;
            int maxConcurrentJob = crawlerMaxConcurrentJob;
            jobQueue.MaxConcurrentJob = maxConcurrentJob;
            maxConcurrentJobControl = new Semaphore(maxConcurrentJob, maxConcurrentJob);
            AsyncTimeout = crawlerAsyncTimeout;
            jobQueue.AsyncTimeout = AsyncTimeout;

            jobQueue.EnableTpsControl = crawlerEnableTpsControl;
            if (jobQueue.EnableTpsControl)
            {
                jobQueue.Tps = crawlerTps;
            }

            jobQueue.StartProcess();

            SyncTimeout = crawlerSyncTimeout;

            if (!string.IsNullOrEmpty(proxyServer))
            {
                crawlerProxy = new WebProxy(proxyServer);
                try
                {
                    crawlerProxy.BypassProxyOnLocal = proxyBypassOnLocal;
                }
                catch
                {
                    crawlerProxy.BypassProxyOnLocal = true;
                }
            }

            try
            {
                lruCache = new LruCache<string, UrlContent>(cacheSize);
            }
            catch
            {
                lruCache = new LruCache<string, UrlContent>(5000);
            }
            try
            {
                cacheExpireTimeSpan = TimeSpan.Parse(cacheExpireTimeSpanString);
            }
            catch
            {
                cacheExpireTimeSpan = TimeSpan.FromDays(1);
            }

            // Set Connection limit
            if (connectionLimit > ServicePointManager.DefaultConnectionLimit)
            {
                ServicePointManager.DefaultConnectionLimit = (int)connectionLimit;
            }
        }
        #endregion

        #region job queue stuff
        static WaitHandle ProcessRequest(CrawlerState state)
        {
            maxConcurrentJobControl.WaitOne();
            try
            {
                state.request = state.CreateRequest();

                if (state.hasDataToSend)
                {
                    state.request.ContentLength = state.DataToSend.Length;
                    IAsyncResult sendResult = state.request.BeginGetRequestStream(state.internalCallback, state);
                    return sendResult.AsyncWaitHandle;
                }
                else
                {
                    IAsyncResult result = state.request.BeginGetResponse(state.internalCallback, state);
                    return result.AsyncWaitHandle;
                }
            }
            catch (Exception e)
            {
                state.exception = e;
                state.userCallback(state);
                maxConcurrentJobControl.Release();
                return null;
            }
        }

        static void AbortJob(CrawlerState state)
        {
            if (state.request != null)
            {
                lock (state)
                {
                    if (state.request != null)
                    {
                        state.request.Abort();
                        state.request = null;
                    }
                }
            }
        }
        #endregion

        #region callback methods
        internal static void CrawlerCallback(IAsyncResult asyncResult)
        {
            CrawlerState state = (CrawlerState)asyncResult.AsyncState;

            HttpWebResponse response = null;
            bool retry = false;
            try
            {
                lock (state)
                {
                    if (state.request == null)
                    {
                        throw new Exception("CrawlerState is null. Network Timeout.");
                    }
                    else
                    {
                        response = (HttpWebResponse)state.request.EndGetResponse(asyncResult);
						if (response.StatusCode == HttpStatusCode.Redirect) ;
						else if (response.StatusCode == HttpStatusCode.MovedPermanently) ;
						else if (response.StatusCode != HttpStatusCode.OK) throw new Exception(response.StatusCode.ToString());
                        try
                        {
							state.encoding = response.ContentEncoding;
                            state.rowData = ExtractRawData(response);
                            state.request = null;
                            UpdateCache(state.OriginalRequest, state.RowData);
                        }
                        catch (IOException ioe)
                        {
                            if (state.retryCount < 3 && ioe.InnerException != null && ioe.InnerException.GetType() == typeof(System.Net.Sockets.SocketException))
                            {
                                // fix bug99499@iDSS: retry for this particular exception
                                jobQueue.EnqueueJob(state);
                                retry = true;
                                state.retryCount++;
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                state.exception = e;
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                    response = null;
                }
                if (!retry)
                {
                    state.userCallback(state);
                }
                maxConcurrentJobControl.Release();
            }
        }

        internal static void SendCallback(IAsyncResult asyncResult)
        {
            CrawlerState state = (CrawlerState)asyncResult.AsyncState;
            try
            {
                lock (state)
                {
                    if (state.request == null)
                    {
                        throw new Exception("Crawler State is null. Network Timeout.");
                    }
                    else
                    {
                        Stream sendStream = state.request.EndGetRequestStream(asyncResult);
                        sendStream.Write(state.DataToSend, 0, state.DataToSend.Length);

                        IAsyncResult responseResult = state.request.BeginGetResponse(CrawlerCallback, state);
                        ThreadPool.RegisterWaitForSingleObject(responseResult.AsyncWaitHandle, TimeoutCallback, state, AsyncTimeout, true);
                    }
                }
            }
            catch (Exception e)
            {
                state.exception = e;
            }
            finally
            {
                if (state.exception != null)
                {
                    // call user's callback if anything is wrong.
                    state.userCallback(state);
                    maxConcurrentJobControl.Release();
                }
            }
        }

        static void TimeoutCallback(object state, bool timedOut)
        {
            if (timedOut)
            {
                AbortJob(state as CrawlerState);
            }
        }
        #endregion

        #region crawl url Sync methods
        internal class SyncContext
        {
            internal ManualResetEvent signal;
            internal byte[] data;
            internal Exception error;

            public SyncContext(ManualResetEvent signal)
            {
                this.signal = signal;
            }
        }
        public virtual byte[] Crawl(string uriString)
        {
            byte[] content = GetFromCache(uriString);
            if (content != null)
            {
                return content;
            }
            using (ManualResetEvent signal = new ManualResetEvent(false))
            {
                SyncContext context = new SyncContext(signal);
                AsyncCrawl(uriString, SyncCallback, context);
                signal.WaitOne();
                if (context.error != null)
                {
                    throw context.error;
                }

                return context.data;
            }
        }
        internal static void SyncCallback(object obj)
        {
            CrawlerState state = obj as CrawlerState;
            SyncContext context = (SyncContext)state.UserState;
            if (state.Exception != null)
            {
                context.error = state.Exception;
            }
            else
            {
                context.data = state.RowData;
            }
            context.signal.Set();
        }
        #endregion

        #region crawl url Async methods

		public void AsyncCrawl(string uriString, WaitCallback callback, object userState)
		{
			AsyncCrawl(uriString, callback, userState, DefaultPriority);
		}

        public void AsyncCrawl(string uriString, WaitCallback callback, object userState, int priority)
        {
            byte[] content = GetFromCache(uriString);
            if (content != null)
            {
                CrawlerState cs = new CrawlerState();
                cs.originalRequest = uriString;
                cs.rowData = content;
                cs.userState = userState;
				cs.priority = priority;

                ThreadPool.QueueUserWorkItem(callback, cs);
                return;
            }

            CrawlerState state = CreateState(uriString, callback, userState);
            jobQueue.EnqueueJob(state);
        }

        #endregion

        #region cache
        //fengj change it to public as in provider level it will also be called
        public static byte[] GetFromCache(string url)
        {
			return null;
            UrlContent cache = lruCache.Search(Normalize(url));
            if (cache != null && cache.creationTime.Add(cacheExpireTimeSpan) > DateTime.Now)
            {
                return cache.content;
            }

            return null;
        }
        static void UpdateCache(string url, byte[] content)
        {
            lruCache.UpdateCache(Normalize(url), new UrlContent(url, content));
        }
        static string Normalize(string url)
        {
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                url = "http://" + url;
            }
            return url;
        }
        #endregion

        #region private methods
        public virtual HttpWebRequest CreateRequest(string originalRequest)
        {
            Uri uri = CreateUri(originalRequest);
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);
            if (crawlerProxy != null)
            {
                request.Proxy = crawlerProxy;
            }
            request.UserAgent = userAgent;
            request.AllowAutoRedirect = allowAutoRedirect;
            request.CookieContainer = new CookieContainer();
            request.Timeout = SyncTimeout;
            request.Accept = "text/*";
            return request;
        }

        public virtual byte[] CreateRequestData(string originalRequest)
        {
            return null;
        }
		
        protected virtual CrawlerState CreateState(string request, WaitCallback callback, object userState)
        {
            CrawlerState state = new CrawlerState();
            state.internalCallback = CrawlerCallback;
            state.userState = userState;
            state.originalRequest = request;
            state.userCallback = callback;
            state.crawler = this;

            return state;
        }

        protected Uri CreateUri(string uriString)
        {
            if (!uriString.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                uriString = "http://" + uriString;
            }

            return new Uri(uriString);
        }

        static byte[] ExtractRawData(HttpWebResponse response)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    int readCount = 0;
                    byte[] buffer = new byte[8192];
                    while ((readCount = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        memoryStream.Write(buffer, 0, readCount);
                    }
                }

                memoryStream.Flush();
                return memoryStream.ToArray();
            }
        }

        #region stuff used to decode the byte[] content to string

        public static string ExtractString(byte[] content)
        {
            Encoding encoding = DetectEncoding(content);
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            return encoding.GetString(content);
        }

        // HTTP header. refer to RFC2068 for details
        // Content-Type: text/html; charset=ISO-8859-4
        private static Regex charsetRegex = new Regex(@"charset\s*=\s*(?<charset>[^|;]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // META info. refer to http://www.w3.org/TR/html4/struct/global.html#edef-META for details
        // <META http-equiv="Content-Type" content="text/html; charset=EUC-JP">
        private static Regex metaRegex = new Regex("<META http-equiv\\s*=\\s*\"?Content-Type\"? content\\s*=\\s*\"(?<content>[^\"]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        const int MetaLength = 1024;

        private static Encoding DetectEncoding(string contentType)
        {
            Encoding encoding = null;
            String charset = null;
            Match match = charsetRegex.Match(contentType);
            if (match.Success)
            {
                charset = match.Groups["charset"].Value;
                try
                {
                    encoding = Encoding.GetEncoding(charset);
                }
                catch (Exception)
                {
                }
            }

            return encoding;
        }

        private static Encoding DetectEncoding(byte[] content)
        {
            Encoding encoding = null;
            String meta = Encoding.ASCII.GetString(content, 0, content.Length > MetaLength ? MetaLength : content.Length);
            Match metaMatch = metaRegex.Match(meta);
            if (metaMatch.Success)
            {
                string metaContent = metaMatch.Groups["content"].Value;
                encoding = DetectEncoding(metaContent);
            }

            return encoding;
        }

        #endregion

        #endregion
    }

	/*
    public class MsnInfoSpaceCrawler : Crawler
    {
        static string msnSearchEn;

        static MsnInfoSpaceCrawler()
        {
            ConfigHelper config = new ConfigHelper(typeof(Crawler));
            msnSearchEn = config.GetConfigValue<string>("MsnSearch_En");
        }

        public override HttpWebRequest CreateRequest(string query)
        {
            query = HttpUtility.UrlEncode(query).Replace("+", "%20");
            string uriString = msnSearchEn.Replace("%s", query);

            return base.CreateRequest(uriString);
        }
    }
	 */
}

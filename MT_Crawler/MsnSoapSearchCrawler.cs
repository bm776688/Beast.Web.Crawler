using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading;

using Microsoft.Advertising.Analytics.SharedService.MsnSearch;
using System.Text.RegularExpressions;

namespace Microsoft.Advertising.Analytics.SharedService
{
    public class MsnSoapSearchCrawler : Crawler
    {
        static string AppID;
        static string MsnSearchUrl;

        static MsnSoapSearchCrawler()
        {
            ConfigHelper config = new ConfigHelper(typeof(Crawler));
            AppID = config.GetConfigValue<string>("AppID");
            MsnSearchUrl = config.GetConfigValue<string>("MsnWebServiceUrl");
        }

        #region default search request property

        private string cultureInfo = "en-US";
        public string CultureInfo
        {
            get { return cultureInfo; }
            set { cultureInfo = value; }
        }

        private SafeSearchOptions safeSearch = SafeSearchOptions.Moderate;
        public SafeSearchOptions SafeSearch
        {
            get { return safeSearch; }
            set { safeSearch = value; }
        }

        private SearchFlags flags = SearchFlags.None;
        public SearchFlags Flags
        {
            get { return flags; }
            set { flags = value; }
        }

        private ResultFieldMask fieldMask = ResultFieldMask.Title | ResultFieldMask.Description | ResultFieldMask.Url | ResultFieldMask.DisplayUrl;
        public ResultFieldMask FieldMask
        {
            get { return fieldMask; }
            set { fieldMask = value; }
        }

        private int offset = 0;
        public int Offset
        {
            get { return offset; }
            set { offset = value; }
        }        

        private int count = 50;
        public int Count
        {
            get { return count; }
            set
            {
                // Max count is 50
                if (value > 50)
                {
                    count = 50;
                }
                else
                {
                    count = value;
                }
            }
        }

        private SourceType source = SourceType.Web;
        public SourceType Source
        {
            get { return source; }
            set { source = value; }
        }

        #endregion

        public override byte[] CreateRequestData(string originalRequest)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Append("<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">");
            sb.Append("<soap:Body>");
            sb.Append("<Search xmlns=\"http://schemas.microsoft.com/MSNSearch/2005/09/fex\">");
            sb.Append("<Request>");
            sb.Append("<AppID>").Append(AppID).Append("</AppID>");
            sb.Append("<Query>").Append(XmlHelper.XmlEncoding(originalRequest)).Append("</Query>");
            sb.Append("<CultureInfo>").Append(CultureInfo).Append("</CultureInfo>");
            sb.Append("<SafeSearch>").Append(SafeSearch).Append("</SafeSearch>");
            sb.Append("<Flags>").Append(Flags).Append("</Flags>");
            sb.Append("<Requests>");

            sb.Append("<SourceRequest>");
            sb.Append("<Source>").Append(Source).Append("</Source>");
            sb.Append("<Offset>").Append(Offset).Append("</Offset>");
            sb.Append("<Count>").Append(Count).Append("</Count>");
            sb.Append("<ResultFields>");
            sb.Append(FieldMask.ToString().Replace(",", ""));
            sb.Append("</ResultFields>");
            sb.Append("</SourceRequest>");
            sb.Append("</Requests></Request></Search></soap:Body></soap:Envelope>");

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public override HttpWebRequest CreateRequest(string query)
        {
            HttpWebRequest request = base.CreateRequest(MsnSearchUrl);
            request.Method = "POST";
            request.ContentType = "text/xml; charset=utf-8";
            request.Headers.Add("SOAPAction: \"http://schemas.microsoft.com/MSNSearch/2005/09/fex/Search\"");
            request.ServicePoint.Expect100Continue = false;

            return request;
        }

        protected override CrawlerState CreateState(string request, WaitCallback callback, object userState)
        {
            CrawlerState state = base.CreateState(MsnSearchUrl, callback, userState);

            state.originalRequest = request;
            state.internalCallback = Crawler.SendCallback;
            state.hasDataToSend = true;

            return state;
        }
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

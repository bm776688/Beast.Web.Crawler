using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Microsoft.Advertising.Analytics.SharedService;
using System.IO;

namespace WebApplicatio
{
	public partial class _Default : System.Web.UI.Page
	{
		protected void Page_Load(object sender, EventArgs e)
		{
			Crawler crawler = new Crawler();
			CrawlerState st = new CrawlerState();
	
			for(int i = 0; i < 1000; i++) crawler.AsyncCrawl("http://www.hao123.com/", ResultCallback, null);
		}

		void ResultCallback(object obj)
		{
			CrawlerState state = obj as CrawlerState;

			if (state.Exception != null) Console.WriteLine(state.Exception);
			else
			{
				string s = Crawler.ExtractString(state.RowData);

			}
		}
	}
}
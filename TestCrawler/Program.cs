using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Advertising.Analytics.SharedService;

namespace TestCrawler
{
	class Program
	{
		static string path = Environment.CurrentDirectory;
		static int cnt = 0;
		static Queue<string> urlQueue;
		static HashSet<string> urls;
		static int errorCnt = 0;

		static void Main(string[] args)
		{
			// NormalTest();
			ForeverTest();
			Console.ReadLine();
		}

		static void ForeverTest() 
		{
			Crawler crawler = Crawler.GetInstance();

			urls = new HashSet<string>();
			urls.Add("http://www.hao123.com");

			urlQueue = new Queue<string>();
			urlQueue.Enqueue("http://www.hao123.com");

			while (true)
			{
				while (urlQueue.Count > 0)
				{
					string url = null;
					lock (urlQueue)
					{
						url = urlQueue.Dequeue();
					}
					crawler.AsyncCrawl(url, CompleteCallback, null);
				}
			}
		}

		static void CompleteCallback(object obj)
		{
			cnt++;
			CrawlerState state = obj as CrawlerState;
			if (state.Exception != null)
			{
				Console.WriteLine(state.Exception);
				errorCnt++;
			}
			else
			{
				Console.WriteLine(state.OriginalRequest + "," + state.Priority + ",");
				string s = Crawler.ExtractString(state.RowData);
				string fileName = string.Format("{0}/output/{1}.txt", path, cnt);
				using (StreamWriter sw = new StreamWriter(fileName))
				{
					sw.Write(s);
					sw.Close();
				}
				if (string.IsNullOrEmpty(s))
				{
					Console.WriteLine("crawError exception");
					errorCnt++;
				}
				Extractor ex = new Extractor();
				PageInfo[] res = ex.Crawl(s, TimeSpan.FromSeconds(10));
				foreach (PageInfo pi in res)
				{
					lock (urls)
					{
						if (urls.Contains(pi.Url)) continue;
						else urls.Add(pi.Url);
					}
					lock (urlQueue) urlQueue.Enqueue(pi.Url);
				}
			}
		}

	}
}

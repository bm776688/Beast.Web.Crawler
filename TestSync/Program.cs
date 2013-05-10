using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Advertising.Analytics.SharedService;
using System.Security.Policy;
using System.Threading;

namespace TestSync
{
	class Program
	{
		static void Main(string[] args)
		{
			TimeoutCrawler tc = new TimeoutCrawler();
			List<Tuple<Uri, int>> request = new List<Tuple<Uri, int>>();
			for (int i = 0; i < 100; i++) request.Add(new Tuple<Uri, int>(new Uri("http://www.hao123.com"), 1));

			IList<Tuple<Uri, byte[]>> result = tc.Crawl(request, TimeSpan.FromMilliseconds(300));

			foreach (Tuple<Uri, byte[]> response in result) 
			{
				Console.WriteLine(response.Item1);
			}

			Console.WriteLine(result.Count);
			Console.ReadLine();
		}
	}

	public interface ITimeoutCrawler
	{
		IList<Tuple<Uri, byte[]>> Crawl(IList<Tuple<Uri, int /*priority*/>> request, TimeSpan timeout);
	}

	public class TimeoutCrawler :ITimeoutCrawler
	{
		private Crawler crawler = Crawler.GetInstance();
		private List<Tuple<Uri, byte[]>> result;
		private AutoResetEvent handle;
		private int cnt;
		private object lockObj = new object();
		private bool isAborted;

		public IList<Tuple<Uri, byte[]>> Crawl(IList<Tuple<Uri, int>> request, TimeSpan timeout)
		{
			isAborted = false;
			result = new List<Tuple<Uri, byte[]>>();
			cnt = request.Count;
			handle = new AutoResetEvent(false);
			foreach (Tuple<Uri, int> tuple in request) 
			{
				crawler.AsyncCrawl(tuple.Item1.ToString(), ResultCallBack, handle, tuple.Item2);
			}
			handle.WaitOne(timeout);

			lock (result)
			{
				isAborted = true;
				return result;
			}
		}

		private void ResultCallBack(object obj) 
		{
			CrawlerState state = obj as CrawlerState;
			if (state.Exception != null) { /* exception */ }
			else 
			{
				lock(result)
				{
					if (isAborted) return;
					result.Add(new Tuple<Uri, byte[]>(new Uri(state.OriginalRequest), state.RowData));
				}
			}
			lock (lockObj)
			{
				cnt--;
				if (cnt == 0) handle.Set();
			}
		}
	}

}

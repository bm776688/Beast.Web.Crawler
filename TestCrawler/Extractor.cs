using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace TestCrawler
{
	public class Extractor
	{
		private HashSet<string> crawledUrl;

		private bool isBaseExsist;

		private string urlBase;

		private string parentUrl;

		public const string urlRegPatten = @"(((f|ht){1}t(p|ps)://)[-a-zA-Z0-9@:%_\+.~#?&//=]+)";

		private Regex reg;

		private List<PageInfo> result;

		private int order;
		private int deep;

		public Extractor()
		{
			crawledUrl = new HashSet<string>();
			reg = new Regex(urlRegPatten);
		}


		public PageInfo[] Crawl(string html, TimeSpan timeout)
		{
			List<PageInfo> pages = GetLinkedUrlCollection(html);
			pages.Sort();
			return pages.ToArray();
		}

		private List<PageInfo> GetLinkedUrlCollection(string html)
		{
			HtmlWeb web = new HtmlWeb();
			HtmlDocument document = new HtmlDocument();
			document.LoadHtml(html);

			Regex reg = new Regex(urlRegPatten);
			urlBase = "";
			isBaseExsist = false;

			// get url base
			HtmlNodeCollection bases = document.DocumentNode.SelectNodes("//base");
			if (bases != null)
				foreach (HtmlNode baseNode in bases)
					if (baseNode.Attributes["href"] != null)
					{
						urlBase = baseNode.Attributes["href"].Value;
						isBaseExsist = true;
					}

			result = new List<PageInfo>();
			order = 0;
			deep = 0;
			int tickCount = Environment.TickCount;
			Traversal(document.DocumentNode);
			return result;
		}

		private void Traversal(HtmlNode node)
		{
			order++;
			deep++;
			foreach (HtmlNode child in node.ChildNodes)
			{
				ProcessNode(child);
				Traversal(child);
			}
			deep--;
		}

		private void ProcessNode(HtmlNode node)
		{
			switch (node.Name)
			{
				case "meta":
					if (node.Attributes["content"] != null)
					{
						string s = node.Attributes["content"].Value;
						if (s.EndsWith("/")) s = s.Substring(0, s.Length - 1);
						if (!crawledUrl.Contains(s) && reg.IsMatch(s)) ;
					}
					break;
				case "a":
					if (node.Attributes["href"] != null)
					{
						string s = node.Attributes["href"].Value;
						if (s.EndsWith("/")) s = s.Substring(0, s.Length - 1);
						if (!crawledUrl.Contains(s) && reg.IsMatch(s))
						{
							s = reg.Match(s).Value;
							PageInfo pi = new PageInfo();
							pi.Url = s;
							pi.DomTreeOrder = order;
							pi.DomTreeDeep = deep;
							pi.Similarity = LCS(parentUrl, s);
							result.Add(pi);
						}
					}
					break;
				case "frame":
					if (node.Attributes["src"] != null)
					{
						/*
						string s = node.Attributes["src"].Value;
						if (s.EndsWith("/")) s = s.Substring(0, s.Length - 1);
						if (!crawledUrl.Contains(s) && reg.IsMatch(s)) result.Add(s);
						if (!isBaseExsist) break;
						s = urlBase + s;
						if (!crawledUrl.Contains(s) && reg.IsMatch(s)) result.Add(s);
							*/
					}
					break;
				default:
					break;
			}
		}

		public int LCS(string a, string b)
		{
			if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;
			int lenA = a.Length;
			int lenB = b.Length;
			int[,] rec = new int[lenA + 1, lenB + 1];
			rec[0, 0] = a[0] == b[0] ? 1 : 0;
			for (int i = 0; i < lenA; i++)
				for (int j = 0; j < lenB; j++)
				{
					rec[i, j + 1] = Math.Max(rec[i, j + 1], rec[i, j]);
					rec[i + 1, j] = Math.Max(rec[i + 1, j], rec[i, j]);
					if (i < lenA - 1 && j < lenB - 1 && a[i] == b[j]) rec[i + 1, j + 1] = rec[i, j] + 1;
				}
			return rec[lenA - 1, lenB - 1];
		}
	}
}

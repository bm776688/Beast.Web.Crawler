using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestCrawler
{
	public class PageInfo : IComparable<PageInfo>
	{
		public string Url { get; set; }

		public int DomTreeOrder { get; set; }

		public int Similarity { get; set; }

		public int DomTreeDeep { get; set; }

		public double Priority
		{
			get
			{
				return DomTreeOrder + Similarity;
			}
		}

		public override string ToString()
		{
			return "<" + Similarity + "," + DomTreeOrder + "," + DomTreeDeep + "," + Url + ">";
		}

		public int CompareTo(PageInfo other)
		{
			return this.Similarity == other.Similarity ? this.DomTreeOrder - other.DomTreeOrder : other.Similarity - this.Similarity;
		}
	}
}

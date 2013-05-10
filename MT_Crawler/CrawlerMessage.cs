// WARNING:
// This file was generated by the Microsoft DataWarehouse String Resource Tool 1.33.0.0
// from information in CrawlerMessage.strings.
// DO NOT MODIFY THIS FILE'S CONTENTS, THEY WILL BE OVERWRITTEN
// 
namespace Microsoft.Advertising.Analytics.SharedService
{
	using System;
	using System.Resources;
	using System.Globalization;
	
	
	[System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
	internal class CrawlerMessage
	{
		
		protected CrawlerMessage()
		{
		}
		
		public static CultureInfo Culture
		{
			get
			{
				return Keys.Culture;
			}
			set
			{
				Keys.Culture = value;
			}
		}
		
		public static string RequestExceedMaxAllowed
		{
			get
			{
				return Keys.GetString(Keys.RequestExceedMaxAllowed);
			}
		}
		
		public static string TpsExceed
		{
			get
			{
				return Keys.GetString(Keys.TpsExceed);
			}
		}
		
		public static string MissConfigFile(string sectionName)
		{
			return Keys.GetString(Keys.MissConfigFile, sectionName);
		}
		
		public static string MissConfigKey(string keyName)
		{
			return Keys.GetString(Keys.MissConfigKey, keyName);
		}
		
		public static string ConvertFailed(string keyName, string value, string toType)
		{
			return Keys.GetString(Keys.ConvertFailed, keyName, value, toType);
		}
		
		public static string TimeoutAbort(string time)
		{
			return Keys.GetString(Keys.TimeoutAbort, time);
		}
		
		[System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
		public class Keys
		{
			
			static ResourceManager resourceManager = new ResourceManager(typeof(CrawlerMessage).FullName, typeof(CrawlerMessage).Module.Assembly);
			
			static CultureInfo _culture = null;
			
			public const string MissConfigFile = "MissConfigFile";
			
			public const string MissConfigKey = "MissConfigKey";
			
			public const string ConvertFailed = "ConvertFailed";
			
			public const string RequestExceedMaxAllowed = "RequestExceedMaxAllowed";
			
			public const string TpsExceed = "TpsExceed";
			
			public const string TimeoutAbort = "TimeoutAbort";
			
			private Keys()
			{
			}
			
			public static CultureInfo Culture
			{
				get
				{
					return _culture;
				}
				set
				{
					_culture = value;
				}
			}
			
			public static string GetString(string key)
			{
				return resourceManager.GetString(key, _culture);
			}
			
			public static string GetString(string key, object arg0)
			{
				return string.Format(global::System.Globalization.CultureInfo.CurrentCulture, resourceManager.GetString(key, _culture), arg0);
			}
			
			public static string GetString(string key, object arg0, object arg1, object arg2)
			{
				return string.Format(global::System.Globalization.CultureInfo.CurrentCulture, resourceManager.GetString(key, _culture), arg0, arg1, arg2);
			}
		}
	}
}

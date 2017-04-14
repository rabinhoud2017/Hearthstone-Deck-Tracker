using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Newtonsoft.Json;

namespace Hearthstone_Deck_Tracker.HsReplay
{
	public static class HsReplayDecks
	{
		private const string Url = "https://hsreplay.net/analytics/query/list_deck_inventory";
		private const string CacheFile = "hsreplay_decks.cache";

		private static string CacheFilePath => Path.Combine(Config.Instance.DataDir, CacheFile);
		private static DecksData _data;
		private static bool _loading;

		public static string[] AvailableDecks
		{
			get
			{
				if(!(_data?.IsStale ?? true) || _loading)
					return _data?.Data ?? new string[0];
				Load();
				return new string[0];
			}
		}

		public static event Action OnLoaded;

		static HsReplayDecks()
		{
			Load();
		}

		private static async void Load()
		{
			_loading = true;
			var data = await LoadFromDisk();
			Log.Info($"Loaded from disk: {data}");
			if(data?.IsStale ?? true)
			{
				Log.Info("Cached data was not found or stale. Fetching latest...");
				data = await Fetch();
				if(data == null)
				{
					Log.Warn("No data. Can retry in 30 minutes.");
					data = new DecksData()
					{
						ClientTimeStamp = DateTime.Now.Subtract(TimeSpan.FromHours(23.5)),
						Data = new string[0]
					};
				}
				await WriteToDisk(data);
			}
			_data = data;
			Log.Info($"Complete: {data}");
			OnLoaded?.Invoke();
			_loading = false;
		}

		private static async Task<DecksData> LoadFromDisk()
		{
			Log.Info("Loading data from disk...");
			var cacheFile = new FileInfo(CacheFilePath);
			if(!cacheFile.Exists)
				return null;
			try
			{
				using(var sr = new StreamReader(CacheFilePath))
				{
					var data = await sr.ReadToEndAsync();
					return JsonConvert.DeserializeObject<DecksData>(data);
				}
			}
			catch(Exception e)
			{
				Log.Error(e);
				return null;
			}
		}

		private static async Task<bool> WriteToDisk(DecksData data)
		{
			Log.Info("Writing data to disk...");
			try
			{
				using(var sw = new StreamWriter(CacheFilePath))
					await sw.WriteAsync(JsonConvert.SerializeObject(data));
			}
			catch(Exception e)
			{
				Log.Error(e);
				return false;
			}
			return true;
		}

		private static async Task<DecksData> Fetch()
		{
			Log.Info("Fetching latest data...");
			try
			{
				using(var wc = new WebClient())
				{
					var data = await wc.DownloadStringTaskAsync(Url);
					var decksData = JsonConvert.DeserializeObject<DecksData>(data);
					decksData.ClientTimeStamp = DateTime.Now;
					return decksData;
				}
			}
			catch(Exception e)
			{
				Log.Error(e);
				return null;
			}
		}
	}

	public class DecksData
	{
		[JsonProperty("series")]
		public string[] Data { get; set; }

		[JsonProperty("as_of")]
		public DateTime ServerTimeStamp { get; set; }

		public DateTime ClientTimeStamp { get; set; }

		[JsonIgnore]
		public TimeSpan Age => DateTime.Now - ClientTimeStamp;

		[JsonIgnore]
		public bool IsStale => Age.TotalHours > 24;

		public override string ToString()
		{
			return $"Count={Data.Length}, ServerTS={ServerTimeStamp}, Downloaded={ClientTimeStamp} Age={Age}";
		}
	}
}

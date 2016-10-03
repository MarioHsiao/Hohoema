﻿using NicoPlayerHohoema.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace NicoPlayerHohoema.Models
{
	// FavFeedGroupを管理する

	// 常にFavManagerへのアクションを購読して
	// 自身が管理するfavFeedGroupが参照しているFavが購読解除された場合に、
	// グループ内からも削除するように働く

	// フィードの更新を指揮する

	// フィードの保存処理をコントロールする

	public class FeedManager
	{
		static Newtonsoft.Json.JsonSerializerSettings FeedGroupSerializerSettings = new Newtonsoft.Json.JsonSerializerSettings()
		{
			TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Objects
		};


		public HohoemaApp HohoemaApp { get; private set; }

		public uint UserId { get; private set; }

		public Dictionary<Guid, FileAccessor<List<FavFeedItem>>> FeedStreamFileAccessors { get; private set; }

		public Dictionary<IFeedGroup, FileAccessor<FeedGroup2>> FeedGroupDict { get; private set; }
		public IReadOnlyCollection<IFeedGroup> FeedGroups
		{
			get
			{
				return FeedGroupDict.Keys;
			}
		}
		


		public FeedManager(HohoemaApp hohoemaApp, uint userId)
		{
			HohoemaApp = hohoemaApp;
			UserId = userId;
			FeedGroupDict = new Dictionary<IFeedGroup, FileAccessor<FeedGroup2>>();
			FeedStreamFileAccessors = new Dictionary<Guid, FileAccessor<List<FavFeedItem>>>();
		}
		

		public async Task<StorageFolder> GetFeedStreamDataFolder()
		{
			var folder = await HohoemaApp.GetApplicationLocalDataFolder();

			return await folder.CreateFolderAsync("feed", CreationCollisionOption.OpenIfExists);
		}

		public Task<StorageFolder> GetFeedGroupFolder()
		{
			return HohoemaApp.GetFeedRoamingSettingsFolder();
		}
		
		internal async Task Initialize()
		{
			var feedGroupFolder = await GetFeedGroupFolder();
//			var feedStreamDataFolder = await GetFeedStreamDataFolder();

			var files = await feedGroupFolder.GetFilesAsync();

			if (files.Count == 0)
			{
				// ローミング設定ないにデータが無い場合はアプリローカルからロードできないか試行する
				var legacyFeedSettingsFolder = await HohoemaApp.GetLegacyFeedSettingsFolder();
				files = await legacyFeedSettingsFolder.GetFilesAsync();

				await Load(files);

				// ローカル設定から読み込まれたデータがあればローミング設定に保存
				if (FeedGroups.Count > 0)
				{
					await Save();
				}
			}
			else
			{
				// ローミングフォルダからフィードを読み込む
				await Load(files);
			}

			

			Debug.WriteLine($"FeedManager: {FeedGroupDict.Count} 件のFeedGroupを読み込みました。");


			var updater = new SimpleBackgroundUpdate("feedManager_" + UserId, () => Refresh());
			await HohoemaApp.BackgroundUpdater.Schedule(updater);
		}

		public async Task Load(IReadOnlyList<StorageFile> files)
		{
			var feedGroupFolder = await GetFeedGroupFolder();
			var feedStreamDataFolder = await GetFeedStreamDataFolder();

			var legacyFeedSettingsFolder = await HohoemaApp.GetLegacyFeedSettingsFolder();

			foreach (var file in files)
			{
				if (file.FileType == ".json")
				{
					var fileName = file.Name;
					var fileAccessor = new FileAccessor<FeedGroup2>(feedGroupFolder, fileName);

					try
					{
						var item = await fileAccessor.Load(FeedGroupSerializerSettings);

						if (item == null)
						{
							var legacyFeedGroupFileAccessor = new FileAccessor<FeedGroup>(legacyFeedSettingsFolder, fileName);
							var item_legacy = await legacyFeedGroupFileAccessor.Load(FeedGroupSerializerSettings);

							if (item_legacy != null)
							{
								item = new FeedGroup2(item_legacy);
							}
						}

						if (item != null)
						{
							item.HohoemaApp = this.HohoemaApp;
							item.FeedManager = this;
							FeedGroupDict.Add(item, fileAccessor);
							var itemId = item.Id.ToString();

							var streamFileAccessor = new FileAccessor<List<FavFeedItem>>(feedStreamDataFolder, $"{itemId}.json");
							FeedStreamFileAccessors.Add(item.Id, streamFileAccessor);

							await item.LoadFeedStream(streamFileAccessor);

							Debug.WriteLine($"FeedManager: [Sucesss] load {item.Label}");
						}
						else
						{
							Debug.WriteLine($"FeedManager: [?] .json but not FeedGroup file < {fileName}");
						}
					}
					catch
					{
						Debug.WriteLine($"FeedManager: [Failed] load {file.Path}");
					}
				}
			}
		}


		public IFeedGroup GetFeedGroup(Guid id)
		{
			return FeedGroups.SingleOrDefault(x => x.Id == id);
		}


		private async Task Refresh()
		{
			foreach (var items in FeedGroups)
			{
				await Task.Delay(500);

				await items.Refresh();
			}
		}

		

		private async Task _Save(KeyValuePair<IFeedGroup, FileAccessor<FeedGroup2>> feedItem)
		{
			var fileAccessor = feedItem.Value;
			await fileAccessor.Save(feedItem.Key as FeedGroup2, FeedGroupSerializerSettings);

			var feedStreamFileAccessor = FeedStreamFileAccessors[feedItem.Key.Id];
			await feedStreamFileAccessor.Save(feedItem.Key.FeedItems);
		}

		public async Task Save()
		{
			foreach (var feedGroupTuple in FeedGroupDict)
			{
				await _Save(feedGroupTuple);
			}
		}

		public Task SaveOne(IFeedGroup group)
		{
			var target = FeedGroupDict.SingleOrDefault(x => x.Key.Id == group.Id);
			return _Save(target);
		}

		public async Task<IFeedGroup> AddFeedGroup(string label)
		{
			var folder = await GetFeedGroupFolder();
			var feedStreamDataFolder = await GetFeedStreamDataFolder();

			var feedGroup = new FeedGroup2(label);
			var fileAccessor = new FileAccessor<FeedGroup2>(folder, label + ".json");

			feedGroup.HohoemaApp = this.HohoemaApp;
			feedGroup.FeedManager = this;
			FeedGroupDict.Add(feedGroup, fileAccessor);

			var itemId = feedGroup.Id.ToString();
			var streamFileAccessor = new FileAccessor<List<FavFeedItem>>(feedStreamDataFolder, $"{itemId}.json");
			FeedStreamFileAccessors.Add(feedGroup.Id, streamFileAccessor);

			await fileAccessor.Save(feedGroup);
			return feedGroup;
		}

		

		public bool CanAddLabel(string label)
		{
			if (String.IsNullOrWhiteSpace(label)) { return false; }

			return FeedGroups.All(x => x.Label != label);
		}


		public async Task<bool> RemoveFeedGroup(IFeedGroup group)
		{
			var removeTarget = FeedGroups.SingleOrDefault(x => x.Id == group.Id);

			if (removeTarget != null)
			{
				var fileAccessor = FeedGroupDict[removeTarget];
				await fileAccessor.Delete(StorageDeleteOption.PermanentDelete);

				var feedStreamFileAccesssor = FeedStreamFileAccessors[group.Id];
				await feedStreamFileAccesssor.Delete(StorageDeleteOption.PermanentDelete);

				return FeedGroupDict.Remove(removeTarget);
			}
			else
			{
				return false;
			}
		}


		internal Task<bool> RenameFeedGroup(IFeedGroup group, string newLabel)
		{
			var target = FeedGroups.SingleOrDefault(x => x.Id == group.Id);

			if (target != null)
			{
				var fileAccessor = FeedGroupDict[target];

				return fileAccessor.Rename(newLabel + ".json");
			}
			else
			{
				return Task.FromResult(false);
			}
		}
		

		public async Task MarkAsRead(string videoId)
		{
			foreach (var group in FeedGroupDict)
			{
				var feedGroup = group.Key;
				if (feedGroup.MarkAsRead(videoId))
				{
					await _Save(group);
				}
			}			
		}
		

		public async Task MarkAsReadAllVideo()
		{
			foreach (var group in FeedGroupDict)
			{
				var feedGroup = group.Key;
				feedGroup.ForceMarkAsRead();
				await _Save(group);				
			}
		}
		
	}
}

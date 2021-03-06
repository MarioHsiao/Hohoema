﻿using NicoPlayerHohoema.Models;
using Prism.Commands;
using Prism.Mvvm;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NicoPlayerHohoema.ViewModels.VideoInfoContent
{
	public class MylistVideoInfoContentViewModel : MediaInfoViewModel
	{
		public MylistVideoInfoContentViewModel(string videoId, string threadId, UserMylistManager mylistManager) 
			: base()
		{
			VideoId = videoId;
			ThreadId = threadId;
			MylistManager = mylistManager;

			MylistComment = new ReactiveProperty<string>("")
				.AddTo(_CompositeDisposable);
			MylistGroups = MylistManager.UserMylists
				.Select(x => new VideoTargetedMylistGroupInfoViewModel(videoId, x, this))
				.ToList();


			DeflistRegistrationCapacity = MylistManager.DeflistRegistrationCapacity;
			MylistRegistrationCapacity = MylistManager.MylistRegistrationCapacity;
			DeflistRegistrationCount = MylistManager.DeflistRegistrationCount;
			MylistRegistrationCount = MylistManager.MylistRegistrationCount;
		}

		public string VideoId { get; private set; }
		public string ThreadId { get; private set; }

		public ReactiveProperty<string> MylistComment { get; private set; }

		public int DeflistRegistrationCapacity { get; private set; }
		public int DeflistRegistrationCount { get; private set; }
		public int MylistRegistrationCapacity { get; private set; }
		public int MylistRegistrationCount { get; private set; }

		public List<VideoTargetedMylistGroupInfoViewModel> MylistGroups { get; private set; }
		public UserMylistManager MylistManager { get; private set; }



		public async Task<bool> RegistrationMylist(MylistGroupInfo groupInfo)
		{
			var result = await groupInfo.Registration(VideoId, MylistComment.Value);

			if (result == Mntone.Nico2.ContentManageResult.Success)
			{
				DeflistRegistrationCount = MylistManager.DeflistRegistrationCount;
				MylistRegistrationCount = MylistManager.MylistRegistrationCount;
				OnPropertyChanged(nameof(DeflistRegistrationCount));
				OnPropertyChanged(nameof(MylistRegistrationCount));
			}

			return result == Mntone.Nico2.ContentManageResult.Success;
		}

		public async Task<bool> UnregistrationMylist(MylistGroupInfo groupInfo)
		{
			var result = await groupInfo.Unregistration(VideoId);

			if (result == Mntone.Nico2.ContentManageResult.Success)
			{
				DeflistRegistrationCount = MylistManager.DeflistRegistrationCount;
				MylistRegistrationCount = MylistManager.MylistRegistrationCount;
				OnPropertyChanged(nameof(DeflistRegistrationCount));
				OnPropertyChanged(nameof(MylistRegistrationCount));
			}

			return result == Mntone.Nico2.ContentManageResult.Success;
		}
	}


	// 対象Videoに対するマイリストグループの表示と機能を提供するViewModel
	// VideoIdからマイリストへの登録がされているかをチェック
	public class VideoTargetedMylistGroupInfoViewModel : BindableBase, IDisposable
	{
		public VideoTargetedMylistGroupInfoViewModel(string videoId, MylistGroupInfo groupInfo, MylistVideoInfoContentViewModel mylistContentVM)
		{
			VideoId = videoId;
			GroupInfo = groupInfo;
			MylistVideoInfoContentViewModel = mylistContentVM;
			IsRegistrated = new ReactiveProperty<bool>(groupInfo.CheckRegistratedVideoId(videoId), ReactivePropertyMode.DistinctUntilChanged);
			NowProccessing = new ReactiveProperty<bool>(false);

			_RegistrationLock = new SemaphoreSlim(1, 1);

			IsRegistrated.Subscribe(async requestRegistration => 
			{
				if (NowProccessing.Value) { return; }

				try
				{
					await _RegistrationLock.WaitAsync();

					NowProccessing.Value = true;

					// 登録したい
					if (requestRegistration)
					{
						if (await MylistVideoInfoContentViewModel.RegistrationMylist(GroupInfo))
						{
							Debug.WriteLine($"成功：マイリスト「{GroupInfo.Name}」に {VideoId} を登録しました。");
						}
						else
						{
							Debug.WriteLine($"失敗：マイリスト「{GroupInfo.Name}」に {VideoId} を登録できませんでした。");
							IsRegistrated.Value = false;
						}
					}
					// 登録解除したい
					else
					{
						if (await MylistVideoInfoContentViewModel.UnregistrationMylist(GroupInfo))
						{
							Debug.WriteLine($"成功：マイリスト「{GroupInfo.Name}」から {VideoId} を登録解除しました。");
						}
						else 
						{
							Debug.WriteLine($"失敗：マイリスト「{GroupInfo.Name}」から {VideoId} を登録解除できませんでした。");
							IsRegistrated.Value = true;
						}
					}

					OnPropertyChanged(nameof(GroupInfo));
				}
				finally
				{
					NowProccessing.Value = false;

					_RegistrationLock.Release();
				}

			});
		}



		public void Dispose()
		{
			IsRegistrated.Dispose();
		}


		public ReactiveProperty<bool> IsRegistrated { get; private set; }
		public ReactiveProperty<bool> NowProccessing { get; private set; }


		private SemaphoreSlim _RegistrationLock;
		

		public string VideoId { get; private set; }
		public MylistGroupInfo GroupInfo { get; private set; }
		public MylistVideoInfoContentViewModel MylistVideoInfoContentViewModel { get; private set; }

	}
}

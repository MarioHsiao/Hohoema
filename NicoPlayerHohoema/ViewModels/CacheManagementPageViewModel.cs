﻿using Mntone.Nico2.Videos.WatchAPI;
using NicoPlayerHohoema.Models;
using Prism.Mvvm;
using Prism.Windows.Mvvm;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Prism.Windows.Navigation;
using System.Threading;
using System.Reactive.Disposables;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Reactive.Concurrency;
using NicoPlayerHohoema.Util;
using Windows.UI.Xaml.Navigation;
using Prism.Commands;

namespace NicoPlayerHohoema.ViewModels
{
	public class CacheManagementPageViewModel : HohoemaVideoListingPageViewModelBase<CacheVideoViewModel>
	{
		public static SynchronizationContextScheduler scheduler;
		public CacheManagementPageViewModel(HohoemaApp app, PageManager pageManager, Views.Service.MylistRegistrationDialogService mylistDialogService)
			: base(app, pageManager, mylistDialogService)
		{
			if (scheduler == null)
			{
				scheduler = new SynchronizationContextScheduler(SynchronizationContext.Current);
			}
			_MediaManager = app.MediaManager;

			OpenCacheSettingsCommand = new DelegateCommand(() => 
			{
				PageManager.OpenPage(HohoemaPageType.Settings, HohoemaSettingsKind.Cache.ToString());
			});
		}

		
		#region Implement HohoemaVideListViewModelBase

		protected override IIncrementalSource<CacheVideoViewModel> GenerateIncrementalSource()
		{
			return new CacheVideoInfoLoadingSource(HohoemaApp, PageManager);
		}


		public override void OnNavigatingFrom(NavigatingFromEventArgs e, Dictionary<string, object> viewModelState, bool suspending)
		{
			base.OnNavigatingFrom(e, viewModelState, suspending);

			// 削除動作
			_MediaManager.DeleteUnrequestedVideos().ConfigureAwait(false);
		}

		protected override bool CheckNeedUpdateOnNavigateTo(NavigationMode mode)
		{
			return true;
		}

		protected override void PostResetList()
		{
			
		}

		protected override uint IncrementalLoadCount
		{
			get
			{
				return 20u;
			}
		}
		

		#endregion



		public DelegateCommand OpenCacheSettingsCommand { get; private set; }

		NiconicoMediaManager _MediaManager;
	}

	// 自身のキャッシュ状況を表現する
	// キャッシュ処理の状態、進捗状況
	
	

	public class CacheVideoViewModel : VideoInfoControlViewModel
	{

		public CacheVideoViewModel(NicoVideo nicoVideo, NicoVideoQuality quality, PageManager pageManager)
			: base(nicoVideo, pageManager)
		{
			Quality = quality;
			CacheRequestTime = nicoVideo.CacheRequestTime;

			if (quality == NicoVideoQuality.Low)
			{
				IsIncompleteCache = nicoVideo.LowQualityCacheState != NicoVideoCacheState.Cached;
				ProgressPercent = nicoVideo.ObserveProperty(x => x.LowQualityCacheProgressSize)
					.Select(x => ProgressToPercent(x, (uint)nicoVideo.ThumbnailResponseCache.CachedItem.SizeLow))
					.ToReactiveProperty(CacheManagementPageViewModel.scheduler)
					.AddTo(_CompositeDisposable);
				IsVisibleProgress = nicoVideo.ObserveProperty(x => x.LowQualityCacheState)
					.Select(x => x == NicoVideoCacheState.NowDownloading)
					.ToReactiveProperty(CacheManagementPageViewModel.scheduler)
					.AddTo(_CompositeDisposable);
				CacheState = nicoVideo.ObserveProperty(x => x.LowQualityCacheState)
					.ToReactiveProperty(CacheManagementPageViewModel.scheduler)
					.AddTo(_CompositeDisposable);

			}
			else
			{
				IsIncompleteCache = nicoVideo.OriginalQualityCacheState != NicoVideoCacheState.Cached;
				ProgressPercent = nicoVideo.ObserveProperty(x => x.OriginalQualityCacheProgressSize)
					.Select(x => ProgressToPercent(x, (uint)nicoVideo.ThumbnailResponseCache.CachedItem.SizeHigh))
					.ToReactiveProperty(CacheManagementPageViewModel.scheduler)
					.AddTo(_CompositeDisposable);
				IsVisibleProgress = nicoVideo.ObserveProperty(x => x.OriginalQualityCacheState)
					.Select(x => x == NicoVideoCacheState.NowDownloading)
					.ToReactiveProperty(CacheManagementPageViewModel.scheduler)
					.AddTo(_CompositeDisposable);
				CacheState = nicoVideo.ObserveProperty(x => x.OriginalQualityCacheState)
					.ToReactiveProperty(CacheManagementPageViewModel.scheduler)
					.AddTo(_CompositeDisposable);
			}


			PrivateReasonText = nicoVideo.WatchApiResponseCache?.CachedItem?.PrivateReason.ToString() ?? "";
			IsRequireConfirmDelete = new ReactiveProperty<bool>(nicoVideo.IsRequireConfirmDelete);
			IsForceDisplayNGVideo = true;
		}

		private float ProgressToPercent(uint size, uint totalSize)
		{
			if (size == totalSize) { return 100.0f; }
			return (float)Math.Floor((size / (float)totalSize) * 1000.0f) * 0.1f;
		}



		protected override VideoPlayPayload MakeVideoPlayPayload()
		{
			var payload = base.MakeVideoPlayPayload();

			payload.Quality = Quality;

			return payload;
		}


		private DelegateCommand _ConfirmDeleteCommand;
		public DelegateCommand ConfirmDeleteCommand
		{
			get
			{
				return _ConfirmDeleteCommand
					?? (_ConfirmDeleteCommand = new DelegateCommand(() =>
					{
						try
						{
							// TODO: MediaManagerに削除動画の確認が済んだことを伝える
//							NicoVideo.DeletedVideoConfirmedFromUser(NicoVideo).ConfigureAwait(false);
							IsRequireConfirmDelete.Value = false;
						}
						catch { }
					}));
			}
		}


		public string PrivateReasonText { get; private set; }


		public bool IsIncompleteCache { get; private set; }

		public DateTime CacheRequestTime { get; private set; }

		public NicoVideoQuality Quality { get; private set; }
		public ReactiveProperty<NicoVideoCacheState?> CacheState { get; private set; }

		public ReactiveProperty<float> ProgressPercent { get; private set; }

		public ReactiveProperty<bool> IsVisibleProgress { get; private set; }

		public ReactiveProperty<bool> IsRequireConfirmDelete { get; private set; }



	}


	public class CacheVideoInfoLoadingSource : IIncrementalSource<CacheVideoViewModel>
	{
		public CacheVideoInfoLoadingSource(HohoemaApp app, PageManager pageManager)
		{
			_HohoemaApp = app;
			_PageManager = pageManager;
		}


		public async Task<IEnumerable<CacheVideoViewModel>> GetPagedItems(uint pageIndex, uint pageSize)
		{
			// 
			var contentFinder = _HohoemaApp.ContentFinder;
			var mediaManager = _HohoemaApp.MediaManager;


			if (RawList == null || pageIndex == 1)
			{
				List<CacheVideoViewModel> list = new List<CacheVideoViewModel>();

				
				foreach (var req in mediaManager.CacheRequestedItemsStack.ToArray())
				{
					var item = await mediaManager.GetNicoVideo(req.RawVideoid);

					await item.CheckCacheStatus();
					await item.WatchApiResponseCache.UpdateFromLocal();

					if (req.Quality == NicoVideoQuality.Original)
					{
						var vm = await ToCacheVideoViewModel(item.RawVideoId, NicoVideoQuality.Original);
						list.Add(vm);
					}
					else if (req.Quality == NicoVideoQuality.Low)
					{
						var vm = await ToCacheVideoViewModel(item.RawVideoId, NicoVideoQuality.Low);
						list.Add(vm);
					}
				}

				RawList = list.OrderBy(x => x.CacheRequestTime).Reverse().ToList();
			}

			int head = (int)((pageIndex - 1) * pageSize);
			return RawList.Skip(head).Take((int)pageSize);
		}

		private async Task<CacheVideoViewModel> ToCacheVideoViewModel(string videoId, NicoVideoQuality quality)
		{
			return await ToCacheVideoViewModel(new NicoVideoCacheRequest()
			{
				RawVideoid = videoId,
				Quality = quality
			});
		}


		private async Task<CacheVideoViewModel> ToCacheVideoViewModel(NicoVideoCacheRequest req)
		{
			var mediaManager = _HohoemaApp.MediaManager;

			var nicoVideo = await mediaManager.GetNicoVideo(req.RawVideoid);

			var vm = new CacheVideoViewModel(nicoVideo, req.Quality, _PageManager);

			await vm.LoadThumbnail();
			return vm;

		}




		public List<CacheVideoViewModel> RawList { get; private set; }

		HohoemaApp _HohoemaApp;
		PageManager _PageManager;

	}

}
﻿using NicoPlayerHohoema.Models;
using NicoPlayerHohoema.Util;
using Prism.Commands;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Prism.Windows.Navigation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System.Threading;
using Windows.UI.Xaml;
using NicoPlayerHohoema.Views.Service;
using Microsoft.Practices.Unity;
using System.Runtime.InteropServices.WindowsRuntime;

namespace NicoPlayerHohoema.ViewModels
{
	public abstract class HohoemaVideoListingPageViewModelBase<VIDEO_INFO_VM> : HohoemaListingPageViewModelBase<VIDEO_INFO_VM>
		where VIDEO_INFO_VM : VideoInfoControlViewModel
	{
		public HohoemaVideoListingPageViewModelBase(HohoemaApp app, PageManager pageManager, MylistRegistrationDialogService mylistDialogService, bool isRequireSignIn = true, bool useDefaultPageTitle = true)
			: base(app, pageManager, useDefaultPageTitle:useDefaultPageTitle)
		{
			MylistDialogService = mylistDialogService;


			var SelectionItemsChanged = SelectedItems.ToCollectionChanged().ToUnit();

#if DEBUG
			SelectedItems.CollectionChangedAsObservable()
				.Subscribe(x =>
				{
					Debug.WriteLine("Selected Count: " + SelectedItems.Count);
				});
#endif


			PlayAllCommand = SelectionItemsChanged
				.Select(_ => SelectedItems.Count > 0)
				.ToReactiveCommand(false)
				.AddTo(_CompositeDisposable);

			PlayAllCommand
				.SubscribeOnUIDispatcher()
				.Subscribe(_ =>
				{

				// TODO: プレイリストに登録
				// プレイリストを空にしてから選択動画を登録

				//				SelectedVideoInfoItems.First()?.PlayCommand.Execute();
			    })
			    .AddTo(_CompositeDisposable);

			CancelCacheDownloadRequest = SelectionItemsChanged
				.Select(_ => SelectedItems.Count > 0)
				.ToReactiveCommand(false)
				.AddTo(_CompositeDisposable);

			CancelCacheDownloadRequest
				.SubscribeOnUIDispatcher()
				.Subscribe(async _ =>
				{
					var items = EnumerateCacheRequestedVideoItems().ToList();
					var action = AsyncInfo.Run<uint>(async (cancelToken, progress) => 
					{
						uint count = 0;
						foreach (var item in items)
						{
        					await item.NicoVideo.CancelCacheRequest();

							++count;
							progress.Report(count);
						}

						ClearSelection();
					});

					await PageManager.StartNoUIWork("キャッシュリクエストをキャンセル中", items.Count, () => action);
				}
			)
			.AddTo(_CompositeDisposable);


            // クオリティ指定無しのキャッシュDLリクエスト
            RequestCacheDownload = SelectionItemsChanged
                .Select(_ => SelectedItems.Count > 0 && CanDownload)
				.ToReactiveCommand(false)
				.AddTo(_CompositeDisposable);

			RequestCacheDownload
				.SubscribeOnUIDispatcher()
				.Subscribe(async _ =>
				{
                    foreach (var item in EnumerateCanDownloadVideoItem())
					{
                        await item.NicoVideo.RequestCache(HohoemaApp.UserSettings.PlayerSettings.DefaultQuality);
					}

					ClearSelection();
					await UpdateList();
				})
			.AddTo(_CompositeDisposable);



			RegistratioMylistCommand = SelectionItemsChanged
				.Select(x => SelectedItems.Count > 0)
				.ToReactiveCommand(false)
				.AddTo(_CompositeDisposable);
			RegistratioMylistCommand
				.SubscribeOnUIDispatcher()
				.Subscribe(async _ =>
				{
                    var mylists = HohoemaApp.UserMylistManager.UserMylists;
                    var localMylists = HohoemaApp.Playlist.Playlists;
                    var selectDialogService = App.Current.Container.Resolve<ContentSelectDialogService>();
                    var result = await selectDialogService.ShowDialog(
                        "追加先マイリストを選択",
                        new List<ISelectableContainer>()
                        {
                            new ChoiceFromListSelectableContainer("マイリスト",
                                mylists.Select(x => new SelectDialogPayload() { Label = x.Name, Id = x.Id, Context = x })
                            ),
                            new ChoiceFromListSelectableContainer("ローカルマイリスト",
                                localMylists.Select(x => new SelectDialogPayload() { Label = x.Name, Id = x.Id, Context = x })
                            )
                        }
                        );

                    if (result == null) { return; }

                    var targetMylist = result.Context as IPlayableList;

                    var items = SelectedItems.ToList();
					var action = AsyncInfo.Run<uint>(async (cancelToken, progress) =>
					{
						uint progressCount = 0;


						Debug.WriteLine($"一括マイリストに追加を開始...");
						int successCount = 0;
						int existCount = 0;
						int failedCount = 0;
						foreach (var video in SelectedItems)
						{
                            if (targetMylist.Origin == PlaylistOrigin.LoginUser)
                            {
                                var mylistGroup = targetMylist as MylistGroupInfo;
                                var registrationResult = await mylistGroup.Registration(
                                video.RawVideoId
                                , ""
                                , withRefresh: false /* あとで一括でリフレッシュ */
                                );

                                switch (registrationResult)
                                {
                                    case Mntone.Nico2.ContentManageResult.Success: successCount++; break;
                                    case Mntone.Nico2.ContentManageResult.Exist: existCount++; break;
                                    case Mntone.Nico2.ContentManageResult.Failed: failedCount++; break;
                                    default:
                                        break;
                                }

                                Debug.WriteLine($"{video.Title}[{video.RawVideoId}]:{registrationResult.ToString()}");
                            }
                            else if (targetMylist.Origin == PlaylistOrigin.Local)
                            {
                                var localMylist = targetMylist as LocalMylist;
                                if (localMylist.PlaylistItems.FirstOrDefault(x => x.ContentId == video.RawVideoId) != null)
                                {
                                    existCount++;
                                }
                                else
                                {
                                    var resultItem = localMylist.AddVideo(video.RawVideoId, video.Title);
                                    if (resultItem != null)
                                    {
                                        successCount++;
                                    }
                                    else
                                    {
                                        failedCount++;
                                    }
                                }
                            }

                            progressCount++;
                            progress.Report(progressCount);
                        }


                        if (targetMylist.Origin == PlaylistOrigin.LoginUser)
                        {
                            var mylistGroup = targetMylist as MylistGroupInfo;
                            await mylistGroup.Refresh();

                            // マイリストに追加に失敗したものを残すように
                            // 登録済みのアイテムを選択アイテムリストから削除
                            foreach (var item in SelectedItems.ToArray())
                            {
                                if (mylistGroup.CheckRegistratedVideoId(item.RawVideoId))
                                {
                                    SelectedItems.Remove(item);
                                }
                            }
                        }

                        // リフレッシュ
                        


						// ユーザーに結果を通知

						var titleText = $"「{targetMylist.Name}」に {successCount}件 の動画を登録しました";
						var toastService = App.Current.Container.Resolve<ToastNotificationService>();
						var resultText = $"";
						if (existCount > 0)
						{
							resultText += $"重複：{existCount} 件";
						}
						if (failedCount > 0)
						{
							resultText += $"\n登録に失敗した {failedCount}件 は選択されたままです";
						}

						toastService.ShowText(titleText, resultText);

						//					ResetList();

						Debug.WriteLine($"一括マイリストに追加を完了---------------");
						ClearSelection();
					});

					await PageManager.StartNoUIWork("マイリストに追加", items.Count, () => action);
				}
			);
            

            Playlists = HohoemaApp.Playlist.Playlists.ToReadOnlyReactiveCollection();
        }


		protected override void OnDispose()
		{
			base.OnDispose();
		}


		protected override Task OnSignIn(ICollection<IDisposable> userSessionDisposer, CancellationToken cancelToken)
        {
            ReflectCanDownloadStatus();

            return base.OnSignIn(userSessionDisposer, cancelToken);
        }

		public override void OnNavigatedTo(NavigatedToEventArgs e, Dictionary<string, object> viewModelState)
		{
			base.OnNavigatedTo(e, viewModelState);

			if (IncrementalLoadingItems == null
				|| CheckNeedUpdateOnNavigateTo(e.NavigationMode))
			{
				//				ResetList();
			}

            ReflectCanDownloadStatus();
        }

		protected override void OnHohoemaNavigatingFrom(NavigatingFromEventArgs e, Dictionary<string, object> viewModelState, bool suspending)
		{
			base.OnHohoemaNavigatingFrom(e, viewModelState, suspending);

			// 戻る時だけ
			if (e.NavigationMode == NavigationMode.Back 
				&& IncrementalLoadingItems != null
				&& IncrementalLoadingItems.Source is HohoemaVideoPreloadingIncrementalSourceBase<VIDEO_INFO_VM>)
			{
				var preloadSource = IncrementalLoadingItems.Source as HohoemaVideoPreloadingIncrementalSourceBase<VIDEO_INFO_VM>;
				preloadSource.CancelPreloading();
			}
		}

		private IEnumerable<VideoInfoControlViewModel> EnumerateCacheRequestedVideoItems()
		{
			return SelectedItems.Where(x =>
			{
				return x.NicoVideo.OriginalQuality.IsCacheRequested
					|| x.NicoVideo.LowQuality.IsCacheRequested;

			});
		}

		private IEnumerable<VideoInfoControlViewModel> EnumerateCanDownloadVideoItem(NicoVideoQuality? quality = null)
		{
			if (!quality.HasValue)
			{
				return SelectedItems.Where(x =>
				{
					var video = x.NicoVideo;
					if (video.OriginalQuality.CanRequestCache && !video.OriginalQuality.IsCached)
					{
						return true;
					}
					else if (video.LowQuality.CanRequestCache && !video.LowQuality.IsCached)
					{
						return true;
					}
					else
					{
						return false;
					}
				});
			}
            else
            {
                switch (quality)
                {
                    case NicoVideoQuality.Original:
                        return SelectedItems.Where(x => x.NicoVideo.OriginalQuality.CanRequestCache && !x.NicoVideo.OriginalQuality.IsCached);
                    case NicoVideoQuality.Low:
                        return SelectedItems.Where(x => x.NicoVideo.LowQuality.CanRequestCache && !x.NicoVideo.LowQuality.IsCached);
                    default:
                        return Enumerable.Empty<VideoInfoControlViewModel>();
                }
            }
        }

		

        private void ReflectCanDownloadStatus()
        {
            CanDownload = HohoemaApp.UserSettings.CacheSettings.IsUserAcceptedCache 
                && HohoemaApp.UserSettings.CacheSettings.IsEnableCache
                && HohoemaApp.IsLoggedIn
                ;
        }







        private bool _CanDownload;
		public bool CanDownload
		{
			get { return _CanDownload; }
			set { SetProperty(ref _CanDownload, value); }
		}

        public ReadOnlyReactiveCollection<LocalMylist> Playlists { get; private set; }

		public MylistRegistrationDialogService MylistDialogService { get; private set; }

		public ReactiveCommand PlayAllCommand { get; private set; }
        public ReactiveCommand CancelCacheDownloadRequest { get; private set; }
		public ReactiveCommand DeleteOriginalQualityCache { get; private set; }
		public ReactiveCommand DeleteLowQualityCache { get; private set; }

		public ReactiveCommand RegistratioMylistCommand { get; private set; }


		// クオリティ指定なしのコマンド
		// VMがクオリティを実装している場合には、そのクオリティを仕様
		// そうでない場合は、リクエスト時は低クオリティのみを
		// 削除時はすべてのクオリティの動画を指定してアクションを実行します。
		// 基本的にキャッシュ管理画面でしか使わないはずです
		public ReactiveCommand RequestCacheDownload { get; private set; }
		public ReactiveCommand DeleteCache { get; private set; }
	}


    
}

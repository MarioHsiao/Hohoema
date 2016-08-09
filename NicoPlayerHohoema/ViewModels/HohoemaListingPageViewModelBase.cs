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

namespace NicoPlayerHohoema.ViewModels
{
	abstract public class HohoemaListingPageViewModelBase<ITEM_VM> : HohoemaViewModelBase
		where ITEM_VM : HohoemaListingPageItemBase
	{
		public HohoemaListingPageViewModelBase(HohoemaApp app, PageManager pageManager, bool isRequireSignIn = false)
			: base(app, pageManager, isRequireSignIn)
		{
			NowLoadingItems = new ReactiveProperty<bool>(true)
				.AddTo(_CompositeDisposable);
			SelectedItems = new ObservableCollection<ITEM_VM>();

			HasItem = new ReactiveProperty<bool>(true);

			ListViewVerticalOffset = new ReactiveProperty<double>(0.0)
				.AddTo(_CompositeDisposable);
			_LastListViewOffset = 0;


			MaxItemsCount = new ReactiveProperty<int>(0)
				.AddTo(_CompositeDisposable);
			LoadedItemsCount = new ReactiveProperty<int>(0)
				.AddTo(_CompositeDisposable);
			SelectedItemsCount = SelectedItems.ObserveProperty(x => x.Count)
				.ToReactiveProperty(0)
				.AddTo(_CompositeDisposable);



			NowRefreshable = new ReactiveProperty<bool>(false);

			// 複数選択モード
			IsSelectionModeEnable = new ReactiveProperty<bool>(false)
				.AddTo(_CompositeDisposable);

			IsSelectionModeEnable.Where(x => !x)
				.Subscribe(x => ClearSelection())
				.AddTo(_CompositeDisposable);

			// 複数選択モードによって再生コマンドの呼び出しを制御する
			SelectItemCommand = IsSelectionModeEnable
				.Select(x => !x)
				.ToReactiveCommand<ITEM_VM>()
				.AddTo(_CompositeDisposable);


			SelectItemCommand.Subscribe(x =>
			{
				if (x?.SelectedCommand.CanExecute(null) ?? false)
				{
					x.SelectedCommand.Execute(null);
				}

				ClearSelection();
			})
			.AddTo(_CompositeDisposable);


			var SelectionItemsChanged = SelectedItems.ToCollectionChanged().ToUnit();

#if DEBUG
			SelectedItems.CollectionChangedAsObservable()
				.Subscribe(x => 
				{
					Debug.WriteLine("Selected Count: " + SelectedItems.Count);
				});
#endif


			
		}


		protected override void OnDispose()
		{
			IncrementalLoadingItems?.Dispose();
		}


		protected override void OnSignIn(ICollection<IDisposable> disposer)
		{
			base.OnSignIn(disposer);			
		}

		protected override void OnSignOut()
		{
			base.OnSignOut();

			if (IsRequireSignIn)
			{
				IncrementalLoadingItems = null;
			}
		}

		public override void OnNavigatedTo(NavigatedToEventArgs e, Dictionary<string, object> viewModelState)
		{
			base.OnNavigatedTo(e, viewModelState);
		}


		protected override async Task NavigatedToAsync(CancellationToken cancelToken, NavigatedToEventArgs e, Dictionary<string, object> viewModelState)
		{
			if (!NowSignIn && PageIsRequireSignIn)
			{
				IncrementalLoadingItems = null;
				return;
			}

			await ListPageNavigatedToAsync(cancelToken, e, viewModelState);

			if (_IncrementalLoadingItems != null)
			{
				IncrementalLoadingItems = _IncrementalLoadingItems;
				_IncrementalLoadingItems = null;
			}

			if (IncrementalLoadingItems == null
				|| CheckNeedUpdateOnNavigateTo(e.NavigationMode))
			{
				await ResetList();
			}
			else
			{
				OnPropertyChanged(nameof(IncrementalLoadingItems));

				await Task.Delay(100);

				ListViewVerticalOffset.Value = _LastListViewOffset + 0.1;
				ChangeCanIncmentalLoading(true);				
			}
		}

		protected virtual Task ListPageNavigatedToAsync(CancellationToken cancelToken, NavigatedToEventArgs e, Dictionary<string, object> viewModelState)
		{
			return Task.CompletedTask;
		}

		public override void OnNavigatingFrom(NavigatingFromEventArgs e, Dictionary<string, object> viewModelState, bool suspending)
		{
			base.OnNavigatingFrom(e, viewModelState, suspending);

			_LastListViewOffset = ListViewVerticalOffset.Value;
			ChangeCanIncmentalLoading(false);

			_IncrementalLoadingItems = IncrementalLoadingItems;
			IncrementalLoadingItems = null;
		}


		private void ChangeCanIncmentalLoading(bool enableLoading)
		{
			if (IncrementalLoadingItems != null)
			{
				IncrementalLoadingItems.IsPuaseLoading = !enableLoading;
			}
		}

		protected async Task UpdateList()
		{
			// TODO: 表示中のアイテムすべての状態を更新
			// 主にキャッシュ状態の更新が目的

			var source = IncrementalLoadingItems?.Source;

			if (source != null)
			{
				MaxItemsCount.Value = await source.ResetSource();
			}
		}

		protected async Task ResetList()
		{
			HasItem.Value = true;
			LoadedItemsCount.Value = 0;

			IsSelectionModeEnable.Value = false;

			if (IncrementalLoadingItems != null)
			{
				IncrementalLoadingItems.BeginLoading -= BeginLoadingItems;
				IncrementalLoadingItems.DoneLoading -= CompleteLoadingItems;
				IncrementalLoadingItems.Dispose();
				IncrementalLoadingItems = null;
			}

			try
			{

				var source = GenerateIncrementalSource();

				MaxItemsCount.Value = await source.ResetSource();

				IncrementalLoadingItems = new IncrementalLoadingCollection<IIncrementalSource<ITEM_VM>, ITEM_VM>(source, IncrementalLoadCount);
				OnPropertyChanged(nameof(IncrementalLoadingItems));

				IncrementalLoadingItems.BeginLoading += BeginLoadingItems;
				IncrementalLoadingItems.DoneLoading += CompleteLoadingItems;

				PostResetList();
			}
			catch
			{
				IncrementalLoadingItems = null;
				HasItem.Value = false;
				Debug.WriteLine("failed GenerateIncrementalSource.");
			}
		}


		private void BeginLoadingItems()
		{
			NowLoadingItems.Value = true;
		}

		private void CompleteLoadingItems()
		{
			NowLoadingItems.Value = false;

			LoadedItemsCount.Value = IncrementalLoadingItems?.Count ?? 0;
			HasItem.Value = LoadedItemsCount.Value > 0;
		}

		protected virtual void PostResetList() { }

		protected abstract uint IncrementalLoadCount { get; }

		abstract protected IIncrementalSource<ITEM_VM> GenerateIncrementalSource();

		protected virtual bool CheckNeedUpdateOnNavigateTo(NavigationMode mode) { return mode != NavigationMode.Back; }

		protected void ClearSelection()
		{
			SelectedItems.Clear();
		}

		#region Selection


		public ReactiveProperty<bool> IsSelectionModeEnable { get; private set; }
		

		public ReactiveCommand<ITEM_VM> SelectItemCommand { get; private set; }

		

		private DelegateCommand _EnableSelectionCommand;
		public DelegateCommand EnableSelectionCommand
		{
			get
			{
				return _EnableSelectionCommand
					?? (_EnableSelectionCommand = new DelegateCommand(() => 
					{
						IsSelectionModeEnable.Value = true;
					}));
			}
		}


		private DelegateCommand _DisableSelectionCommand;
		public DelegateCommand DisableSelectionCommand
		{
			get
			{
				return _DisableSelectionCommand
					?? (_DisableSelectionCommand = new DelegateCommand(() =>
					{
						IsSelectionModeEnable.Value = false;
					}));
			}
		}

		#endregion



		private DelegateCommand _RefreshCommand;
		public DelegateCommand RefreshCommand
		{
			get
			{
				return _RefreshCommand
					?? (_RefreshCommand = new DelegateCommand(() => 
					{
						IncrementalLoadingItems.Clear();
					}));
			}
		}

		public ReactiveProperty<int> MaxItemsCount { get; private set; }
		public ReactiveProperty<int> LoadedItemsCount { get; private set; }
		public ReactiveProperty<int> SelectedItemsCount { get; private set; }



		public ObservableCollection<ITEM_VM> SelectedItems { get; private set; }

		public IncrementalLoadingCollection<IIncrementalSource<ITEM_VM>, ITEM_VM> IncrementalLoadingItems { get; private set; }
		private IncrementalLoadingCollection<IIncrementalSource<ITEM_VM>, ITEM_VM> _IncrementalLoadingItems;

		public ReactiveProperty<double> ListViewVerticalOffset { get; private set; }
		private double _LastListViewOffset;

		public ReactiveProperty<bool> NowLoadingItems { get; private set; }

		public ReactiveProperty<bool> NowRefreshable { get; private set; }

		public ReactiveProperty<bool> HasItem { get; private set; }

		
		public bool PageIsRequireSignIn { get; private set; }
		public bool NowSignedIn { get; private set; }

	}
}

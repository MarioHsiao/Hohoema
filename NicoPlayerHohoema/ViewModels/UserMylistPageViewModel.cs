﻿using NicoPlayerHohoema.Models;
using Prism.Windows.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prism.Windows.Navigation;
using Prism.Mvvm;
using Prism.Commands;
using Mntone.Nico2.Mylist.MylistGroup;
using System.Collections.ObjectModel;
using Reactive.Bindings;
using Mntone.Nico2.Mylist;
using Reactive.Bindings.Extensions;
using System.Threading;

namespace NicoPlayerHohoema.ViewModels
{
	public class UserMylistPageViewModel : HohoemaViewModelBase
	{
		public UserMylistPageViewModel(HohoemaApp app, PageManager pageMaanger)
			: base(app, pageMaanger)
		{
			MylistGroupItems = new ObservableCollection<MylistGroupListItem>();
			IsPublicMylist = new ReactiveProperty<bool>(false)
				.AddTo(_CompositeDisposable);
			IsHostedMylist = new ReactiveProperty<bool>(false)
				.AddTo(_CompositeDisposable);
		}


		public override void OnNavigatedTo(NavigatedToEventArgs e, Dictionary<string, object> viewModelState)
		{
			base.OnNavigatedTo(e, viewModelState);
		}

		protected override async Task NavigatedToAsync(CancellationToken cancelToken, NavigatedToEventArgs e, Dictionary<string, object> viewModelState)
		{
			List<MylistGroupData> mylists = null;

			if (e.Parameter is string)
			{
				UserId = e.Parameter as string;				
			}

			MylistGroupItems.Clear();

			if (UserId != null)
			{
				try
				{
					var userInfo = await HohoemaApp.NiconicoContext.User.GetUserAsync(UserId);
					UserName = userInfo.Nickname;
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine(ex.Message);
				}

				

				try
				{
					mylists = await HohoemaApp.ContentFinder.GetUserMylistGroups(UserId);
					IsPublicMylist.Value = true;
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine(ex.Message);
					IsPublicMylist.Value = false;
				}

				IsHostedMylist.Value = false;

				var mylistGroupVMItems = mylists.Select(x => new MylistGroupListItem(x, PageManager));

				foreach (var mylistGroupVM in mylistGroupVMItems)
				{
					MylistGroupItems.Add(mylistGroupVM);
				}
			}
			else
			{
				// ログインユーザーのマイリスト一覧を表示
				try
				{
					var userInfo = await HohoemaApp.NiconicoContext.User.GetInfoAsync();
					UserName = userInfo.Name;
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine(ex.Message);

				}

				var listItems = HohoemaApp.UserMylistManager.UserMylists
					.Select(x => new MylistGroupListItem(x, PageManager));

				foreach (var item in listItems)
				{
					MylistGroupItems.Add(item);
				}

				IsHostedMylist.Value = true;
			}

			
			UpdateTitle($"{UserName} さんのマイリスト一覧");
		}

		public ReactiveProperty<bool> IsHostedMylist { get; private set; }
		public ReactiveProperty<bool> IsPublicMylist { get; private set; }

		public string UserId { get; private set; }

		private string _UserName;
		public string UserName
		{
			get { return _UserName; }
			set { SetProperty(ref _UserName, value); }
		}


		public ObservableCollection<MylistGroupListItem> MylistGroupItems { get; private set; }
	}



	public class MylistGroupListItem : BindableBase
	{
		public MylistGroupListItem(MylistGroupInfo info, PageManager pageManager)
		{
			_PageManager = pageManager;

			Title = info.Name;
			Description = info.Description;
			GroupId = info.GroupId;
			IsPublic = info.IsPublic;


		}
		public MylistGroupListItem(MylistGroupData mylistGroup, PageManager pageManager)
		{
			_PageManager = pageManager;

			Title = mylistGroup.Name;
			Description = mylistGroup.Description;
			GroupId = mylistGroup.Id;
			IsPublic = mylistGroup.GetIsPublic();
			
		}

		private DelegateCommand _OpenMylistCommand;
		public DelegateCommand OpenMylistCommand
		{
			get
			{
				return _OpenMylistCommand
					?? (_OpenMylistCommand = new DelegateCommand(() =>
					{
						_PageManager.OpenPage(HohoemaPageType.Mylist, GroupId);
					}
					));
			}
		}

		public bool IsPublic { get; set; }

		public string Title { get; set; }

		public string Description { get; set; }

		public string GroupId { get; private set; }


		PageManager _PageManager;
	}
}
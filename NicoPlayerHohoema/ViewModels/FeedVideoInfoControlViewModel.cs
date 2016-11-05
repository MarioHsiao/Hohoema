﻿using NicoPlayerHohoema.Models;
using Prism.Commands;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoPlayerHohoema.ViewModels
{
	public class FeedVideoInfoControlViewModel : VideoInfoControlViewModel
	{
		public FeedVideoInfoControlViewModel(FavFeedItem feedItem, IFeedGroup feedGroup, NicoVideo nicoVideo, PageManager pageMan)
			: base(nicoVideo, pageMan)
		{
			IsUnread = feedItem.ToReactivePropertyAsSynchronized(x => x.IsUnread)
				.AddTo(_CompositeDisposable);

			_FafFeedItem = feedItem;
			_FeedGroup = feedGroup;
		}

		FavFeedItem _FafFeedItem;
		IFeedGroup _FeedGroup;

		public ReactiveProperty<bool> IsUnread { get; private set; }
		public FavoriteItemType SourceType { get; private set; }
		public string SourceTitle { get; private set; }
	}

}

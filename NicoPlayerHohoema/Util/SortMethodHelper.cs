﻿using Mntone.Nico2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoPlayerHohoema.Util
{
	public static class SortHelper
	{
		public static string ToCulturizedText(Sort sort, Order order)
		{
			var isAscending = order == Order.Ascending;
			switch (sort)
			{
				case Sort.NewComment:
					return isAscending ? "コメントが古い順" : "コメントが新しい順";
				case Sort.ViewCount:
					return isAscending ? "再生数が少ない順" : "再生数が多い順";
				case Sort.MylistCount:
					return isAscending ? "マイリスト数が少ない順" : "マイリスト数が多い順";
				case Sort.CommentCount:
					return isAscending ? "コメント数が少ない順" : "コメント数が多い順";
				case Sort.FirstRetrieve:
					return isAscending ? "投稿日時が古い順" : "投稿日時が新しい順";
				case Sort.Length:
					return isAscending ? "動画時間が短い順" : "動画時間が長い順";
				case Sort.Popurarity:
					return isAscending ? "人気が低い順" : "人気が高い順";
				case Sort.MylistPopurarity:
					return isAscending ? "人気が低い順" : "人気が高い順";
				case Sort.VideoCount:
					return isAscending ? "動画数が少ない順" : "動画数が多い順";
				case Sort.UpdateTime:
					return isAscending ? "更新が古い順" : "更新が新しい順";
				case Sort.Relation:
					return isAscending ? "適合率が低い順" : "適合率が高い順";
				default:
					throw new NotSupportedException();
			}
		}
	}
}

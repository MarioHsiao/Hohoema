﻿using Mntone.Nico2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoPlayerHohoema.Util
{
	public static class SortMethodHelper
	{
		public static string ToCulturizedText(SortMethod method, SortDirection dir)
		{
			var isAscending = dir == SortDirection.Ascending;
			switch (method)
			{
				case SortMethod.NewComment:
					return isAscending ? "コメントが古い順" : "コメントが新しい順";
				case SortMethod.ViewCount:
					return isAscending ? "再生数が少ない順" : "再生数が多い順";
				case SortMethod.MylistCount:
					return isAscending ? "マイリスト数が少ない順" : "マイリスト数が多い順";
				case SortMethod.CommentCount:
					return isAscending ? "コメント数が少ない順" : "コメント数が多い順";
				case SortMethod.FirstRetrieve:
					return isAscending ? "投稿日時が古い順" : "投稿日時が新しい順";
				case SortMethod.Length:
					return isAscending ? "動画時間が短い順" : "動画時間が長い順";
				case SortMethod.Popurarity:
					return "人気が高い順";
				default:
					throw new NotSupportedException();
			}
		}
	}
}
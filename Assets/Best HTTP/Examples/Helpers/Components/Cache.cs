using System;
using System.Collections.Generic;
using BestHTTP.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BestHTTP.Examples.Helpers.Components
{
	public class Cache : MonoBehaviour
	{
#pragma warning disable 0649, 0169
		[SerializeField] Text _count;

		[SerializeField] Text _size;

		[SerializeField] Button _clear;
#pragma warning restore

		void Start()
		{
			PluginEventHelper.OnEvent += OnPluginEvent;
			UpdateLabels();
		}

		void OnDestroy()
		{
			PluginEventHelper.OnEvent -= OnPluginEvent;
		}

		void OnPluginEvent(PluginEventInfo @event)
		{
			if (@event.Event == PluginEvents.SaveCacheLibrary)
			{
				UpdateLabels();
			}
		}

		void UpdateLabels()
		{
#if !BESTHTTP_DISABLE_CACHING
			_count.text = Caching.HTTPCacheService.GetCacheEntityCount().ToString("N0");
			_size.text = Caching.HTTPCacheService.GetCacheSize().ToString("N0");
#else
            this._count.text = "0";
            this._size.text = "0";
#endif
		}

		public void OnClearButtonClicked()
		{
#if !BESTHTTP_DISABLE_CACHING
			Caching.HTTPCacheService.BeginClear();
#endif
		}
	}
}
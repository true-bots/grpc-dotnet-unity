using BestHTTP.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using BestHTTP.Cookies;
using UnityEngine;
using UnityEngine.UI;

namespace BestHTTP.Examples.Helpers.Components
{
	public class Cookies : MonoBehaviour
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
#if !BESTHTTP_DISABLE_COOKIES
			if (@event.Event == PluginEvents.SaveCookieLibrary)
			{
				UpdateLabels();
			}
#endif
		}

		void UpdateLabels()
		{
#if !BESTHTTP_DISABLE_COOKIES
			List<Cookie> cookies = BestHTTP.Cookies.CookieJar.GetAll();
			long size = cookies.Sum(c => c.GuessSize());

			_count.text = cookies.Count.ToString("N0");
			_size.text = size.ToString("N0");
#else
            this._count.text = "0";
            this._size.text = "0";
#endif
		}

		public void OnClearButtonClicked()
		{
#if !BESTHTTP_DISABLE_COOKIES
			BestHTTP.Cookies.CookieJar.Clear();
#endif
		}
	}
}
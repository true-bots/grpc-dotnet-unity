#if !BESTHTTP_DISABLE_SERVERSENT_EVENTS

using System;
using BestHTTP.Examples.Helpers;
using BestHTTP.ServerSentEvents;
using UnityEngine;
using UnityEngine.UI;

namespace BestHTTP.Examples.ServerSentEvents
{
	public class SimpleSample : SampleBase
	{
#pragma warning disable 0649

		[Tooltip("The url of the resource to use.")] [SerializeField]
		string _path = "/sse";

		[SerializeField] ScrollRect _scrollRect;

		[SerializeField] RectTransform _contentRoot;

		[SerializeField] TextListItem _listItemPrefab;

		[SerializeField] int _maxListItemEntries = 100;

		[SerializeField] Button _startButton;

		[SerializeField] Button _closeButton;

#pragma warning restore

		EventSource eventSource;

		protected override void Start()
		{
			base.Start();

			SetButtons(true, false);
		}

		void OnDestroy()
		{
			if (eventSource != null)
			{
				eventSource.Close();
				eventSource = null;
			}
		}

		public void OnStartButton()
		{
			GUIHelper.RemoveChildren(_contentRoot, 0);

			// Create the EventSource instance
			eventSource = new EventSource(new Uri(sampleSelector.BaseURL + _path));

			// Subscribe to generic events
			eventSource.OnOpen += OnOpen;
			eventSource.OnClosed += OnClosed;
			eventSource.OnError += OnError;
			eventSource.OnStateChanged += OnStateChanged;
			eventSource.OnMessage += OnMessage;

			// Subscribe to an application specific event
			eventSource.On("datetime", OnDateTime);

			// Start to connect to the server
			eventSource.Open();

			AddText("Opening Server-Sent Events...");

			SetButtons(false, true);
		}

		public void OnCloseButton()
		{
			SetButtons(false, false);
			eventSource.Close();
		}

		void OnOpen(EventSource eventSource)
		{
			AddText("Open");
		}

		void OnClosed(EventSource eventSource)
		{
			AddText("Closed");

			this.eventSource = null;

			SetButtons(true, false);
		}

		void OnError(EventSource eventSource, string error)
		{
			AddText(string.Format("Error: <color=red>{0}</color>", error));
		}

		void OnStateChanged(EventSource eventSource, States oldState, States newState)
		{
			AddText(string.Format("State Changed {0} => {1}", oldState, newState));
		}

		void OnMessage(EventSource eventSource, Message message)
		{
			AddText(string.Format("Message: <color=yellow>{0}</color>", message));
		}

		void OnDateTime(EventSource eventSource, Message message)
		{
			DateTimeData dtData = JSON.LitJson.JsonMapper.ToObject<DateTimeData>(message.Data);

			AddText(string.Format("OnDateTime: <color=yellow>{0}</color>", dtData.ToString()));
		}

		void SetButtons(bool start, bool close)
		{
			if (_startButton != null)
			{
				_startButton.interactable = start;
			}

			if (_closeButton != null)
			{
				_closeButton.interactable = close;
			}
		}

		void AddText(string text)
		{
			GUIHelper.AddText(_listItemPrefab, _contentRoot, text, _maxListItemEntries, _scrollRect);
		}
	}

	[PlatformSupport.IL2CPP.Preserve]
	sealed class DateTimeData
	{
#pragma warning disable 0649
		[PlatformSupport.IL2CPP.Preserve] public int eventid;

		[PlatformSupport.IL2CPP.Preserve] public string datetime;
#pragma warning restore

		public override string ToString()
		{
			return string.Format("[DateTimeData EventId: {0}, DateTime: {1}]", eventid, datetime);
		}
	}
}
#endif
#if !BESTHTTP_DISABLE_WEBSOCKET

using System;
using BestHTTP.Examples.Helpers;
using UnityEngine;
using UnityEngine.UI;

namespace BestHTTP.Examples.Websockets
{
	public class WebSocketSample : SampleBase
	{
#pragma warning disable 0649

		[SerializeField] [Tooltip("The WebSocket address to connect")]
		string address = "wss://besthttpwebgldemo.azurewebsites.net/ws";

		[SerializeField] InputField _input;

		[SerializeField] ScrollRect _scrollRect;

		[SerializeField] RectTransform _contentRoot;

		[SerializeField] TextListItem _listItemPrefab;

		[SerializeField] int _maxListItemEntries = 100;

		[SerializeField] Button _connectButton;

		[SerializeField] Button _closeButton;

#pragma warning restore

		/// <summary>
		/// Saved WebSocket instance
		/// </summary>
		WebSocket.WebSocket webSocket;

		protected override void Start()
		{
			base.Start();

			SetButtons(true, false);
			_input.interactable = false;
		}

		void OnDestroy()
		{
			if (webSocket != null)
			{
				webSocket.Close();
				webSocket = null;
			}
		}

		public void OnConnectButton()
		{
			// Create the WebSocket instance
			webSocket = new WebSocket.WebSocket(new Uri(address));

#if !UNITY_WEBGL || UNITY_EDITOR
			webSocket.StartPingThread = true;

#if !BESTHTTP_DISABLE_PROXY && (!UNITY_WEBGL || UNITY_EDITOR)
			if (HTTPManager.Proxy != null)
			{
				webSocket.OnInternalRequestCreated = (ws, internalRequest) =>
					internalRequest.Proxy = new HTTPProxy(HTTPManager.Proxy.Address, HTTPManager.Proxy.Credentials, false);
			}
#endif
#endif

			// Subscribe to the WS events
			webSocket.OnOpen += OnOpen;
			webSocket.OnMessage += OnMessageReceived;
			webSocket.OnClosed += OnClosed;
			webSocket.OnError += OnError;

			// Start connecting to the server
			webSocket.Open();

			AddText("Connecting...");

			SetButtons(false, true);
			_input.interactable = false;
		}

		public void OnCloseButton()
		{
			AddText("Closing!");
			// Close the connection
			webSocket.Close(1000, "Bye!");

			SetButtons(false, false);
			_input.interactable = false;
		}

		public void OnInputField(string textToSend)
		{
			if ((!Input.GetKeyDown(KeyCode.KeypadEnter) && !Input.GetKeyDown(KeyCode.Return)) || string.IsNullOrEmpty(textToSend))
			{
				return;
			}

			AddText(string.Format("Sending message: <color=green>{0}</color>", textToSend))
				.AddLeftPadding(20);

			// Send message to the server
			webSocket.Send(textToSend);
		}

		#region WebSocket Event Handlers

		/// <summary>
		/// Called when the web socket is open, and we are ready to send and receive data
		/// </summary>
		void OnOpen(WebSocket.WebSocket ws)
		{
			AddText("WebSocket Open!");

			_input.interactable = true;
		}

		/// <summary>
		/// Called when we received a text message from the server
		/// </summary>
		void OnMessageReceived(WebSocket.WebSocket ws, string message)
		{
			AddText(string.Format("Message received: <color=yellow>{0}</color>", message))
				.AddLeftPadding(20);
		}

		/// <summary>
		/// Called when the web socket closed
		/// </summary>
		void OnClosed(WebSocket.WebSocket ws, ushort code, string message)
		{
			AddText(string.Format("WebSocket closed! Code: {0} Message: {1}", code, message));

			webSocket = null;

			SetButtons(true, false);
		}

		/// <summary>
		/// Called when an error occured on client side
		/// </summary>
		void OnError(WebSocket.WebSocket ws, string error)
		{
			AddText(string.Format("An error occured: <color=red>{0}</color>", error));

			webSocket = null;

			SetButtons(true, false);
		}

		#endregion

		void SetButtons(bool connect, bool close)
		{
			if (_connectButton != null)
			{
				_connectButton.interactable = connect;
			}

			if (_closeButton != null)
			{
				_closeButton.interactable = close;
			}
		}

		TextListItem AddText(string text)
		{
			return GUIHelper.AddText(_listItemPrefab, _contentRoot, text, _maxListItemEntries, _scrollRect);
		}
	}
}

#endif
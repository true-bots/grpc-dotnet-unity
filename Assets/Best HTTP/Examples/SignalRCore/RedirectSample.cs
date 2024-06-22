#if !BESTHTTP_DISABLE_SIGNALR_CORE

using BestHTTP;
using BestHTTP.Connections;
using BestHTTP.Examples.Helpers;
using BestHTTP.SignalRCore;
using BestHTTP.SignalRCore.Encoders;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace BestHTTP.Examples
{
	/// <summary>
	/// This sample demonstrates redirection capabilities. The server will redirect a few times the client before
	/// routing it to the final endpoint.
	/// </summary>
	public sealed class RedirectSample : SampleBase
	{
#pragma warning disable 0649

		[SerializeField] string _path = "/redirect_sample";

		[SerializeField] ScrollRect _scrollRect;

		[SerializeField] RectTransform _contentRoot;

		[SerializeField] TextListItem _listItemPrefab;

		[SerializeField] int _maxListItemEntries = 100;

		[SerializeField] Button _connectButton;

		[SerializeField] Button _closeButton;

#pragma warning restore

		// Instance of the HubConnection
		public HubConnection hub;

		protected override void Start()
		{
			base.Start();

			SetButtons(true, false);
		}

		void OnDestroy()
		{
			if (hub != null)
			{
				hub.StartClose();
			}
		}

		public void OnConnectButton()
		{
			// Server side of this example can be found here:
			// https://github.com/Benedicht/BestHTTP_DemoSite/blob/master/BestHTTP_DemoSite/Hubs/

#if BESTHTTP_SIGNALR_CORE_ENABLE_MESSAGEPACK_CSHARP
            try
            {
                MessagePack.Resolvers.StaticCompositeResolver.Instance.Register(
                    MessagePack.Resolvers.DynamicEnumAsStringResolver.Instance,
                    MessagePack.Unity.UnityResolver.Instance,
                    //MessagePack.Unity.Extension.UnityBlitWithPrimitiveArrayResolver.Instance,
                    //MessagePack.Resolvers.StandardResolver.Instance,
                    MessagePack.Resolvers.ContractlessStandardResolver.Instance
                );

                var options = MessagePack.MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.StaticCompositeResolver.Instance);
                MessagePack.MessagePackSerializer.DefaultOptions = options;
            }
            catch
            { }
#endif

			IProtocol protocol = null;
#if BESTHTTP_SIGNALR_CORE_ENABLE_MESSAGEPACK_CSHARP
            protocol = new MessagePackCSharpProtocol();
#elif BESTHTTP_SIGNALR_CORE_ENABLE_GAMEDEVWARE_MESSAGEPACK
            protocol = new MessagePackProtocol();
#else
			protocol = new JsonProtocol(new LitJsonEncoder());
#endif

			// Crete the HubConnection
			hub = new HubConnection(new Uri(sampleSelector.BaseURL + _path), protocol);
			hub.AuthenticationProvider = new RedirectLoggerAccessTokenAuthenticator(hub);

			// Subscribe to hub events
			hub.OnConnected += Hub_OnConnected;
			hub.OnError += Hub_OnError;
			hub.OnClosed += Hub_OnClosed;

			hub.OnRedirected += Hub_Redirected;

			hub.OnTransportEvent += (hub, transport, ev) =>
				AddText(string.Format("Transport(<color=green>{0}</color>) event: <color=green>{1}</color>", transport.TransportType, ev));

			// And finally start to connect to the server
			hub.StartConnect();

			AddText("StartConnect called");
			SetButtons(false, false);
		}

		public void OnCloseButton()
		{
			if (hub != null)
			{
				AddText("Calling StartClose");

				hub.StartClose();

				SetButtons(false, false);
			}
		}

		void Hub_Redirected(HubConnection hub, Uri oldUri, Uri newUri)
		{
			AddText(string.Format("Hub connection redirected to '<color=green>{0}</color>'!", hub.Uri));
		}

		/// <summary>
		/// This callback is called when the plugin is connected to the server successfully. Messages can be sent to the server after this point.
		/// </summary>
		void Hub_OnConnected(HubConnection hub)
		{
			AddText(string.Format("Hub Connected with <color=green>{0}</color> transport using the <color=green>{1}</color> encoder.",
				hub.Transport.TransportType.ToString(), hub.Protocol.Name));

			// Call a parameterless function. We expect a string return value.
			hub.Invoke<string>("Echo", "Message from the client")
				.OnSuccess(ret => AddText(string.Format(" '<color=green>Echo</color>' returned: '<color=yellow>{0}</color>'", ret)));

			SetButtons(false, true);
		}

		/// <summary>
		/// This is called when the hub is closed after a StartClose() call.
		/// </summary>
		void Hub_OnClosed(HubConnection hub)
		{
			AddText("Hub Closed");
			SetButtons(true, false);
		}

		/// <summary>
		/// Called when an unrecoverable error happen. After this event the hub will not send or receive any messages.
		/// </summary>
		void Hub_OnError(HubConnection hub, string error)
		{
			AddText(string.Format("Hub Error: <color=red>{0}</color>", error));
			SetButtons(true, false);
		}

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

		void AddText(string text)
		{
			GUIHelper.AddText(_listItemPrefab, _contentRoot, text, _maxListItemEntries, _scrollRect);
		}
	}

	public sealed class RedirectLoggerAccessTokenAuthenticator : IAuthenticationProvider
	{
		/// <summary>
		/// No pre-auth step required for this type of authentication
		/// </summary>
		public bool IsPreAuthRequired
		{
			get { return false; }
		}

#pragma warning disable 0067
		/// <summary>
		/// Not used event as IsPreAuthRequired is false
		/// </summary>
		public event OnAuthenticationSuccededDelegate OnAuthenticationSucceded;

		/// <summary>
		/// Not used event as IsPreAuthRequired is false
		/// </summary>
		public event OnAuthenticationFailedDelegate OnAuthenticationFailed;

#pragma warning restore 0067

		HubConnection _connection;

		public RedirectLoggerAccessTokenAuthenticator(HubConnection connection)
		{
			_connection = connection;
		}

		/// <summary>
		/// Not used as IsPreAuthRequired is false
		/// </summary>
		public void StartAuthentication()
		{
		}

		/// <summary>
		/// Prepares the request by adding two headers to it
		/// </summary>
		public void PrepareRequest(HTTPRequest request)
		{
			request.SetHeader("x-redirect-count", _connection.RedirectCount.ToString());

			if (HTTPProtocolFactory.GetProtocolFromUri(request.CurrentUri) == SupportedProtocols.HTTP)
			{
				request.Uri = PrepareUri(request.Uri);
			}
		}

		public Uri PrepareUri(Uri uri)
		{
			if (_connection.NegotiationResult != null && !string.IsNullOrEmpty(_connection.NegotiationResult.AccessToken))
			{
				string query = string.IsNullOrEmpty(uri.Query) ? "?" : uri.Query + "&";
				UriBuilder uriBuilder = new UriBuilder(uri.Scheme, uri.Host, uri.Port, uri.AbsolutePath,
					query + "access_token=" + _connection.NegotiationResult.AccessToken);
				return uriBuilder.Uri;
			}
			else
			{
				return uri;
			}
		}

		public void Cancel()
		{
		}
	}
}

#endif
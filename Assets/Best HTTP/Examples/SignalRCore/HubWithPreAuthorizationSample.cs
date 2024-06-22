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
	public sealed class HubWithPreAuthorizationSample : SampleBase
	{
#pragma warning disable 0649

		[SerializeField] string _hubPath = "/HubWithAuthorization";

		[SerializeField] string _jwtTokenPath = "/generateJwtToken";

		[SerializeField] ScrollRect _scrollRect;

		[SerializeField] RectTransform _contentRoot;

		[SerializeField] TextListItem _listItemPrefab;

		[SerializeField] int _maxListItemEntries = 100;

		[SerializeField] Button _connectButton;

		[SerializeField] Button _closeButton;

#pragma warning restore

		// Instance of the HubConnection
		HubConnection hub;

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
			hub = new HubConnection(new Uri(sampleSelector.BaseURL + _hubPath), protocol);

			hub.AuthenticationProvider = new PreAuthAccessTokenAuthenticator(new Uri(sampleSelector.BaseURL + _jwtTokenPath));

			hub.AuthenticationProvider.OnAuthenticationSucceded += AuthenticationProvider_OnAuthenticationSucceded;
			hub.AuthenticationProvider.OnAuthenticationFailed += AuthenticationProvider_OnAuthenticationFailed;

			// Subscribe to hub events
			hub.OnConnected += Hub_OnConnected;
			hub.OnError += Hub_OnError;
			hub.OnClosed += Hub_OnClosed;

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
				hub.StartClose();

				AddText("StartClose called");
				SetButtons(false, false);
			}
		}

		void AuthenticationProvider_OnAuthenticationSucceded(IAuthenticationProvider provider)
		{
			string str = string.Format("Pre-Authentication Succeded! Token: '<color=green>{0}</color>' ",
				(hub.AuthenticationProvider as PreAuthAccessTokenAuthenticator).Token);

			AddText(str);
		}

		void AuthenticationProvider_OnAuthenticationFailed(IAuthenticationProvider provider, string reason)
		{
			AddText(string.Format("Authentication Failed! Reason: '{0}'", reason));
		}

		/// <summary>
		/// This callback is called when the plugin is connected to the server successfully. Messages can be sent to the server after this point.
		/// </summary>
		void Hub_OnConnected(HubConnection hub)
		{
			AddText(string.Format("Hub Connected with <color=green>{0}</color> transport using the <color=green>{1}</color> encoder.",
				hub.Transport.TransportType.ToString(), hub.Protocol.Name));
			SetButtons(false, true);

			// Call a parameterless function. We expect a string return value.
			hub.Invoke<string>("Echo", "Message from the client")
				.OnSuccess(ret => AddText(string.Format("'<color=green>Echo</color>' returned: '<color=yellow>{0}</color>'", ret)).AddLeftPadding(20));

			AddText("'<color=green>Message from the client</color>' sent!")
				.AddLeftPadding(20);
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

		TextListItem AddText(string text)
		{
			return GUIHelper.AddText(_listItemPrefab, _contentRoot, text, _maxListItemEntries, _scrollRect);
		}
	}

	public sealed class PreAuthAccessTokenAuthenticator : IAuthenticationProvider
	{
		/// <summary>
		/// No pre-auth step required for this type of authentication
		/// </summary>
		public bool IsPreAuthRequired
		{
			get { return true; }
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

		public string Token { get; private set; }

		Uri authenticationUri;

		HTTPRequest authenticationRequest;
		bool isCancellationRequested;

		public PreAuthAccessTokenAuthenticator(Uri authUri)
		{
			authenticationUri = authUri;
		}

		public void StartAuthentication()
		{
			authenticationRequest = new HTTPRequest(authenticationUri, OnAuthenticationRequestFinished);
			authenticationRequest.Send();
		}

		void OnAuthenticationRequestFinished(HTTPRequest req, HTTPResponse resp)
		{
			switch (req.State)
			{
				// The request finished without any problem.
				case HTTPRequestStates.Finished:
					if (resp.IsSuccess)
					{
						authenticationRequest = null;
						Token = resp.DataAsText;
						if (OnAuthenticationSucceded != null)
						{
							OnAuthenticationSucceded(this);
						}
					}
					else // Internal server error?
					{
						AuthenticationFailed(string.Format("Request Finished Successfully, but the server sent an error. Status Code: {0}-{1} Message: {2}",
							resp.StatusCode,
							resp.Message,
							resp.DataAsText));
					}

					break;

				// The request finished with an unexpected error. The request's Exception property may contain more info about the error.
				case HTTPRequestStates.Error:
					AuthenticationFailed("Request Finished with Error! " +
					                     (req.Exception != null ? req.Exception.Message + "" + req.Exception.StackTrace : "No Exception"));
					break;

				// The request aborted, initiated by the user.
				case HTTPRequestStates.Aborted:
					AuthenticationFailed("Request Aborted!");
					break;

				// Connecting to the server is timed out.
				case HTTPRequestStates.ConnectionTimedOut:
					AuthenticationFailed("Connection Timed Out!");
					break;

				// The request didn't finished in the given time.
				case HTTPRequestStates.TimedOut:
					AuthenticationFailed("Processing the request Timed Out!");
					break;
			}
		}

		void AuthenticationFailed(string reason)
		{
			authenticationRequest = null;

			if (isCancellationRequested)
			{
				return;
			}

			if (OnAuthenticationFailed != null)
			{
				OnAuthenticationFailed(this, reason);
			}
		}

		/// <summary>
		/// Prepares the request by adding two headers to it
		/// </summary>
		public void PrepareRequest(HTTPRequest request)
		{
			if (HTTPProtocolFactory.GetProtocolFromUri(request.CurrentUri) == SupportedProtocols.HTTP)
			{
				request.Uri = PrepareUri(request.Uri);
			}
		}

		public Uri PrepareUri(Uri uri)
		{
			if (!string.IsNullOrEmpty(Token))
			{
				string query = string.IsNullOrEmpty(uri.Query) ? "?" : uri.Query + "&";
				UriBuilder uriBuilder = new UriBuilder(uri.Scheme, uri.Host, uri.Port, uri.AbsolutePath, query + "access_token=" + Token);
				return uriBuilder.Uri;
			}
			else
			{
				return uri;
			}
		}

		public void Cancel()
		{
			isCancellationRequested = true;
			if (authenticationRequest != null)
			{
				authenticationRequest.Abort();
			}
		}
	}
}

#endif
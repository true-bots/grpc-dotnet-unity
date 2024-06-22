#if !BESTHTTP_DISABLE_SIGNALR_CORE
using System;

namespace BestHTTP.SignalRCore.Authentication
{
	public sealed class DefaultAccessTokenAuthenticator : IAuthenticationProvider
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

		public DefaultAccessTokenAuthenticator(HubConnection connection)
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
			if (_connection.NegotiationResult == null)
			{
				return;
			}

			// Add Authorization header to http requests, add access_token param to the uri otherwise
			if (Connections.HTTPProtocolFactory.GetProtocolFromUri(request.CurrentUri) == Connections.SupportedProtocols.HTTP)
			{
				request.SetHeader("Authorization", "Bearer " + _connection.NegotiationResult.AccessToken);
			}
			else
#if !BESTHTTP_DISABLE_WEBSOCKET
			if (Connections.HTTPProtocolFactory.GetProtocolFromUri(request.Uri) != Connections.SupportedProtocols.WebSocket)
			{
				request.Uri = PrepareUriImpl(request.Uri);
			}
#else
                ;
#endif
		}

		public Uri PrepareUri(Uri uri)
		{
			if (_connection.NegotiationResult == null)
			{
				return uri;
			}

			if (uri.Query.StartsWith("??"))
			{
				UriBuilder builder = new UriBuilder(uri);
				builder.Query = builder.Query.Substring(2);

				return builder.Uri;
			}

#if !BESTHTTP_DISABLE_WEBSOCKET
			if (Connections.HTTPProtocolFactory.GetProtocolFromUri(uri) == Connections.SupportedProtocols.WebSocket)
			{
				uri = PrepareUriImpl(uri);
			}
#endif

			return uri;
		}

		Uri PrepareUriImpl(Uri uri)
		{
			if (_connection.NegotiationResult != null && !string.IsNullOrEmpty(_connection.NegotiationResult.AccessToken))
			{
				string query = string.IsNullOrEmpty(uri.Query) ? "" : uri.Query + "&";
				UriBuilder uriBuilder = new UriBuilder(uri.Scheme, uri.Host, uri.Port, uri.AbsolutePath,
					query + "access_token=" + _connection.NegotiationResult.AccessToken);
				return uriBuilder.Uri;
			}

			return uri;
		}

		public void Cancel()
		{
		}
	}
}
#endif
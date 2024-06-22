#if !BESTHTTP_DISABLE_SIGNALR_CORE
using System;
using System.Collections.Generic;
using System.Text;
using BestHTTP.Logger;
using BestHTTP.PlatformSupport.Memory;
using BestHTTP.PlatformSupport.Text;

namespace BestHTTP.SignalRCore.Transports
{
	abstract class TransportBase : ITransport
	{
		public abstract TransportTypes TransportType { get; }

		public TransferModes TransferMode
		{
			get { return TransferModes.Binary; }
		}

		/// <summary>
		/// Current state of the transport. All changes will be propagated to the HubConnection through the onstateChanged event.
		/// </summary>
		public TransportStates State
		{
			get { return _state; }
			protected set
			{
				if (_state != value)
				{
					TransportStates oldState = _state;
					_state = value;

					if (OnStateChanged != null)
					{
						OnStateChanged(oldState, _state);
					}
				}
			}
		}

		protected TransportStates _state;

		/// <summary>
		/// This will store the reason of failures so HubConnection can include it in its OnError event.
		/// </summary>
		public string ErrorReason { get; protected set; }

		/// <summary>
		/// Called every time when the transport's <see cref="State"/> changed.
		/// </summary>
		public event Action<TransportStates, TransportStates> OnStateChanged;

		public LoggingContext Context { get; protected set; }

		/// <summary>
		/// Cached list of parsed messages.
		/// </summary>
		protected List<Messages.Message> messages = new List<Messages.Message>();

		/// <summary>
		/// Parent HubConnection instance.
		/// </summary>
		protected HubConnection connection;

		internal TransportBase(HubConnection con)
		{
			connection = con;
			Context = new LoggingContext(this);
			Context.Add("Hub", connection.Context);
			State = TransportStates.Initial;
		}

		/// <summary>
		/// ITransport.StartConnect
		/// </summary>
		public abstract void StartConnect();

		/// <summary>
		/// ITransport.Send
		/// </summary>
		/// <param name="msg"></param>
		public abstract void Send(BufferSegment msg);

		/// <summary>
		/// ITransport.StartClose
		/// </summary>
		public abstract void StartClose();

		protected string ParseHandshakeResponse(string data)
		{
			// The handshake response is
			//  -an empty json object ('{}') if the handshake process is succesfull
			//  -otherwise it has one 'error' field

			Dictionary<string, object> response = JSON.Json.Decode(data) as Dictionary<string, object>;

			if (response == null)
			{
				return "Couldn't parse json data: " + data;
			}

			object error;
			if (response.TryGetValue("error", out error))
			{
				return error.ToString();
			}

			return null;
		}

		protected void HandleHandshakeResponse(string data)
		{
			ErrorReason = ParseHandshakeResponse(data);

			State = string.IsNullOrEmpty(ErrorReason) ? TransportStates.Connected : TransportStates.Failed;
		}

		//StringBuilder queryBuilder = new StringBuilder(3);
		protected Uri BuildUri(Uri baseUri)
		{
			if (connection.NegotiationResult == null)
			{
				return baseUri;
			}

			UriBuilder builder = new UriBuilder(baseUri);

			StringBuilder queryBuilder = StringBuilderPool.Get(3);

			queryBuilder.Append(baseUri.Query);
			if (!string.IsNullOrEmpty(connection.NegotiationResult.ConnectionToken))
			{
				queryBuilder.Append("&id=").Append(connection.NegotiationResult.ConnectionToken);
			}
			else if (!string.IsNullOrEmpty(connection.NegotiationResult.ConnectionId))
			{
				queryBuilder.Append("&id=").Append(connection.NegotiationResult.ConnectionId);
			}

			builder.Query = StringBuilderPool.ReleaseAndGrab(queryBuilder);

			if (builder.Query.StartsWith("??"))
			{
				builder.Query = builder.Query.Substring(2);
			}

			return builder.Uri;
		}
	}
}
#endif
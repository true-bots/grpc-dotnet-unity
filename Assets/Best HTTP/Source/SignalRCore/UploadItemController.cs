#if !BESTHTTP_DISABLE_SIGNALR_CORE
using System;
using BestHTTP;
using BestHTTP.Futures;
using BestHTTP.SignalRCore.Messages;

namespace BestHTTP.SignalRCore
{
	public interface IUPloadItemController<TResult> : IDisposable
	{
		string[] StreamingIDs { get; }
		HubConnection Hub { get; }

		void UploadParam<T>(string streamId, T item);
		void Cancel();
	}

	public sealed class DownStreamItemController<TResult> : IFuture<TResult>, IDisposable
	{
		public readonly long invocationId;
		public readonly HubConnection hubConnection;
		public readonly IFuture<TResult> future;

		public FutureState state
		{
			get { return future.state; }
		}

		public TResult value
		{
			get { return future.value; }
		}

		public Exception error
		{
			get { return future.error; }
		}

		public bool IsCanceled { get; private set; }

		public DownStreamItemController(HubConnection hub, long iId, IFuture<TResult> future)
		{
			hubConnection = hub;
			invocationId = iId;
			this.future = future;
		}

		public void Cancel()
		{
			if (IsCanceled)
			{
				return;
			}

			IsCanceled = true;

			Message message = new Message
			{
				type = MessageTypes.CancelInvocation,
				invocationId = invocationId.ToString()
			};

			hubConnection.SendMessage(message);
		}

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Cancel();
		}

		public IFuture<TResult> OnItem(FutureValueCallback<TResult> callback)
		{
			return future.OnItem(callback);
		}

		public IFuture<TResult> OnSuccess(FutureValueCallback<TResult> callback)
		{
			return future.OnSuccess(callback);
		}

		public IFuture<TResult> OnError(FutureErrorCallback callback)
		{
			return future.OnError(callback);
		}

		public IFuture<TResult> OnComplete(FutureCallback<TResult> callback)
		{
			return future.OnComplete(callback);
		}
	}

	public sealed class UpStreamItemController<TResult> : IUPloadItemController<TResult>, IFuture<TResult>
	{
		public readonly long invocationId;
		public readonly string[] streamingIds;
		public readonly HubConnection hubConnection;
		public readonly IFuture<TResult> future;

		public string[] StreamingIDs
		{
			get { return streamingIds; }
		}

		public HubConnection Hub
		{
			get { return hubConnection; }
		}

		public FutureState state
		{
			get { return future.state; }
		}

		public TResult value
		{
			get { return future.value; }
		}

		public Exception error
		{
			get { return future.error; }
		}

		public bool IsFinished { get; private set; }

		public bool IsCanceled { get; private set; }

		object[] streams;

		public UpStreamItemController(HubConnection hub, long iId, string[] sIds, IFuture<TResult> future)
		{
			hubConnection = hub;
			invocationId = iId;
			streamingIds = sIds;
			streams = new object[streamingIds.Length];
			this.future = future;
		}

		public UploadChannel<TResult, T> GetUploadChannel<T>(int paramIdx)
		{
			UploadChannel<TResult, T> stream = streams[paramIdx] as UploadChannel<TResult, T>;
			if (stream == null)
			{
				streams[paramIdx] = stream = new UploadChannel<TResult, T>(this, paramIdx);
			}

			return stream;
		}

		public void UploadParam<T>(string streamId, T item)
		{
			if (streamId == null)
			{
				return;
			}

			Message message = new Message
			{
				type = MessageTypes.StreamItem,
				invocationId = streamId.ToString(),
				item = item
			};

			hubConnection.SendMessage(message);
		}

		public void Finish()
		{
			if (!IsFinished)
			{
				IsFinished = true;

				for (int i = 0; i < streamingIds.Length; ++i)
				{
					if (streamingIds[i] != null)
					{
						Message message = new Message
						{
							type = MessageTypes.Completion,
							invocationId = streamingIds[i].ToString()
						};

						hubConnection.SendMessage(message);
					}
				}
			}
		}

		public void Cancel()
		{
			if (!IsFinished && !IsCanceled)
			{
				IsCanceled = true;

				Message message = new Message
				{
					type = MessageTypes.CancelInvocation,
					invocationId = invocationId.ToString()
				};

				hubConnection.SendMessage(message);

				// Zero out the streaming ids, disabling any future message sending
				Array.Clear(streamingIds, 0, streamingIds.Length);

				// If it's also a down-stream, set it canceled.
				StreamItemContainer<TResult> itemContainer = future.value as StreamItemContainer<TResult>;
				if (itemContainer != null)
				{
					itemContainer.IsCanceled = true;
				}
			}
		}

		void IDisposable.Dispose()
		{
			GC.SuppressFinalize(this);

			Finish();
		}

		public IFuture<TResult> OnItem(FutureValueCallback<TResult> callback)
		{
			return future.OnItem(callback);
		}

		public IFuture<TResult> OnSuccess(FutureValueCallback<TResult> callback)
		{
			return future.OnSuccess(callback);
		}

		public IFuture<TResult> OnError(FutureErrorCallback callback)
		{
			return future.OnError(callback);
		}

		public IFuture<TResult> OnComplete(FutureCallback<TResult> callback)
		{
			return future.OnComplete(callback);
		}
	}

	/// <summary>
	/// An upload channel that represents one prameter of a client callable function. It implements the IDisposable
	/// interface and calls Finish from the Dispose method.
	/// </summary>
	public sealed class UploadChannel<TResult, T> : IDisposable
	{
		/// <summary>
		/// The associated upload controller
		/// </summary>
		public IUPloadItemController<TResult> Controller { get; private set; }

		/// <summary>
		/// What parameter is bound to.
		/// </summary>
		public int ParamIdx { get; private set; }

		/// <summary>
		/// Returns true if Finish() or Cancel() is already called.
		/// </summary>
		public bool IsFinished
		{
			get { return Controller.StreamingIDs[ParamIdx] == null; }
			private set
			{
				if (value)
				{
					Controller.StreamingIDs[ParamIdx] = null;
				}
			}
		}

		/// <summary>
		/// The unique generated id of this parameter channel.
		/// </summary>
		public string StreamingId
		{
			get { return Controller.StreamingIDs[ParamIdx]; }
		}

		internal UploadChannel(IUPloadItemController<TResult> ctrl, int paramIdx)
		{
			Controller = ctrl;
			ParamIdx = paramIdx;
		}

		/// <summary>
		/// Uploads a parameter value to the server.
		/// </summary>
		public void Upload(T item)
		{
			string streamId = StreamingId;
			if (streamId != null)
			{
				Controller.UploadParam(streamId, item);
			}
		}

		/// <summary>
		/// Calling this function cancels the call itself, not just a parameter upload channel.
		/// </summary>
		public void Cancel()
		{
			if (!IsFinished)
			{
				// Cancel all upload stream, cancel will also set streaming ids to 0.
				Controller.Cancel();
			}
		}

		/// <summary>
		/// Finishes the channel by telling the server that no more uplode items will follow.
		/// </summary>
		public void Finish()
		{
			if (!IsFinished)
			{
				string streamId = StreamingId;
				if (streamId != null)
				{
					// this will set the streaming id to 0
					IsFinished = true;

					Message message = new Message
					{
						type = MessageTypes.Completion,
						invocationId = streamId.ToString()
					};

					Controller.Hub.SendMessage(message);
				}
			}
		}

		void IDisposable.Dispose()
		{
			if (!IsFinished)
			{
				Finish();
			}

			GC.SuppressFinalize(this);
		}
	}
}
#endif
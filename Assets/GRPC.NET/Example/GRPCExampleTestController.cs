using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BestHTTP;
using BestHTTP.Logger;
using Grpc.Core;
using Grpc.Net.Client;
using TestProto;
using UnityEngine;
using ColorUtility = UnityEngine.ColorUtility;
using Metadata = Grpc.Core.Metadata;

namespace GRPC.NET.Example
{
	public class GRPCExampleTestController : MonoBehaviour
	{
		GRPCExampleLogger m_Logger;

		GrpcTestCallIdFactory m_CallIdFactory;

		GrpcTestCallFactory m_CallFactoryA;
		GrpcTestCallFactory m_CallFactoryB;

		void Awake()
		{
			m_Logger = gameObject.GetComponent<GRPCExampleLogger>();

			// Setup BestHTTP
			HTTPManager.Setup();
			HTTPManager.Logger.Level = Loglevels.All; // Enable all log levels

			// Shared Id call Factory
			m_CallIdFactory = new GrpcTestCallIdFactory();

			m_CallFactoryA = new GrpcTestCallFactory(m_CallIdFactory, 'A', m_Logger);
			m_CallFactoryB = new GrpcTestCallFactory(m_CallIdFactory, 'B', m_Logger);
		}

		void Update()
		{
			m_CallIdFactory.Update();
		}

		static string MetadataToString(Metadata metadata)
		{
			return string.Join(",", metadata);
		}

		static readonly Metadata AuthMetadata = new Metadata { { "Authorization", "Bearer f623f5c2-46a8-4bdc-9ac5-45358a615a54" } };

		void RunTestCallUnary(GrpcTestCallFactory callFactory, Metadata metadata = null, string detail = null)
		{
			Debug.Log("RunTestCallUnary");

			GrpcTestCall call = callFactory.CreateUnary(metadata, detail);
			call.Call();
		}

		void RunTestCallServerStreaming(GrpcTestCallFactory callFactory)
		{
			Debug.Log("RunTestCallServerStreaming");

			GrpcTestCall call = callFactory.CreateServerStreaming(AuthMetadata);
			call.Call();
		}

		void RunTestCallClientStreaming(GrpcTestCallFactory callFactory)
		{
			Debug.Log("RunTestCallClientStreaming");

			GrpcTestCall call = callFactory.CreateClientStreaming(AuthMetadata);
			call.Call();
		}

		void RunTestCallBiStreaming(GrpcTestCallFactory callFactory)
		{
			Debug.Log("RunTestCallBiStreaming");

			GrpcTestCall call = callFactory.CreateBiStreaming(AuthMetadata);
			call.Call();
		}

		class UnaryGrpcTestCall : GrpcTestCall
		{
			readonly string m_Detail;

			public UnaryGrpcTestCall(GRPCExampleLogger log, GrpcTestCallIdFactory callIdFactory, char factoryId,
				HelloWorldService.HelloWorldServiceClient client, Metadata metadata, string detail) : base(log, callIdFactory, factoryId,
				false, client, metadata)
			{
				m_Detail = detail;
			}

			protected override void CallImpl(CancellationToken cancelToken, CancellationToken _)
			{
				FireCallStarted("Unary");

				AsyncUnaryCall<HelloResponse> asyncCall = Client.helloAsync(new HelloRequest()
				{
					Text = "World" + (m_Detail != null ? $" [{m_Detail}]" : "")
				}, Metadata, cancellationToken: cancelToken);

				void PrintStatus()
				{
					Metadata metadata = asyncCall.GetTrailers();
					WriteLog($"Trailers[{metadata.Count}]: {MetadataToString(metadata)}");
					WriteLog($"Status: {asyncCall.GetStatus()}");
				}

				asyncCall.ResponseHeadersAsync.ContinueWith(ContinuationWithHeaders, cancelToken);
				asyncCall.ResponseAsync.ContinueWith(ContinuationWithResponse, cancelToken);

				// If we have no response we still complete the call here
				asyncCall.GetAwaiter().OnCompleted(() => FireCallCompleted(PrintStatus));
			}
		}

		class ServerStreamingGrpcTestCall : GrpcTestCall
		{
			public ServerStreamingGrpcTestCall(GRPCExampleLogger log, GrpcTestCallIdFactory callIdFactory, char factoryId,
				HelloWorldService.HelloWorldServiceClient client, Metadata metadata) : base(log, callIdFactory, factoryId, true, client, metadata)
			{
			}

			protected override void CallImpl(CancellationToken cancelToken, CancellationToken receiveCancelToken)
			{
				FireCallStarted("ServerStreaming");

				AsyncServerStreamingCall<HelloResponse> asyncCall = Client.helloServer(new HelloRequest()
				{
					Text = "World"
				}, Metadata, cancellationToken: cancelToken);

				void PrintStatus()
				{
					Metadata metadata = asyncCall.GetTrailers();
					WriteLog($"Trailers[{metadata.Count}]: {MetadataToString(metadata)}");
					WriteLog($"Status: {asyncCall.GetStatus()}");
				}

				asyncCall.ResponseHeadersAsync.ContinueWith(ContinuationWithHeaders, cancelToken);

				ProcessServerResponses(asyncCall.ResponseStream, receiveCancelToken, PrintStatus);
			}
		}

		class ClientStreamingGrpcTestCall : GrpcTestCall
		{
			public ClientStreamingGrpcTestCall(GRPCExampleLogger log, GrpcTestCallIdFactory callIdFactory, char factoryId,
				HelloWorldService.HelloWorldServiceClient client, Metadata metadata) : base(log, callIdFactory, factoryId, false, client, metadata)
			{
			}

			protected override void CallImpl(CancellationToken cancelToken, CancellationToken _)
			{
				FireCallStarted("ClientStreaming");

				AsyncClientStreamingCall<HelloRequest, HelloResponse> asyncCall = Client.helloClient(Metadata, cancellationToken: cancelToken);

				void PrintStatus()
				{
					Metadata metadata = asyncCall.GetTrailers();
					WriteLog($"Trailers[{metadata.Count}]: {MetadataToString(metadata)}");
					WriteLog($"Status: {asyncCall.GetStatus()}");
				}

				asyncCall.ResponseHeadersAsync.ContinueWith(ContinuationWithHeaders, cancelToken);
				asyncCall.ResponseAsync.ContinueWith(ContinuationWithResponse, cancelToken);

				SendClientRequests(asyncCall.RequestStream);

				// If we have no response we still complete the call here
				asyncCall.GetAwaiter().OnCompleted(() => FireCallCompleted(PrintStatus));
			}
		}

		class BiStreamingGrpcTestCall : GrpcTestCall
		{
			public BiStreamingGrpcTestCall(GRPCExampleLogger log, GrpcTestCallIdFactory callIdFactory, char factoryId,
				HelloWorldService.HelloWorldServiceClient client, Metadata metadata) : base(log, callIdFactory, factoryId, true, client, metadata)
			{
			}

			protected override void CallImpl(CancellationToken cancelToken, CancellationToken receiveCancelToken)
			{
				FireCallStarted("ClientStreaming");

				AsyncDuplexStreamingCall<HelloRequest, HelloResponse> asyncCall = Client.helloBoth(Metadata, cancellationToken: cancelToken);

				void PrintStatus()
				{
					Metadata metadata = asyncCall.GetTrailers();
					WriteLog($"Trailers[{metadata.Count}]: {MetadataToString(metadata)}");
					WriteLog($"Status: {asyncCall.GetStatus()}");
				}

				asyncCall.ResponseHeadersAsync.ContinueWith(ContinuationWithHeaders, cancelToken);

				SendClientRequests(asyncCall.RequestStream);

				ProcessServerResponses(asyncCall.ResponseStream, receiveCancelToken, PrintStatus);
			}
		}

		class GrpcTestCallIdFactory
		{
			static Color Hex2Color(string hex)
			{
				return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.red;
			}

			static readonly Color[] ColorList =
			{
				Hex2Color("#db5f57"), Hex2Color("#dbc257"), Hex2Color("#91db57"), Hex2Color("#57db80"),
				Hex2Color("#57d3db"), Hex2Color("#5770db"), Hex2Color("#a157db"), Hex2Color("#db57b2")
			};

			int m_Inc = 0;
			int m_ColorPointer = 0;

			List<GrpcTestCall> m_ActiveCalls = new List<GrpcTestCall>();

			public (int, Color) CreateNext(GrpcTestCall call)
			{
				Interlocked.Increment(ref m_Inc);

				// Find unique color for this call
				Color c = ColorList[m_ColorPointer];
				m_ColorPointer++;
				if (m_ColorPointer >= ColorList.Length)
				{
					m_ColorPointer = 0;
				}

				// Add call to active list
				m_ActiveCalls.Add(call);

				return (m_Inc, c);
			}

			// Keep track of active calls
			public IEnumerable<GrpcTestCall> GetActiveCalls()
			{
				return m_ActiveCalls;
			}

			public void Completed(GrpcTestCall call)
			{
				m_ActiveCalls.Remove(call);
			}

			// Timer utility callback to make delayed calls possible
			public event Action OnTimer;

			float m_Time = 0;

			public void Update()
			{
				if (Time.realtimeSinceStartup >= m_Time)
				{
					m_Time = Time.realtimeSinceStartup + 1.0f; // 1 sec delay
					OnTimer?.Invoke();
				}
			}
		}

		class GrpcTestCallFactory
		{
			public string ServerAddressInput = "https://127.0.0.1:50051";

			readonly GrpcTestCallIdFactory m_IdFactory;
			readonly char m_FactoryId;
			readonly GRPCExampleLogger m_Logger;

			GrpcChannel m_Channel;
			HelloWorldService.HelloWorldServiceClient m_Client;

			public GrpcTestCallFactory(GrpcTestCallIdFactory idFactory, char factoryId, GRPCExampleLogger logger)
			{
				m_IdFactory = idFactory;
				m_FactoryId = factoryId;
				m_Logger = logger;
			}

			public GrpcTestCall CreateUnary(Metadata metadata, string detail = null)
			{
				EnsureClientConnection();
				return new UnaryGrpcTestCall(m_Logger, m_IdFactory, m_FactoryId, m_Client, metadata, detail);
			}

			public GrpcTestCall CreateServerStreaming(Metadata metadata)
			{
				EnsureClientConnection();
				return new ServerStreamingGrpcTestCall(m_Logger, m_IdFactory, m_FactoryId, m_Client, metadata);
			}

			public GrpcTestCall CreateClientStreaming(Metadata metadata)
			{
				EnsureClientConnection();
				return new ClientStreamingGrpcTestCall(m_Logger, m_IdFactory, m_FactoryId, m_Client, metadata);
			}

			public GrpcTestCall CreateBiStreaming(Metadata metadata)
			{
				EnsureClientConnection();
				return new BiStreamingGrpcTestCall(m_Logger, m_IdFactory, m_FactoryId, m_Client, metadata);
			}

			void EnsureClientConnection()
			{
				// Setup GRPC channel
				GRPCBestHttpHandler httpHandler = new GRPCBestHttpHandler();
				m_Channel = GrpcChannel.ForAddress(ServerAddressInput, new GrpcChannelOptions
				{
					HttpHandler = httpHandler
				});

				m_Client = new HelloWorldService.HelloWorldServiceClient(m_Channel);
			}
		}

		abstract class GrpcTestCall
		{
			protected readonly GRPCExampleLogger Log;
			protected readonly GrpcTestCallIdFactory CallIdFactory;
			public readonly char FactoryId;
			public readonly int Inc;
			public readonly Color Color;

			protected readonly bool UsesReceive;
			CancellationTokenSource m_CancelToken;
			CancellationTokenSource m_ReceiveCancelToken;

			protected readonly HelloWorldService.HelloWorldServiceClient Client;
			protected readonly Metadata Metadata;

			public GrpcTestCall(GRPCExampleLogger log, GrpcTestCallIdFactory callIdFactory, char factoryId, bool usesReceive,
				HelloWorldService.HelloWorldServiceClient client, Metadata metadata)
			{
				Log = log;
				FactoryId = factoryId;
				UsesReceive = usesReceive;
				CallIdFactory = callIdFactory;
				(Inc, Color) = callIdFactory.CreateNext(this);
				Client = client;
				Metadata = metadata;
			}

			protected abstract void CallImpl(CancellationToken cancelToken, CancellationToken receiveCancelToken);

			public void Call()
			{
				m_CancelToken = new CancellationTokenSource();
				m_ReceiveCancelToken = new CancellationTokenSource();
				CallImpl(m_CancelToken.Token, m_ReceiveCancelToken.Token);
			}

			public void Cancel()
			{
				m_CancelToken.Cancel();
			}

			public void CancelReceive()
			{
				m_ReceiveCancelToken.Cancel();
			}

			public bool SupportsReceiveCancellation()
			{
				return UsesReceive;
			}

			protected void WriteLog(string msg)
			{
				string colorHex = ColorUtility.ToHtmlStringRGBA(Color);
				Log.WriteLogOutput($"<color=#{colorHex}>[{FactoryId}|{Inc}] {msg}</color>");
			}

			protected void FireCallStarted(string callName)
			{
				WriteLog($"Call {callName} <b>started</b>");
			}

			protected void FireCallException(Exception ex)
			{
				WriteLog($"Call exception: {ex.Message}");
			}

			protected void FireCallCompleted(Action printStatusFunc)
			{
				// If call did not complete we can not access status or trailers
				try
				{
					printStatusFunc();
				}
				catch (Exception e)
				{
					// ignored
					_ = e;
				}

				WriteLog("Call <b>completed</b>");
				CallIdFactory.Completed(this);
			}

			protected void ContinuationWithHeaders(Task<Metadata> task)
			{
				Metadata metadata = task.Result;
				WriteLog($"Metadata[{metadata.Count}]: {string.Join(",", metadata)}");
			}

			protected void ContinuationWithResponse(Task<HelloResponse> task)
			{
				if (task.Exception != null)
				{
					WriteLog($"Error: {task.Exception.Message}");
				}
				else
				{
					HelloResponse result = task.Result;
					WriteLog($"Received: {result.Text}");
				}
			}

			protected async void ProcessServerResponses(IAsyncStreamReader<HelloResponse> responseStream,
				CancellationToken receiveCancelToken, Action printStatusFunc)
			{
				try
				{
					int idx = 0;
					while (await responseStream.MoveNext(receiveCancelToken))
					{
						HelloResponse response = responseStream.Current;
						WriteLog($"Received({idx++}): {response.Text}");
					}
				}
				catch (Exception e)
				{
					FireCallException(e);
				}
				finally
				{
					FireCallCompleted(printStatusFunc);
				}
			}

			protected void SendClientRequests(IClientStreamWriter<HelloRequest> requestStream)
			{
				int idx = 0;

				async void OnTimerFunc()
				{
					try
					{
						if (idx < 5)
						{
							HelloRequest msg = new HelloRequest() { Text = $"World {idx}" };
							WriteLog($"Send({idx}): {msg.Text}");
							await requestStream.WriteAsync(msg);
							idx += 1;
						}
						else
						{
							await requestStream.CompleteAsync();
							CallIdFactory.OnTimer -= OnTimerFunc;
						}
					}
					catch (Exception e)
					{
						WriteLog($"Error Sending: {e.Message}");
						CallIdFactory.OnTimer -= OnTimerFunc;
					}
				}

				CallIdFactory.OnTimer += OnTimerFunc;
			}
		}

		void GUITestButtonArea(GrpcTestCallFactory callFactory)
		{
			GUILayout.BeginVertical();
			if (GUILayout.Button("Test Unary call"))
			{
				RunTestCallUnary(callFactory, AuthMetadata);
			}

			if (GUILayout.Button("Test Server-Streaming Call"))
			{
				RunTestCallServerStreaming(callFactory);
			}

			if (GUILayout.Button("Test Client-Streaming Call"))
			{
				RunTestCallClientStreaming(callFactory);
			}

			if (GUILayout.Button("Test Bidirectional-Streaming Call"))
			{
				RunTestCallBiStreaming(callFactory);
			}

			if (GUILayout.Button("Test Unary call (throw ex after resp)"))
			{
				RunTestCallUnary(callFactory, AuthMetadata, "exception-after");
			}

			if (GUILayout.Button("Test Unary call (throw ex before resp)"))
			{
				RunTestCallUnary(callFactory, AuthMetadata, "exception-before");
			}

			if (GUILayout.Button("Test Unary call (throw ex after with meta)"))
			{
				RunTestCallUnary(callFactory, AuthMetadata, "exception-after-meta");
			}

			if (GUILayout.Button("Test Unary call (throw ex before with meta)"))
			{
				RunTestCallUnary(callFactory, AuthMetadata, "exception-before-meta");
			}

			if (GUILayout.Button("Test Unary call (no response)"))
			{
				RunTestCallUnary(callFactory, AuthMetadata, "no-response");
			}

			if (GUILayout.Button("Test Unary call (no auth token)"))
			{
				RunTestCallUnary(callFactory);
			}

			GUILayout.EndVertical();
		}

		void GUICancelButtonArea(GrpcTestCallIdFactory callFactory)
		{
			GUILayout.BeginVertical();
			foreach (GrpcTestCall call in callFactory.GetActiveCalls()) // FIXME: modified while reading...
			{
				string colorHex = ColorUtility.ToHtmlStringRGBA(call.Color);

				if (call.SupportsReceiveCancellation())
				{
					GUILayout.BeginHorizontal();
				}

				if (GUILayout.Button($"<color=#{colorHex}>[{call.FactoryId}|{call.Inc}] Cancel</color>"))
				{
					call.Cancel();
				}

				if (call.SupportsReceiveCancellation())
				{
					if (GUILayout.Button($"<color=#{colorHex}>[{call.FactoryId}|{call.Inc}] Cancel Receive</color>"))
					{
						call.CancelReceive();
					}

					GUILayout.EndHorizontal();
				}
			}

			GUILayout.EndVertical();
		}

		void OnGUI()
		{
			int pad = 10;
			int screenWidth = Screen.width - pad * 2;

			m_CallFactoryA.ServerAddressInput = GUI.TextField(new Rect(pad, pad, screenWidth * 0.25f, 20), m_CallFactoryA.ServerAddressInput, 400);
			m_CallFactoryB.ServerAddressInput = GUI.TextField(new Rect(pad + screenWidth * 0.25f, pad, screenWidth * 0.25f, 20), m_CallFactoryB.ServerAddressInput, 400);

			Rect buttonAreaA = new Rect(pad, pad + 24, screenWidth * 0.25f, Screen.height);
			GUILayout.BeginArea(buttonAreaA);
			GUITestButtonArea(m_CallFactoryA);
			GUILayout.EndArea();

			Rect buttonAreaB = new Rect(pad + screenWidth * 0.25f, pad + 24, screenWidth * 0.25f, Screen.height);
			GUILayout.BeginArea(buttonAreaB);
			GUITestButtonArea(m_CallFactoryB);
			GUILayout.EndArea();

			Rect cancelArea = new Rect(pad + screenWidth * 0.25f * 3, pad, screenWidth * 0.25f, Screen.height);
			GUILayout.BeginArea(cancelArea);
			GUICancelButtonArea(m_CallIdFactory);
			GUILayout.EndArea();
		}
	}
}
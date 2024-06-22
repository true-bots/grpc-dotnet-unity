using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BestHTTP;

namespace BestHTTP.Examples.HTTP
{
	public class StreamingSample : Helpers.SampleBase
	{
#pragma warning disable 0649

		[Tooltip("The url of the resource to download")] [SerializeField]
		protected string _downloadPath = "/test100mb.dat";

		[Header("Streaming Setup")] [SerializeField]
		protected RectTransform _streamingSetupRoot;

		[SerializeField] protected Slider _fragmentSizeSlider;

		[SerializeField] protected Text _fragmentSizeText;

		[SerializeField] protected Toggle _disableCacheToggle;

		[Header("Reporting")] [SerializeField] protected RectTransform _reportingRoot;

		[SerializeField] protected Slider _downloadProgressSlider;

		[SerializeField] protected Text _downloadProgressText;

		[SerializeField] protected Slider _processedDataSlider;

		[SerializeField] protected Text _processedDataText;

		[SerializeField] protected Text _statusText;

		[SerializeField] protected Button _startDownload;

		[SerializeField] protected Button _cancelDownload;

#pragma warning restore

		/// <summary>
		/// Cached request to be able to abort it
		/// </summary>
		protected HTTPRequest request;

		/// <summary>
		/// Download(processing) progress. Its range is between [0..1]
		/// </summary>
		protected float progress;

		/// <summary>
		/// The fragment size that we will set to the request
		/// </summary>
		protected int fragmentSize = HTTPResponse.MinReadBufferSize;

		protected virtual long DownloadLength { get; set; }

		protected virtual long ProcessedBytes { get; set; }

		protected override void Start()
		{
			base.Start();

			_streamingSetupRoot.gameObject.SetActive(true);
			_reportingRoot.gameObject.SetActive(false);

			_startDownload.interactable = true;
			_cancelDownload.interactable = false;

			_fragmentSizeSlider.value = (1024 * 1024 - HTTPResponse.MinReadBufferSize) / 1024;
			_fragmentSizeText.text = GUIHelper.GetBytesStr(1024 * 1024, 1);
		}

		protected void OnDestroy()
		{
			// Stop the download if we are leaving this example
			if (request != null && request.State < HTTPRequestStates.Finished)
			{
				request.OnDownloadProgress = null;
				request.Callback = null;
				request.Abort();
			}
		}

		public void OnFragmentSizeSliderChanged(float value)
		{
			fragmentSize = HTTPResponse.MinReadBufferSize + (int)value * 1024;
			_fragmentSizeText.text = GUIHelper.GetBytesStr(fragmentSize, 1);
		}

		public void Cancel()
		{
			if (request != null)
			{
				request.Abort();
			}
		}

		protected virtual void SetupRequest()
		{
			request = new HTTPRequest(new Uri(sampleSelector.BaseURL + _downloadPath), OnRequestFinished);

#if !BESTHTTP_DISABLE_CACHING
			// If we are writing our own file set it to true(disable), so don't duplicate it on the file-system
			request.DisableCache = _disableCacheToggle.isOn;
#endif

			request.StreamFragmentSize = fragmentSize;

			request.Tag = DateTime.Now;

			request.OnHeadersReceived += OnHeadersReceived;
			request.OnDownloadProgress += OnDownloadProgress;
			request.OnStreamingData += OnDataDownloaded;
		}

		public virtual void StartStreaming()
		{
			SetupRequest();

			// Start Processing the request
			request.Send();

			_statusText.text = "Download started!";

			// UI
			_streamingSetupRoot.gameObject.SetActive(false);
			_reportingRoot.gameObject.SetActive(true);

			_startDownload.interactable = false;
			_cancelDownload.interactable = true;

			ResetProcessedValues();
		}

		void OnHeadersReceived(HTTPRequest req, HTTPResponse resp, Dictionary<string, List<string>> newHeaders)
		{
			HTTPRange range = resp.GetRange();
			if (range != null)
			{
				DownloadLength = range.ContentLength;
			}
			else
			{
				string contentLength = resp.GetFirstHeaderValue("content-length");
				if (contentLength != null)
				{
					long length = 0;
					if (long.TryParse(contentLength, out length))
					{
						DownloadLength = length;
					}
				}
			}
		}

		protected virtual void OnRequestFinished(HTTPRequest req, HTTPResponse resp)
		{
			switch (req.State)
			{
				// The request finished without any problem.
				case HTTPRequestStates.Finished:
					if (resp.IsSuccess)
					{
						DateTime downloadStarted = (DateTime)req.Tag;
						TimeSpan diff = DateTime.Now - downloadStarted;

						_statusText.text = string.Format("Streaming finished in {0:N0}ms", diff.TotalMilliseconds);
					}
					else
					{
						_statusText.text = string.Format("Request finished Successfully, but the server sent an error. Status Code: {0}-{1} Message: {2}",
							resp.StatusCode,
							resp.Message,
							resp.DataAsText);
						Debug.LogWarning(_statusText.text);

						request = null;
					}

					break;

				// The request finished with an unexpected error. The request's Exception property may contain more info about the error.
				case HTTPRequestStates.Error:
					_statusText.text = "Request Finished with Error! " +
					                   (req.Exception != null ? req.Exception.Message + "\n" + req.Exception.StackTrace : "No Exception");
					Debug.LogError(_statusText.text);

					request = null;
					break;

				// The request aborted, initiated by the user.
				case HTTPRequestStates.Aborted:
					_statusText.text = "Request Aborted!";
					Debug.LogWarning(_statusText.text);

					request = null;
					break;

				// Connecting to the server is timed out.
				case HTTPRequestStates.ConnectionTimedOut:
					_statusText.text = "Connection Timed Out!";
					Debug.LogError(_statusText.text);

					request = null;
					break;

				// The request didn't finished in the given time.
				case HTTPRequestStates.TimedOut:
					_statusText.text = "Processing the request Timed Out!";
					Debug.LogError(_statusText.text);

					request = null;
					break;
			}

			// UI

			_streamingSetupRoot.gameObject.SetActive(true);
			_reportingRoot.gameObject.SetActive(false);

			_startDownload.interactable = true;
			_cancelDownload.interactable = false;
			request = null;
		}

		protected virtual void OnDownloadProgress(HTTPRequest originalRequest, long downloaded, long downloadLength)
		{
			double downloadPercent = downloaded / (double)downloadLength * 100;
			_downloadProgressSlider.value = (float)downloadPercent;
			_downloadProgressText.text = string.Format("{0:F1}%", downloadPercent);
		}

		protected virtual bool OnDataDownloaded(HTTPRequest request, HTTPResponse response, byte[] dataFragment, int dataFragmentLength)
		{
			ProcessedBytes += dataFragmentLength;
			SetDataProcessedUI(ProcessedBytes, DownloadLength);

			// Use downloaded data

			// Return true if dataFrament is processed so the plugin can recycle the byte[]
			return true;
		}

		protected void SetDataProcessedUI(long processed, long length)
		{
			float processedPercent = processed / (float)length * 100f;

			_processedDataSlider.value = processedPercent;
			_processedDataText.text = GUIHelper.GetBytesStr(processed, 0);
		}

		protected virtual void ResetProcessedValues()
		{
			ProcessedBytes = 0;
			DownloadLength = 0;

			SetDataProcessedUI(ProcessedBytes, DownloadLength);
		}
	}
}
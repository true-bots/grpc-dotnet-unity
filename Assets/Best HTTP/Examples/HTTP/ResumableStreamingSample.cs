using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BestHTTP;

namespace BestHTTP.Examples.HTTP
{
	public sealed class ResumableStreamingSample : StreamingSample
	{
		const string ProcessedBytesKey = "ProcessedBytes";
		const string DownloadLengthKey = "DownloadLength";

		/// <summary>
		/// Expected content length
		/// </summary>
		protected override long DownloadLength
		{
			get { return PlayerPrefs.GetInt(_downloadPath + DownloadLengthKey); }
			set { PlayerPrefs.SetInt(_downloadPath + DownloadLengthKey, (int)value); }
		}

		/// <summary>
		/// Total processed bytes
		/// </summary>
		protected override long ProcessedBytes
		{
			get { return PlayerPrefs.GetInt(_downloadPath + ProcessedBytesKey, 0); }
			set { PlayerPrefs.SetInt(_downloadPath + ProcessedBytesKey, (int)value); }
		}

		long downloadStartedAt = 0;

		protected override void Start()
		{
			base.Start();

			// If we have a non-finished download, set the progress to the value where we left it
			float progress = GetSavedProgress();
			if (progress > 0.0f)
			{
				_downloadProgressSlider.value = progress;
				_statusText.text = progress.ToString("F2");
			}
		}

		protected override void SetupRequest()
		{
			base.SetupRequest();

			// Are there any progress, that we can continue?
			downloadStartedAt = ProcessedBytes;

			if (downloadStartedAt > 0)
			{
				// Set the range header
				request.SetRangeHeader(downloadStartedAt);
			}
			else
				// This is a new request
			{
				DeleteKeys();
			}
		}

		protected override void OnRequestFinished(HTTPRequest req, HTTPResponse resp)
		{
			base.OnRequestFinished(req, resp);

			if (req.State == HTTPRequestStates.Finished && resp.IsSuccess)
			{
				DeleteKeys();
			}
		}

		protected override void OnDownloadProgress(HTTPRequest originalRequest, long downloaded, long downloadLength)
		{
			double downloadPercent = (downloadStartedAt + downloaded) / (double)DownloadLength * 100;

			_downloadProgressSlider.value = (float)downloadPercent;
			_downloadProgressText.text = string.Format("{0:F1}%", downloadPercent);
		}

		protected override void ResetProcessedValues()
		{
			SetDataProcessedUI(ProcessedBytes, DownloadLength);
		}

		float GetSavedProgress()
		{
			long down = ProcessedBytes;
			long length = DownloadLength;

			if (down > 0 && length > 0)
			{
				return down / (float)length * 100f;
			}

			return -1;
		}

		void DeleteKeys()
		{
			PlayerPrefs.DeleteKey(_downloadPath + ProcessedBytesKey);
			PlayerPrefs.DeleteKey(_downloadPath + DownloadLengthKey);
			PlayerPrefs.Save();
		}
	}
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BestHTTP.Extensions;
using BestHTTP.PlatformSupport.Memory;

namespace BestHTTP
{
	/// <summary>
	/// Stream based implementation of the multipart/form-data Content-Type. Using this class reading a whole file into memory can be avoided.
	/// This implementation expects that all streams has a final, accessible Length.
	/// </summary>
	public sealed class MultipartFormDataStream : Stream
	{
		public override bool CanRead
		{
			get { return true; }
		}

		public override bool CanSeek
		{
			get { return false; }
		}

		public override bool CanWrite
		{
			get { return false; }
		}

		public override long Length
		{
			get
			{
				// multipart/form-data requires a leading boundary that we can add when all streams are added.
				// This final preparation could be user initiated, but we can do it automatically too when the HTTPRequest
				// first access the Length property.
				if (!prepared)
				{
					prepared = true;
					Prepare();
				}

				return _length;
			}
		}

		long _length;

		public override long Position { get; set; }

		/// <summary>
		/// A random boundary generated in the constructor.
		/// </summary>
		string boundary;

		Queue<StreamList> fields = new Queue<StreamList>(1);
		StreamList currentField;
		bool prepared;

		public MultipartFormDataStream(HTTPRequest request)
		{
			boundary = "BestHTTP_MultipartFormDataStream_" + GetHashCode().ToString("X2");

			request.SetHeader("Content-Type", "multipart/form-data; boundary=" + boundary);
			request.UploadStream = this;
			request.UseUploadStreamLength = true;
		}

		public void AddField(string fieldName, string value)
		{
			AddField(fieldName, value, System.Text.Encoding.UTF8);
		}

		public void AddField(string fieldName, string value, System.Text.Encoding encoding)
		{
			Encoding enc = encoding ?? System.Text.Encoding.UTF8;
			int byteCount = enc.GetByteCount(value);
			byte[] buffer = BufferPool.Get(byteCount, true);
			BufferPoolMemoryStream stream = new BufferPoolMemoryStream();

			enc.GetBytes(value, 0, value.Length, buffer, 0);

			stream.Write(buffer, 0, byteCount);

			stream.Position = 0;

			string mime = encoding != null ? "text/plain; charset=" + encoding.WebName : null;
			AddStreamField(stream, fieldName, null, mime);
		}

		public void AddStreamField(Stream stream, string fieldName)
		{
			AddStreamField(stream, fieldName, null, null);
		}

		public void AddStreamField(Stream stream, string fieldName, string fileName)
		{
			AddStreamField(stream, fieldName, fileName, null);
		}

		public void AddStreamField(Stream stream, string fieldName, string fileName, string mimeType)
		{
			BufferPoolMemoryStream header = new BufferPoolMemoryStream();
			header.WriteLine("--" + boundary);
			header.WriteLine("Content-Disposition: form-data; name=\"" + fieldName + "\"" +
			                 (!string.IsNullOrEmpty(fileName) ? "; filename=\"" + fileName + "\"" : string.Empty));
			// Set up Content-Type head for the form.
			if (!string.IsNullOrEmpty(mimeType))
			{
				header.WriteLine("Content-Type: " + mimeType);
			}

			//header.WriteLine("Content-Length: " + stream.Length.ToString());
			header.WriteLine();
			header.Position = 0;

			BufferPoolMemoryStream footer = new BufferPoolMemoryStream();
			footer.Write(HTTPRequest.EOL, 0, HTTPRequest.EOL.Length);
			footer.Position = 0;

			// all wrapped streams going to be disposed by the StreamList wrapper.
			StreamList wrapper = new StreamList(header, stream, footer);

			try
			{
				if (_length >= 0)
				{
					_length += wrapper.Length;
				}
			}
			catch
			{
				_length = -1;
			}

			fields.Enqueue(wrapper);
		}

		/// <summary>
		/// Adds the final boundary.
		/// </summary>
		void Prepare()
		{
			BufferPoolMemoryStream boundaryStream = new BufferPoolMemoryStream();
			boundaryStream.WriteLine("--" + boundary + "--");
			boundaryStream.Position = 0;

			fields.Enqueue(new StreamList(boundaryStream));

			if (_length >= 0)
			{
				_length += boundaryStream.Length;
			}
		}

		public override int Read(byte[] buffer, int offset, int length)
		{
			if (currentField == null && fields.Count == 0)
			{
				return -1;
			}

			if (currentField == null && fields.Count > 0)
			{
				currentField = fields.Dequeue();
			}

			int readCount = 0;

			do
			{
				// read from the current stream
				int count = currentField.Read(buffer, offset + readCount, length - readCount);

				if (count > 0)
				{
					readCount += count;
				}
				else
				{
					// if the current field's stream is empty, go for the next one.

					// dispose the current one first
					try
					{
						currentField.Dispose();
					}
					catch
					{
					}

					// no more fields/streams? exit
					if (fields.Count == 0)
					{
						break;
					}

					// grab the next one
					currentField = fields.Dequeue();
				}

				// exit when we reach the length goal, or there's no more streams to read from
			} while (readCount < length && fields.Count > 0);

			return readCount;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotImplementedException();
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public override void Flush()
		{
		}
	}
}
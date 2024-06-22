#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.IO;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.Encoders;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.IO.Pem
{
	/**
	* A generic PEM writer, based on RFC 1421
	*/
	public class PemWriter
		: IDisposable
	{
		const int LineLength = 64;

		readonly TextWriter writer;
		readonly int nlLength;
		char[] buf = new char[LineLength];

		/**
		 * Base constructor.
		 *
		 * @param out output stream to use.
		 */
		public PemWriter(TextWriter writer)
		{
			this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
			nlLength = Environment.NewLine.Length;
		}

		#region IDisposable

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				writer.Dispose();
			}
		}

		#endregion

		public TextWriter Writer
		{
			get { return writer; }
		}

		/**
		 * Return the number of bytes or characters required to contain the
		 * passed in object if it is PEM encoded.
		 *
		 * @param obj pem object to be output
		 * @return an estimate of the number of bytes
		 */
		public int GetOutputSize(PemObject obj)
		{
			// BEGIN and END boundaries.
			int size = 2 * (obj.Type.Length + 10 + nlLength) + 6 + 4;

			if (obj.Headers.Count > 0)
			{
				foreach (PemHeader header in obj.Headers)
				{
					size += header.Name.Length + ": ".Length + header.Value.Length + nlLength;
				}

				size += nlLength;
			}

			// base64 encoding
			int dataLen = (obj.Content.Length + 2) / 3 * 4;

			size += dataLen + (dataLen + LineLength - 1) / LineLength * nlLength;

			return size;
		}

		public void WriteObject(PemObjectGenerator objGen)
		{
			PemObject obj = objGen.Generate();

			WritePreEncapsulationBoundary(obj.Type);

			if (obj.Headers.Count > 0)
			{
				foreach (PemHeader header in obj.Headers)
				{
					writer.Write(header.Name);
					writer.Write(": ");
					writer.WriteLine(header.Value);
				}

				writer.WriteLine();
			}

			WriteEncoded(obj.Content);
			WritePostEncapsulationBoundary(obj.Type);
		}

		void WriteEncoded(byte[] bytes)
		{
			bytes = Base64.Encode(bytes);

			for (int i = 0; i < bytes.Length; i += buf.Length)
			{
				int index = 0;
				while (index != buf.Length)
				{
					if (i + index >= bytes.Length)
					{
						break;
					}

					buf[index] = (char)bytes[i + index];
					index++;
				}

				writer.WriteLine(buf, 0, index);
			}
		}

		void WritePreEncapsulationBoundary(string type)
		{
			writer.WriteLine("-----BEGIN " + type + "-----");
		}

		void WritePostEncapsulationBoundary(string type)
		{
			writer.WriteLine("-----END " + type + "-----");
		}
	}
}
#pragma warning restore
#endif
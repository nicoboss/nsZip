using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using LibHac.IO;

namespace nsZip.LibHacExtensions
{
	public static class SaveDeltaHeader
	{
		public static long Save(IStorage delta, FileStream writer, string foundBaseNCA)
		{
			Debug.Print(foundBaseNCA);
			if (delta.Length < 0x40)
			{
				throw new InvalidDataException("Delta file is too small.");
			}

			if (foundBaseNCA.Length > 255)
			{
				throw new IndexOutOfRangeException("Base NCA filename isn't allowed to be longer then 255 characters");
			}

			var Header = new DeltaFragmentHeader(new StorageFile(delta, OpenMode.Read));

			var reader = new FileReader(new StorageFile(delta, OpenMode.Read));
			reader.Position = 0;


			if (!foundBaseNCA.Contains(":") && Header.Magic != DeltaTools.Ndv0Magic)
			{
				writer.Write(DeltaTools.LCA3Macic, 0, DeltaTools.LCA3Macic.Length);
				writer.WriteByte((byte)foundBaseNCA.Length);
				writer.Write(ASCIIEncoding.ASCII.GetBytes(foundBaseNCA), 0, foundBaseNCA.Length);
				return 0;
			}

			if (Header.Magic == DeltaTools.Ndv0Magic)
			{
				var fragmentSize = Header.FragmentHeaderSize + Header.FragmentBodySize;
				//if (delta.Length < fragmentSize)
				//{
				//	throw new InvalidDataException(
				//		$"Delta file is smaller than the header indicates. (0x{fragmentSize} bytes)");
				//}

				var headerData = reader.ReadBytes((int) Header.FragmentHeaderSize);
				headerData[0] = 0x54; //T (NDV0 to TDV0)
				writer.Write(headerData, 0, (int) Header.FragmentHeaderSize);
			}
			else
			{
				writer.Write(Encoding.ASCII.GetBytes(DeltaTools.Cdv0Magic), 0, DeltaTools.Cdv0Magic.Length);
			}

			writer.WriteByte((byte) foundBaseNCA.Length);
			writer.Write(Encoding.ASCII.GetBytes(foundBaseNCA), 0, foundBaseNCA.Length);

			long offset = 0;
			while (reader.Position < delta.Length)
			{
				ReadSegmentHeader(reader, writer, out var size, out var seek);
				if (seek > 0)
				{
					offset += seek;
				}

				if (size > 0)
				{
					offset += size;
				}

				reader.Position += size;
				Debug.Print(reader.Position.ToString());
			}
			if (reader.Position == delta.Length)
			{
				return offset;
			}
			throw new InvalidDataException("Fragment file seems to be corrupted!");
		}

		private static void ReadSegmentHeader(FileReader reader, FileStream writer, out int size, out int seek)
		{
			var pos = reader.Position;
			var type = reader.ReadUInt8();
			var seekBytes = (type & 3) + 1;
			var sizeBytes = ((type >> 3) & 3) + 1;

			size = DeltaTools.ReadInt(reader, sizeBytes);
			seek = DeltaTools.ReadInt(reader, seekBytes);

			reader.Position = pos;
			var len = 1 + sizeBytes + seekBytes;
			writer.Write(reader.ReadBytes(len), 0, len);
		}
	}
}
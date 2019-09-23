using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using LibHac;
using LibHac.Fs;

namespace nsZip.LibHacExtensions
{
	public static class RecreateDelta
	{
		public static long Recreate(IStorage fragmentMeta, FileStream writer, IFileSystem newBaseFolderFs)
		{
			var Segments = new List<DeltaFragmentSegment>();
			if (fragmentMeta.GetSize() < 0x40)
			{
				throw new InvalidDataException("Delta file is too small.");
			}

			var magic = new byte[4];
			fragmentMeta.Read(magic, 0, 4, 0);
			var reader = new FileReader(new StorageFile(fragmentMeta, OpenMode.Read));

			if (Utils.ArraysEqual(magic, DeltaTools.LCA3Macic))
			{
				reader.Position = DeltaTools.LCA3Macic.Length;
				var linkedNcaFilenameSize = reader.ReadUInt8();
				var linkedNcafilename =
					Encoding.ASCII.GetString(reader.ReadBytes(DeltaTools.LCA3Macic.Length + 1, linkedNcaFilenameSize,
						true));
				
				var newLinkedFile = newBaseFolderFs.OpenFile(linkedNcafilename, OpenMode.Read).AsStream();
				newLinkedFile.CopyStream(writer, newLinkedFile.Length);
				return reader.Position;
			}

			var Header = new DeltaFragmentHeader(new StorageFile(fragmentMeta, OpenMode.Read));

			reader.Position = 0;
			if (Header.Magic == DeltaTools.Tdv0Magic)
			{
				var headerData = reader.ReadBytes(0, (int) Header.FragmentHeaderSize, true);
				headerData[0] = 0x4E; //N (TDV0 to NDV0)
				writer.Write(headerData, 0, (int) Header.FragmentHeaderSize);
			}
			else if (Header.Magic == DeltaTools.Cdv0Magic)
			{
				reader.Position = 4;
			}
			else
			{
				throw new InvalidDataException("TDV0/CDV0 magic value is missing.");
			}

			var pos = reader.Position;
			var baseNcaFilenameSize = reader.ReadUInt8();
			var filenameOffset =
				Encoding.ASCII.GetString(reader.ReadBytes(pos + 1, baseNcaFilenameSize, true)).Split(':');

			var newBaseFile = newBaseFolderFs.OpenFile(filenameOffset[0], OpenMode.Read).AsStream();

			long offset = 0;
			var endOffset = Header.NewSize;
			if (filenameOffset.Length > 2)
			{
				offset = long.Parse(filenameOffset[1], NumberStyles.HexNumber);
				endOffset = long.Parse(filenameOffset[2], NumberStyles.HexNumber);
			}

			const int maxBS = 10485760; //10 MB
			int bs;
			var FragmentBlock = new byte[maxBS];

			while (offset < endOffset)
			{
				Console.WriteLine($"ReadSegmentHeader on {offset}");
				ReadSegmentHeader(reader, writer, out var size, out var seek);
				Console.WriteLine($"size: {size}, seek: {seek}, readerPos: {reader.Position}, writerPos: {writer.Position}");
				if (seek > 0)
				{
					var segment = new DeltaFragmentSegment
					{
						SourceOffset = offset,
						Size = seek,
						IsInOriginal = true
					};

					Segments.Add(segment);
					offset += seek;
				}

				if (size > 0)
				{
					var segment = new DeltaFragmentSegment
					{
						SourceOffset = reader.Position,
						Size = size,
						IsInOriginal = false
					};

					newBaseFile.Position = offset;
					var fragmentOffsetEnd = offset + segment.Size;
					while (newBaseFile.Position < fragmentOffsetEnd)
					{
						bs = (int) Math.Min(fragmentOffsetEnd - newBaseFile.Position, maxBS);
						newBaseFile.Read(FragmentBlock, 0, bs);
						writer.Write(FragmentBlock, 0, bs);
					}

					Segments.Add(segment);
					offset += size;
				}
			}

			newBaseFile.Dispose();
			return reader.Position;
		}

		private static void ReadSegmentHeader(FileReader reader, FileStream writer, out long size, out long seek)
		{
			var pos = reader.Position;
			var type = reader.ReadUInt8();

			Console.WriteLine($"type: {type}");
			var seekBytes = (type & 7) + 1;
			var sizeBytes = (type >> 3) + 1;

			size = DeltaTools.ReadInt(reader, sizeBytes);
			seek = DeltaTools.ReadInt(reader, seekBytes);

			reader.Position = pos;
			var len = 1 + sizeBytes + seekBytes;
			writer.Write(reader.ReadBytes(len), 0, len);
		}
	}
}
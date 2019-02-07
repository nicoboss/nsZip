﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using LibHac;
using LibHac.IO;

namespace nsZip.LibHacExtensions
{
	public static class RecreateDelta
	{
		public static long Recreate(IStorage fragmentMeta, FileStream writer, string newBaseFolderPath)
		{
			var Segments = new List<DeltaFragmentSegment>();
			if (fragmentMeta.Length < 0x40)
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
				var newLinkedFile = File.Open($"{newBaseFolderPath}/{linkedNcafilename}", FileMode.Open);
				newLinkedFile.CopyStream(writer, newLinkedFile.Length);
				return reader.Position;
			}

			var Header = new DeltaFragmentHeader(new StorageFile(fragmentMeta, OpenMode.Read));
			if (Header.Magic != DeltaTools.Tdv0Magic)
			{
				throw new InvalidDataException("TDV0 magic value is missing.");
			}

			reader.Position = 0;
			var headerData = reader.ReadBytes(0, (int) Header.FragmentHeaderSize, true);
			headerData[0] = 0x4E; //N (TDV0 to NDV0)
			writer.Write(headerData, 0, (int) Header.FragmentHeaderSize);

			var baseNcaFilenameSize = reader.ReadUInt8();
			var filenameOffset =
				Encoding.ASCII.GetString(reader.ReadBytes(Header.FragmentHeaderSize + 1, baseNcaFilenameSize, true)).Split(':');

			var newBaseFile = File.Open($"{newBaseFolderPath}/{filenameOffset[0]}", FileMode.Open);

			long offset = 0;
			if (filenameOffset.Length > 1)
			{
				offset = long.Parse(filenameOffset[1], NumberStyles.HexNumber);
			}

			const int maxBS = 10485760; //10 MB
			int bs;
			var FragmentBlock = new byte[maxBS];

			while (offset < Header.NewSize)
			{
				ReadSegmentHeader(reader, writer, out var size, out var seek);

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
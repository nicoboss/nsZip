using System;
using System.Collections.Generic;
using System.IO;
using LibHac.IO;

namespace nsZip.LibHacExtensions
{
	public static class RecreateDelta
	{
		public static void Recreate(string fragmentMetaInput, string newBaseFileInput)
		{
			var fragmentMeta = File.Open($"{fragmentMetaInput}", FileMode.Open).AsStorage();
			var newBaseFile = File.Open($"{newBaseFileInput}", FileMode.Open);
			var writer = File.Open("fragment_recreated", FileMode.Create);

			var Segments = new List<DeltaFragmentSegment>();
			if (fragmentMeta.Length < 0x40) throw new InvalidDataException("Delta file is too small.");

			var Header = new DeltaFragmentHeader(new StorageFile(fragmentMeta, OpenMode.Read));

			if (Header.Magic != DeltaTools.Ndv0Magic) throw new InvalidDataException("NDV0 magic value is missing.");
			var fragmentSize = Header.FragmentHeaderSize + Header.FragmentBodySize;

			var reader = new FileReader(new StorageFile(fragmentMeta, OpenMode.Read));

			reader.Position = 0;
			writer.Write(reader.ReadBytes((int) Header.FragmentHeaderSize), 0, (int) Header.FragmentHeaderSize);

			long offset = 0;
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

			fragmentMeta.Dispose();
			newBaseFile.Dispose();
			writer.Dispose();
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
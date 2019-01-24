using System;
using System.IO;
using LibHac.IO;

namespace nsZip.LibHacExtensions
{
	public static class SearchDeltaMatching
	{
		public static string SearchMatching(string fragmentInputFilename, string newBaseFolderPath)
		{
			var fragmentFile = File.Open($"{fragmentInputFilename}", FileMode.Open).AsStorage();

			if (fragmentFile.Length < 0x40)
			{
				throw new InvalidDataException("Delta file is too small.");
			}

			var Header = new DeltaFragmentHeader(new StorageFile(fragmentFile, OpenMode.Read));

			if (Header.Magic != DeltaTools.Ndv0Magic)
			{
				throw new InvalidDataException("NDV0 magic value is missing.");
			}

			var fragmentSize = Header.FragmentHeaderSize + Header.FragmentBodySize;
			if (fragmentFile.Length < fragmentSize)
			{
				throw new InvalidDataException(
					$"Delta file is smaller than the header indicates. (0x{fragmentSize} bytes)");
			}

			var fragmentFileReader = new FileReader(new StorageFile(fragmentFile, OpenMode.Read));
			fragmentFileReader.Position = Header.FragmentHeaderSize;

			var d = new DirectoryInfo(newBaseFolderPath);
			foreach (var file in d.GetFiles("*.nca"))
			{
				var newBaseFile = File.Open(file.FullName, FileMode.Open);
				if (newBaseFile.Length != Header.NewSize)
				{
					continue;
				}

				if (VerifyMatching(newBaseFile, fragmentFileReader, Header))
				{
					return file.Name;
				}

				newBaseFile.Dispose();
			}

			fragmentFile.Dispose();
			return null;
		}


		public static bool VerifyMatching(FileStream newBaseFile, FileReader fragmentFileReader,
			DeltaFragmentHeader Header)
		{
			long offset = 0;
			const int maxBS = 10485760; //10 MB
			int bs;
			var BaseFileBlock = new byte[maxBS];

			while (offset < Header.NewSize)
			{
				ReadSegmentHeader(fragmentFileReader, out var size, out var seek);

				if (seek > 0)
				{
					offset += seek;
				}

				if (size > 0)
				{
					newBaseFile.Position = offset;
					var fragmentOffsetEnd = offset + size;
					while (newBaseFile.Position < fragmentOffsetEnd)
					{
						bs = (int) Math.Min(fragmentOffsetEnd - newBaseFile.Position, maxBS);
						newBaseFile.Read(BaseFileBlock, 0, bs);
						var readFragmentFile = fragmentFileReader.ReadBytes(bs);
						for (var i = 0; i < bs; ++i)
						{
							if (BaseFileBlock[i] != readFragmentFile[i])
							{
								return false;
							}
						}
					}

					offset += size;
				}
				else
				{
					fragmentFileReader.Position += size;
				}
			}

			return true;
		}

		private static void ReadSegmentHeader(FileReader reader, out int size, out int seek)
		{
			var type = reader.ReadUInt8();

			var seekBytes = (type & 3) + 1;
			var sizeBytes = ((type >> 3) & 3) + 1;

			size = DeltaTools.ReadInt(reader, sizeBytes);
			seek = DeltaTools.ReadInt(reader, seekBytes);
		}
	}
}
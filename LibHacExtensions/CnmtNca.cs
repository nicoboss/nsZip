using System.IO;
using System.Linq;
using LibHac;
using LibHac.Fs;
using LibHac.Fs.NcaUtils;

namespace nsZip.LibHacExtensions
{
	public static class CnmtNca
	{
		public static CnmtExtended GetCnmtExtended(string folderPath, Keyset keyset, Output Out)
		{
			var dirDecrypted = new DirectoryInfo(folderPath);
			foreach (var inFile in dirDecrypted.GetFiles("*.cnmt.nca"))
			{
				Out.Print($"{inFile}\r\n");
				var ncaStorage = new StreamStorage(new FileStream(inFile.FullName, FileMode.Open, FileAccess.Read),
					false);
				var DecryptedHeader = new byte[0xC00];
				ncaStorage.Read(DecryptedHeader, 0, 0xC00, 0);
				var Header = new NcaHeader(keyset, new MemoryStorage(DecryptedHeader));

				for (var i = 0; i < 4; ++i)
				{
					var section = Header.GetFsHeader(i);
					if (section.FormatType != NcaFormatType.Pfs0)
					{
						continue;
					}
					var sectOffset = Header.GetSectionStartOffset(i);
					var sectSize = Header.GetSectionSize(i);

					IStorage sectionStorage = ncaStorage.Slice(sectOffset, sectSize, false);
					IStorage pfs0Storage = sectionStorage.Slice(Header..Sha256Info.DataOffset,
						section.Header.Sha256Info.DataSize, false);
					var Pfs0Header = new PartitionFileSystemHeader(new BinaryReader(pfs0Storage.AsStream()));
					var FileDict = Pfs0Header.Files.ToDictionary(x => x.Name, x => x);

					foreach (var file in FileDict)
					{
						if (file.Key.EndsWith(".cnmt"))
						{
							IStorage fileStorage = pfs0Storage.Slice(Pfs0Header.HeaderSize + file.Value.Offset,
								file.Value.Size, false);
							var metadata = new Cnmt(fileStorage.AsStream());
							if (metadata.ExtendedData != null)
							{
								ncaStorage.Dispose();
								return metadata.ExtendedData;
							}
						}
					}
				}

				ncaStorage.Dispose();
			}

			return null;
		}
	}
}
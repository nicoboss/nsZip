using System.IO;
using System.Linq;
using System.Windows.Forms;
using LibHac;
using LibHac.IO;
using nsZip.LibHacExtensions;

namespace nsZip.LibHacControl
{
	internal static class UntrimDeltaNCA
	{
		private const string FragmentFileName = "fragment";

		public static void Process(string folderPath, string newBaseFolderPath, Keyset keyset, Output Out)
		{
			var dirDecrypted = new DirectoryInfo(folderPath);
			foreach (var inFile in dirDecrypted.GetFiles("*.tca"))
			{
				Out.Print($"{inFile}\r\n");
				var ncaStorage = new StreamStorage(new FileStream(inFile.FullName, FileMode.Open, FileAccess.Read),
					false);
				var DecryptedHeader = new byte[0xC00];
				ncaStorage.Read(DecryptedHeader, 0, 0xC00, 0);
				var Header = new NcaHeader(new BinaryReader(new MemoryStream(DecryptedHeader)), keyset);

				var fragmentTrimmed = false;
				for (var i = 0; i < 4; ++i)
				{
					var section = NcaParseSection.ParseSection(Header, i);

					if (section == null || section.Header.Type != SectionType.Pfs0)
					{
						continue;
					}

					if (fragmentTrimmed)
					{
						Out.Print(
							"Warning: Multiple fragments in NCA found! Skip trimming this fragment.\r\n");
						continue;
					}

					IStorage sectionStorage = ncaStorage.Slice(section.Offset, section.Size, false);
					IStorage pfs0Storage = sectionStorage.Slice(section.Header.Sha256Info.DataOffset,
						section.Header.Sha256Info.DataSize, false);
					var Pfs0Header = new PartitionFileSystemHeader(new BinaryReader(pfs0Storage.AsStream()));
					var FileDict = Pfs0Header.Files.ToDictionary(x => x.Name, x => x);
					var path = PathTools.Normalize(FragmentFileName).TrimStart('/');
					if (Pfs0Header.NumFiles == 1 && FileDict.TryGetValue(path, out var fragmentFile))
					{
						var inFileNameNoExtension = Path.GetFileNameWithoutExtension(inFile.Name);
						var writer = File.Open($"{folderPath}/{inFileNameNoExtension}.nca", FileMode.Create);
						var offsetBefore = section.Offset + section.Header.Sha256Info.DataOffset +
						                   Pfs0Header.HeaderSize +
						                   fragmentFile.Offset;
						IStorage ncaStorageBeforeFragment = ncaStorage.Slice(0, offsetBefore, false);
						IStorage fragmentStorageOverflow = ncaStorage.Slice(offsetBefore,
							ncaStorage.Length - offsetBefore, false);
						ncaStorageBeforeFragment.CopyToStream(writer);
						var TDV0len = RecreateDelta.Recreate(fragmentStorageOverflow, writer, newBaseFolderPath);
						var offsetAfter = offsetBefore + TDV0len;
						IStorage fragmentStorageAfter = ncaStorage.Slice(offsetAfter,
							ncaStorage.Length - offsetAfter, false);
						fragmentStorageAfter.CopyToStream(writer);
						writer.Position = 0x200;
						writer.WriteByte(0x4E);
						writer.Dispose();
						fragmentTrimmed = true;
					}
				}

				ncaStorage.Dispose();
				File.Delete(inFile.FullName);
			}
		}
	}
}
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LibHac;
using LibHac.IO;
using nsZip.LibHacExtensions;

namespace nsZip.LibHacControl
{
	internal static class TrimDeltaNCA
	{
		private const string FragmentFileName = "fragment";

		public static void Process(string folderPath, Keyset keyset, RichTextBox DebugOutput)
		{
			var dirDecrypted = new DirectoryInfo(folderPath);
			foreach (var inFile in dirDecrypted.GetFiles("*.nca"))
			{
				DebugOutput.AppendText($"{inFile}\r\n");
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
						DebugOutput.AppendText(
							"Warning: Multiple fragments in NCA found! Skip trimming this fragment.\r\n");
						continue;
					}

					IStorage sectionStorage = ncaStorage.Slice(section.Offset, section.Size, false);
					IStorage pfs0Storage = sectionStorage.Slice(section.Header.Sha256Info.DataOffset,
						section.Header.Sha256Info.DataSize, false);
					var Pfs0Header = new PartitionFileSystemHeader(new BinaryReader(pfs0Storage.AsStream()));
					var FileDict = Pfs0Header.Files.ToDictionary(x => x.Name, x => x);
					var path = PathTools.Normalize(FragmentFileName).TrimStart('/');
					if (FileDict.TryGetValue(path, out var fragmentFile))
					{
						if (Pfs0Header.NumFiles != 1)
						{
							throw new InvalidDataException("A fragment Pfs0 container should only contain 1 file");
							//continue;
						}

						IStorage fragmentStorage = pfs0Storage.Slice(Pfs0Header.HeaderSize + fragmentFile.Offset,
							fragmentFile.Size, false);
						var matching = SearchDeltaMatching.SearchMatching(fragmentStorage, "extracted");
						if (matching == null)
						{
							DebugOutput.AppendText("Warning: No matching found! Skip trimming this fragment.\r\n");
							continue;
						}

						var inFileNameNoExtension = Path.GetFileNameWithoutExtension(inFile.Name);
						var writer = File.Open($"decrypted/{inFileNameNoExtension}.tca", FileMode.Create);
						var offsetBefore = section.Offset + section.Header.Sha256Info.DataOffset +
						                   Pfs0Header.HeaderSize +
						                   fragmentFile.Offset;
						var offsetAfter = offsetBefore + fragmentFile.Size;
						IStorage ncaStorageBeforeFragment = ncaStorage.Slice(0, offsetBefore, false);
						IStorage ncaStorageAfterFragment =
							ncaStorage.Slice(offsetAfter, ncaStorage.Length - offsetAfter, false);
						ncaStorageBeforeFragment.CopyToStream(writer);
						//SaveDeltaHeader.Save(fragmentStorage, writer, matching);
						ncaStorageAfterFragment.CopyToStream(writer);
						writer.Position = 0x200;
						writer.WriteByte(0x54);
						writer.Dispose();
						fragmentTrimmed = true;
					}
				}

				ncaStorage.Dispose();
				if (fragmentTrimmed)
				{
					inFile.Delete();
				}
			}
		}
	}
}
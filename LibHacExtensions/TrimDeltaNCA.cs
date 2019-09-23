using System;
using System.IO;
using System.Linq;
using LibHac;
using LibHac.Fs;
using LibHac.NcaLegacy;
using nsZip.LibHacExtensions;

namespace nsZip.LibHacControl
{
	internal static class TrimDeltaNCA
	{
		private const string FragmentFileName = "fragment";

		public static void Process(string folderPath, Keyset keyset, Output Out)
		{
			var cnmtExtended = CnmtNca.GetCnmtExtended(folderPath, keyset, Out);
			if (cnmtExtended == null)
			{
				Out.Log(
					"[Info] Skiped fragemt trimming as no patch Cnmt was found\r\n");
				return;
			}

			if (cnmtExtended.FragmentSets.Length == 0)
			{
				Out.Log(
					"[Info] Skiped fragemt trimming as no DeltaApplyInfos in the patch Cnmt were found\r\n");
				return;
			}

			var DeltaContentID = 0;
			foreach (var deltaApplyInfo in cnmtExtended.FragmentSets)
			{
				long offset = 0;
				for (var deltaApplyInfoId = 0; deltaApplyInfoId < deltaApplyInfo.FragmentCount; ++deltaApplyInfoId)
				{
					var matching = $"{Utils.BytesToString(deltaApplyInfo.NcaIdNew).ToLower()}.nca";
					var length = new FileInfo(Path.Combine(folderPath, matching)).Length;
					var hexLen = Math.Ceiling(Math.Log(length, 16.0));
					if (deltaApplyInfo.FragmentCount > 1)
					{
						matching += $":{offset.ToString($"X{hexLen}")}:{0.ToString($"X{hexLen}")}";
					}

					var lowerNcaID = Utils.BytesToString(cnmtExtended.DeltaContents[DeltaContentID].NcaId)
						.ToLower();
					var ncaFileName = Path.Combine(folderPath, $"{lowerNcaID}.nca");
					if (!File.Exists(ncaFileName))
					{
						Out.Warn($"[WARN] File: {ncaFileName} not found!\r\n");
						break;
					}
					Out.Log($"{ncaFileName}\r\n");
					var ncaStorage = new StreamStorage(new FileStream(ncaFileName, FileMode.Open, FileAccess.Read),
						false);
					var DecryptedHeader = new byte[0xC00];
					ncaStorage.Read(DecryptedHeader, 0, 0xC00, 0);
					var Header = new NcaHeader(new BinaryReader(new MemoryStream(DecryptedHeader)), keyset);

					var fragmentTrimmed = false;
					for (var sector = 0; sector < 4; ++sector)
					{
						var section = NcaParseSection.ParseSection(Header, sector);

						if (section == null || section.Header.Type != SectionType.Pfs0)
						{
							continue;
						}

						IStorage sectionStorage = ncaStorage.Slice(section.Offset, section.Size, false);
						IStorage pfs0Storage = sectionStorage.Slice(section.Header.Sha256Info.DataOffset,
							section.Header.Sha256Info.DataSize, false);
						var Pfs0Header = new PartitionFileSystemHeader(new BinaryReader(pfs0Storage.AsStream()));
						var FileDict = Pfs0Header.Files.ToDictionary(x => x.Name, x => x);
						var path = PathTools.Normalize("fragment").TrimStart('/');
						if (FileDict.TryGetValue(path, out var fragmentFile))
						{
							if (Pfs0Header.NumFiles != 1)
							{
								throw new InvalidDataException(
									"A fragment Pfs0 container should contain exactly 1 file");
							}

							if (fragmentTrimmed)
							{
								throw new InvalidDataException(
									"Multiple fragments in NCA found!");
							}

							IStorage fragmentStorage = pfs0Storage.Slice(
								Pfs0Header.HeaderSize + fragmentFile.Offset,
								fragmentFile.Size, false);
							var buffer = new byte[0xC00];
							fragmentStorage.Read(buffer, 0, buffer.Length, 0);

							var writerPath = Path.Combine(folderPath, $"{lowerNcaID}.tca");
							var writer = File.Open(writerPath, FileMode.Create);
							var offsetBefore = section.Offset + section.Header.Sha256Info.DataOffset +
							                   Pfs0Header.HeaderSize +
							                   fragmentFile.Offset;
							var offsetAfter = offsetBefore + fragmentFile.Size;
							IStorage ncaStorageBeforeFragment = ncaStorage.Slice(0, offsetBefore, false);
							IStorage ncaStorageAfterFragment =
								ncaStorage.Slice(offsetAfter, ncaStorage.GetSize() - offsetAfter, false);
							ncaStorageBeforeFragment.CopyToStream(writer);

							offset += SaveDeltaHeader.Save(fragmentStorage, writer, matching);
							ncaStorageAfterFragment.CopyToStream(writer);
							writer.Position = 0x200;
							writer.WriteByte(0x54);
							writer.Dispose();
							fragmentTrimmed = true;
						}
					}

					ncaStorage.Dispose();
					File.Delete(ncaFileName);
					++DeltaContentID;
				}

				Out.Log("----------\r\n");
			}
		}
	}
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LibHac;
using nsZip.Crypto;
using nsZip.LibHacExtensions;

namespace nsZip
{
	internal static class EncryptNCA
	{
		internal static readonly string[] KakNames = {"application", "ocean", "system"};

		public static void Encrypt(string ncaName, Keyset keyset, RichTextBox TB)
		{
			var DecryptedKeys = Utils.CreateJaggedArray<byte[][]>(4, 0x10);
			var HeaderKey1 = new byte[16];
			var HeaderKey2 = new byte[16];
			Buffer.BlockCopy(keyset.HeaderKey, 0, HeaderKey1, 0, 16);
			Buffer.BlockCopy(keyset.HeaderKey, 16, HeaderKey2, 0, 16);

			var Input = File.Open($"decrypted/{ncaName}", FileMode.Open);
			TB.AppendText($"Input: {Input}\r\n");
			var DecryptedHeader = new byte[0xC00];
			Input.Read(DecryptedHeader, 0, 0xC00);

			var Header = new NcaHeader(new BinaryReader(new MemoryStream(DecryptedHeader)), keyset);
			var CryptoType = Math.Max(Header.CryptoType, Header.CryptoType2);
			if (CryptoType > 0)
			{
				CryptoType--;
			}

			var HasRightsId = !Header.RightsId.IsEmpty();

			if (!HasRightsId)
			{
				TB.AppendText("Key Area (Encrypted):\r\n");
				if (keyset.KeyAreaKeys[CryptoType][Header.KaekInd].IsEmpty())
				{
					throw new ArgumentException($"key_area_key_{KakNames[Header.KaekInd]}_{CryptoType:x2}",
						"Missing area key!");
				}

				TB.AppendText(
					$"key_area_key_{KakNames[Header.KaekInd]}_{CryptoType:x2}: {Utils.BytesToString(keyset.KeyAreaKeys[CryptoType][Header.KaekInd])}\r\n");
				for (var i = 0; i < 4; ++i)
				{
					Crypto.Crypto.DecryptEcb(keyset.KeyAreaKeys[CryptoType][Header.KaekInd], Header.EncryptedKeys[i],
						DecryptedKeys[i], 0x10);
					TB.AppendText($"Key {i} (Encrypted): {Utils.BytesToString(Header.EncryptedKeys[i])}\r\n");
					TB.AppendText($"Key {i} (Decrypted): {Utils.BytesToString(DecryptedKeys[i])}\r\n");
				}
			}
			else
			{
				var TicketFile = File.Open($"extracted/{Utils.BytesToString(Header.RightsId)}.tik", FileMode.Open);
				TicketFile.Seek(0x180, SeekOrigin.Begin);
				var titleKey = new byte[0x10];
				var TitleKeyDec = new byte[0x10];
				TicketFile.Read(titleKey, 0, 0x10);
				TicketFile.Dispose();
				Crypto.Crypto.DecryptEcb(keyset.Titlekeks[CryptoType], titleKey, TitleKeyDec, 0x10);
				TB.AppendText($"titleKey: {Utils.BytesToString(titleKey)}\r\n");
				TB.AppendText($"TitleKeyDec: {Utils.BytesToString(TitleKeyDec)}\r\n");
				DecryptedKeys[2] = TitleKeyDec;
			}

			var Sections = new NcaSection[4];
			var SectionsByOffset = new Dictionary<long, int>();
			var lowestOffset = long.MaxValue;
			for (var i = 0; i < 4; ++i)
			{
				var section = ParseSection(Header, i);
				if (section == null)
				{
					continue;
				}

				SectionsByOffset.Add(section.Offset, i);
				if (section.Offset < lowestOffset)
				{
					lowestOffset = section.Offset;
				}

				Sections[i] = section;
			}

			var Output = File.Open($"encrypted/{ncaName}", FileMode.Create);
			TB.AppendText("Opened NCA for writing...\r\n");
			TB.AppendText($"HeaderKey: {Utils.BytesToString(keyset.HeaderKey)}\r\n");
			TB.AppendText("Encrypting and writing header to NCA...\r\n");
			Output.Write(CryptoInitialisers.AES_XTS(HeaderKey1, HeaderKey2, 0x200, DecryptedHeader, 0), 0,
				DecryptedHeader.Length);

			var dummyHeader = new byte[0xC00];
			ulong dummyHeaderSector = 6;
			long dummyHeaderPos;

			for (dummyHeaderPos = 0xC00; dummyHeaderPos < lowestOffset; dummyHeaderPos += 0xC00)
			{
				Input.Read(dummyHeader, 0, 0xC00);
				var dummyHeaderWriteCount = (int) Math.Min(lowestOffset - dummyHeaderPos, DecryptedHeader.Length);
				Output.Write(CryptoInitialisers.AES_XTS(HeaderKey1, HeaderKey2, 0x200, dummyHeader, dummyHeaderSector),
					0, dummyHeaderWriteCount);
				dummyHeaderSector += 6;
			}

			TB.AppendText("Encrypting and writing sectors to NCA...\r\n");
			TB.AppendText("Sections:");
			foreach (var i in SectionsByOffset.OrderBy(i => i.Key).Select(item => item.Value))
			{
				var sect = Sections[i];
				if (sect == null)
				{
					continue;
				}

				var isExefs = Header.ContentType == ContentType.Program && i == (int) ProgramPartitionType.Code;
				var PartitionType = isExefs ? "ExeFS" : sect.Type.ToString();
				TB.AppendText($"    Section {i}:\r\n");
				TB.AppendText($"        Offset: 0x{sect.Offset:x12}\r\n");
				TB.AppendText($"        Size: 0x{sect.Size:x12}\r\n");
				TB.AppendText($"        Partition Type: {PartitionType}\r\n");
				TB.AppendText($"        Section CTR: {Utils.BytesToString(sect.Header.Ctr)}\r\n");
				var initialCounter = new byte[0x10];


				if (sect.Header.Ctr != null)
				{
					Array.Copy(sect.Header.Ctr, initialCounter, 8);
				}

				TB.AppendText($"initialCounter: {Utils.BytesToString(initialCounter)}\r\n");
				Input.Seek(sect.Offset, SeekOrigin.Begin);
				Output.Seek(sect.Offset, SeekOrigin.Begin);

				const int maxBS = 10485760; //10 MB
				int bs;
				var DecryptedSectionBlock = new byte[maxBS];
				var sectOffsetEnd = sect.Offset + sect.Size;
				switch (sect.Header.EncryptionType)
				{
					case NcaEncryptionType.None:
						while (Input.Position < sectOffsetEnd)
						{
							bs = (int) Math.Min(sectOffsetEnd - Input.Position, maxBS);
							TB.AppendText($"Encrypted: {Input.Position / 0x100000} MB\r\n");
							Input.Read(DecryptedSectionBlock, 0, bs);
							Output.Write(DecryptedSectionBlock, 0, bs);
						}

						break;
					case NcaEncryptionType.AesCtr:
						while (Input.Position < sectOffsetEnd)
						{
							SetCtrOffset(initialCounter, Input.Position);
							bs = (int) Math.Min(sectOffsetEnd - Input.Position, maxBS);
							TB.AppendText($"Encrypted: {Input.Position / 0x100000} MB\r\n");
							Input.Read(DecryptedSectionBlock, 0, bs);
							Output.Write(
								AesCTR.AesCtrTransform(DecryptedKeys[2], initialCounter, DecryptedSectionBlock, bs), 0,
								bs);
						}

						break;
					case NcaEncryptionType.AesCtrEx:

						var info = sect.Header.BktrInfo;
						var MyBucketTree = new MyBucketTree<AesSubsectionEntry>(
							new MemoryStream(sect.Header.BktrInfo.EncryptionHeader.Header), Input,
							sect.Offset + info.EncryptionHeader.Offset);
						var SubsectionEntries = MyBucketTree.GetEntryList();
						var SubsectionOffsets = SubsectionEntries.Select(x => x.Offset).ToList();

						var subsectionEntryCounter = new byte[0x10];
						Array.Copy(initialCounter, subsectionEntryCounter, 0x10);
						foreach (var entry in SubsectionEntries)
						{
							//Array.Copy(initialCounter, subsectionEntryCounter, 0x10);
							SetCtrOffset(subsectionEntryCounter, Input.Position);
							subsectionEntryCounter[7] = (byte) entry.Counter;
							subsectionEntryCounter[6] = (byte) (entry.Counter >> 8);
							subsectionEntryCounter[5] = (byte) (entry.Counter >> 16);
							subsectionEntryCounter[4] = (byte) (entry.Counter >> 24);

							//bs = (int)Math.Min((sect.Offset + entry.OffsetEnd) - Input.Position, maxBS);
							bs = (int) (entry.OffsetEnd - entry.Offset);
							var DecryptedSectionBlockLUL = new byte[bs];
							TB.AppendText($"Encrypted: {Input.Position / 0x100000} MB\r\n");
							TB.AppendText($"{Input.Position}: {Utils.BytesToString(subsectionEntryCounter)}\r\n");
							Input.Read(DecryptedSectionBlockLUL, 0, bs);
							Output.Write(
								AesCTR.AesCtrTransform(DecryptedKeys[2], subsectionEntryCounter,
									DecryptedSectionBlockLUL, bs), 0, bs);
						}

						while (Input.Position < sectOffsetEnd)
						{
							SetCtrOffset(subsectionEntryCounter, Input.Position);
							bs = (int) Math.Min(sectOffsetEnd - Input.Position, maxBS);
							TB.AppendText($"EncryptedAfter: {Input.Position / 0x100000} MB\r\n");
							Input.Read(DecryptedSectionBlock, 0, bs);
							TB.AppendText($"{Input.Position}: {Utils.BytesToString(subsectionEntryCounter)}\r\n");
							Output.Write(
								AesCTR.AesCtrTransform(DecryptedKeys[2], subsectionEntryCounter, DecryptedSectionBlock,
									bs), 0, bs);
						}

						break;

					default:
						throw new NotImplementedException();
				}
			}

			Input.Dispose();
			Output.Dispose();
			TB.AppendText("Done!");
		}

		private static NcaSection ParseSection(NcaHeader Header, int index)
		{
			var entry = Header.SectionEntries[index];
			var header = Header.FsHeaders[index];
			if (entry.MediaStartOffset == 0)
			{
				return null;
			}

			var sect = new NcaSection();

			sect.SectionNum = index;
			sect.Offset = Utils.MediaToReal(entry.MediaStartOffset);
			sect.Size = Utils.MediaToReal(entry.MediaEndOffset) - sect.Offset;
			sect.Header = header;
			sect.Type = header.Type;

			return sect;
		}

		private static void SetCtrOffset(byte[] ctr, long offset)
		{
			ctr[0xF] = (byte) (offset >> 4);
			ctr[0xE] = (byte) (offset >> 12);
			ctr[0xD] = (byte) (offset >> 20);
			ctr[0xC] = (byte) (offset >> 28);
			ctr[0xB] = (byte) (offset >> 36);
			ctr[0xA] = (byte) (offset >> 44);
			ctr[0x9] = (byte) (offset >> 52);
			ctr[0x8] = (byte) (offset >> 60);
		}
	}
}
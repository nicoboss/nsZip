using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LibHac;
using LibHac.IO;
using LibHac.NcaLegacy;
using nsZip.Crypto;
using nsZip.LibHacExtensions;
using AesSubsectionEntry = nsZip.LibHacExtensions.AesSubsectionEntry;

namespace nsZip
{
	internal static class EncryptNCA
	{
		internal static readonly string[] KakNames = {"application", "ocean", "system"};

		public static void Encrypt(string ncaPath, string outDir, bool writeEncrypted, bool verifyEncrypted, Keyset keyset,
			Output Out)
		{
			Out.Log($"Input: {Path.GetFileName(ncaPath)}\r\n");
			using (FileStream Input = File.Open(ncaPath, FileMode.Open))
			{
				if (writeEncrypted)
				{
					Out.Log("Opened NCA for writing...\r\n");
					using (Stream Output = File.Open(Path.Combine(outDir, Path.GetFileName(ncaPath)), FileMode.Create))
					{
						EncryptFunct(Input, Output, ncaPath, verifyEncrypted, keyset, Out);
					}
				}
				else
				{
					EncryptFunct(Input, null, ncaPath, verifyEncrypted, keyset, Out);
				}
			}
		}

		public static void EncryptFunct(
			FileStream Input, Stream Output, string ncaPath,
			bool verifyEncrypted, Keyset keyset, Output Out)
		{
			var DecryptedKeys = Utils.CreateJaggedArray<byte[][]>(4, 0x10);
			var HeaderKey1 = new byte[16];
			var HeaderKey2 = new byte[16];
			Buffer.BlockCopy(keyset.HeaderKey, 0, HeaderKey1, 0, 16);
			Buffer.BlockCopy(keyset.HeaderKey, 16, HeaderKey2, 0, 16);

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
				Out.Log("Key Area (Encrypted):\r\n");
				if (keyset.KeyAreaKeys[CryptoType][Header.KaekInd].IsEmpty())
				{
					throw new ArgumentException($"key_area_key_{KakNames[Header.KaekInd]}_{CryptoType:x2}",
						"Missing area key!");
				}

				Out.Log(
					$"key_area_key_{KakNames[Header.KaekInd]}_{CryptoType:x2}: {Utils.BytesToString(keyset.KeyAreaKeys[CryptoType][Header.KaekInd])}\r\n");
				for (var i = 0; i < 4; ++i)
				{
					LibHac.Crypto.DecryptEcb(keyset.KeyAreaKeys[CryptoType][Header.KaekInd], Header.EncryptedKeys[i],
						DecryptedKeys[i], 0x10);
					Out.Log($"Key {i} (Encrypted): {Utils.BytesToString(Header.EncryptedKeys[i])}\r\n");
					Out.Log($"Key {i} (Decrypted): {Utils.BytesToString(DecryptedKeys[i])}\r\n");
				}
			}
			else
			{
				var titleKey = keyset.TitleKeys[Header.RightsId];
				var TitleKeyDec = new byte[0x10];
				LibHac.Crypto.DecryptEcb(keyset.TitleKeks[CryptoType], titleKey, TitleKeyDec, 0x10);
				Out.Log($"titleKey: {Utils.BytesToString(titleKey)}\r\n");
				Out.Log($"TitleKeyDec: {Utils.BytesToString(TitleKeyDec)}\r\n");
				DecryptedKeys[2] = TitleKeyDec;
			}

			var Sections = new NcaSection[4];
			var SectionsByOffset = new Dictionary<long, int>();
			var lowestOffset = long.MaxValue;
			for (var i = 0; i < 4; ++i)
			{
				var section = NcaParseSection.ParseSection(Header, i);
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

			Out.Log($"HeaderKey: {Utils.BytesToString(keyset.HeaderKey)}\r\n");
			Out.Log("Encrypting and writing header to NCA...\r\n");
			SHA256Cng sha256NCA = null;
			if (verifyEncrypted)
			{
				sha256NCA = new SHA256Cng();
				sha256NCA.Initialize();
			}

			var encryptedHeader = CryptoInitialisers.AES_XTS(HeaderKey1, HeaderKey2, 0x200, DecryptedHeader, 0);
			if (Output != null)
			{
				Output.Write(encryptedHeader, 0, DecryptedHeader.Length);
			}

			if (verifyEncrypted)
			{
				sha256NCA.TransformBlock(encryptedHeader, 0, DecryptedHeader.Length, null, 0);
			}

			var dummyHeader = new byte[0xC00];
			ulong dummyHeaderSector = 6;
			long dummyHeaderPos;

			for (dummyHeaderPos = 0xC00; dummyHeaderPos < lowestOffset; dummyHeaderPos += 0xC00)
			{
				var dummyHeaderWriteCount = (int)Math.Min(lowestOffset - dummyHeaderPos, DecryptedHeader.Length);
				Input.Read(dummyHeader, 0, dummyHeaderWriteCount);
				var dummyHeaderEncrypted =
					CryptoInitialisers.AES_XTS(HeaderKey1, HeaderKey2, 0x200, dummyHeader, dummyHeaderSector);
				if (Output != null)
				{
					Output.Write(dummyHeaderEncrypted, 0, dummyHeaderWriteCount);
				}

				if (verifyEncrypted)
				{
					sha256NCA.TransformBlock(dummyHeaderEncrypted, 0, dummyHeaderWriteCount, null, 0);
				}

				dummyHeaderSector += 6;
			}

			Out.Log("Encrypting and writing sectors to NCA...\r\n");
			Out.Log("Sections:\r\n");
			foreach (var i in SectionsByOffset.OrderBy(i => i.Key).Select(item => item.Value))
			{
				var sect = Sections[i];
				if (sect == null)
				{
					continue;
				}

				var isExefs = Header.ContentType == ContentType.Program && i == (int)ProgramPartitionType.Code;
				var PartitionType = isExefs ? "ExeFS" : sect.Type.ToString();
				Out.Log($"    Section {i}:\r\n");
				Out.Log($"        Offset: 0x{sect.Offset:x12}\r\n");
				Out.Log($"        Size: 0x{sect.Size:x12}\r\n");
				Out.Log($"        Partition Type: {PartitionType}\r\n");
				Out.Log($"        Section CTR: {Utils.BytesToString(sect.Header.Ctr)}\r\n");
				var initialCounter = new byte[0x10];


				if (sect.Header.Ctr != null)
				{
					Array.Copy(sect.Header.Ctr, initialCounter, 8);
				}

				Out.Log($"initialCounter: {Utils.BytesToString(initialCounter)}\r\n");

				if (Input.Position != sect.Offset)
				{
					//Input.Seek(sect.Offset, SeekOrigin.Begin);
					//Output.Seek(sect.Offset, SeekOrigin.Begin);
					//Todo: sha256NCA Gap support
					throw new NotImplementedException("Gaps between NCA sections aren't implemented yet!");
				}

				const int maxBS = 10485760; //10 MB
				int bs;
				var DecryptedSectionBlock = new byte[maxBS];
				var sectOffsetEnd = sect.Offset + sect.Size;
				var AesCtrEncrypter = new Aes128CtrTransform(DecryptedKeys[2], initialCounter);
				switch (sect.Header.EncryptionType)
				{
					case NcaEncryptionType.None:
						while (Input.Position < sectOffsetEnd)
						{
							bs = (int)Math.Min(sectOffsetEnd - Input.Position, maxBS);
							Out.Print($"Encrypted: {Input.Position / 0x100000} MB\r\n");
							Input.Read(DecryptedSectionBlock, 0, bs);
							if (Output != null)
							{
								Output.Write(DecryptedSectionBlock, 0, bs);
							}

							if (verifyEncrypted)
							{
								sha256NCA.TransformBlock(DecryptedSectionBlock, 0, bs, null, 0);
							}
						}

						break;
					case NcaEncryptionType.AesCtr:

						while (Input.Position < sectOffsetEnd)
						{
							SetCtrOffset(initialCounter, Input.Position);
							bs = (int)Math.Min(sectOffsetEnd - Input.Position, maxBS);
							Out.Print($"Encrypted: {Input.Position / 0x100000} MB\r\n");
							Input.Read(DecryptedSectionBlock, 0, bs);
							AesCtrEncrypter.Counter = initialCounter;
							AesCtrEncrypter.TransformBlock(DecryptedSectionBlock);

							if (Output != null)
							{
								Output.Write(DecryptedSectionBlock, 0, bs);
							}

							if (verifyEncrypted)
							{
								sha256NCA.TransformBlock(DecryptedSectionBlock, 0, bs, null, 0);
							}
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
							subsectionEntryCounter[7] = (byte)entry.Counter;
							subsectionEntryCounter[6] = (byte)(entry.Counter >> 8);
							subsectionEntryCounter[5] = (byte)(entry.Counter >> 16);
							subsectionEntryCounter[4] = (byte)(entry.Counter >> 24);

							bs = (int)Math.Min((sect.Offset + entry.OffsetEnd) - Input.Position, maxBS);
							var DecryptedSectionBlockLUL = new byte[bs];
							Out.Print($"Encrypted: {Input.Position / 0x100000} MB\r\n");
							Out.Log($"{Input.Position}: {Utils.BytesToString(subsectionEntryCounter)}\r\n");
							Input.Read(DecryptedSectionBlockLUL, 0, bs);
							AesCtrEncrypter.Counter = subsectionEntryCounter;
							AesCtrEncrypter.TransformBlock(DecryptedSectionBlockLUL);
							if (Output != null)
							{
								Output.Write(DecryptedSectionBlockLUL, 0, bs);
							}

							if (verifyEncrypted)
							{
								sha256NCA.TransformBlock(DecryptedSectionBlockLUL, 0, bs, null, 0);
							}
						}

						while (Input.Position < sectOffsetEnd)
						{
							SetCtrOffset(subsectionEntryCounter, Input.Position);
							bs = (int)Math.Min(sectOffsetEnd - Input.Position, maxBS);
							Out.Print($"EncryptedAfter: {Input.Position / 0x100000} MB\r\n");
							Input.Read(DecryptedSectionBlock, 0, bs);
							Out.Log($"{Input.Position}: {Utils.BytesToString(subsectionEntryCounter)}\r\n");
							AesCtrEncrypter.Counter = subsectionEntryCounter;
							AesCtrEncrypter.TransformBlock(DecryptedSectionBlock);
							if (Output != null)
							{
								Output.Write(DecryptedSectionBlock, 0, bs);
							}

							if (verifyEncrypted)
							{
								sha256NCA.TransformBlock(DecryptedSectionBlock, 0, bs, null, 0);
							}
						}

						break;

					default:
						throw new NotImplementedException();
				}
			}

			if (verifyEncrypted)
			{
				sha256NCA.TransformFinalBlock(new byte[0], 0, 0);
				var sha256NCAHashString = Utils.BytesToString(sha256NCA.Hash).ToLower();
				if (sha256NCAHashString.StartsWith(Path.GetFileName(ncaPath).Split('.')[0].ToLower()))
				{
					Out.Log($"[VERIFIED] {sha256NCAHashString}\r\n");
				}
				else
				{
					throw new Exception($"[INVALID HASH] sha256({Path.GetFileName(ncaPath)}) = {sha256NCAHashString}\r\n");
				}
			}
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
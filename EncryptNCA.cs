using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LibHac;
using LibHac.Fs;
using LibHac.Fs.NcaUtils;
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
			Out.Print($"Input: {Path.GetFileName(ncaPath)}\r\n");
			using (FileStream Input = File.Open(ncaPath, FileMode.Open))
			{
				if (writeEncrypted)
				{
					Out.Print("Opened NCA for writing...\r\n");
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

			var Header = new NcaHeader(keyset, new MemoryStorage(DecryptedHeader));

			if (!Header.HasRightsId)
			{
				Out.Print("Key Area (Encrypted):\r\n");
				if (keyset.KeyAreaKeys[Header.KeyGeneration][Header.KeyAreaKeyIndex].IsEmpty())
				{
					throw new ArgumentException($"key_area_key_{KakNames[Header.KeyAreaKeyIndex]}_{Header.KeyGeneration:x2}",
						"Missing area key!");
				}

				Out.Print(
					$"key_area_key_{KakNames[Header.KeyAreaKeyIndex]}_{Header.KeyGeneration:x2}: {Utils.BytesToString(keyset.KeyAreaKeys[Header.KeyGeneration][Header.KeyAreaKeyIndex])}\r\n");
				for (var i = 0; i < 4; ++i)
				{
					LibHac.Crypto.DecryptEcb(keyset.KeyAreaKeys[Header.KeyGeneration][Header.KeyAreaKeyIndex], Header.GetEncryptedKey(i).ToArray(),
						DecryptedKeys[i], 0x10);
					Out.Print($"Key {i} (Encrypted): {Utils.BytesToString(Header.GetEncryptedKey(i).ToArray())}\r\n");
					Out.Print($"Key {i} (Decrypted): {Utils.BytesToString(DecryptedKeys[i])}\r\n");
				}
			}
			else
			{
				var titleKey = keyset.TitleKeys[Header.RightsId.ToArray()];
				var TitleKeyDec = new byte[0x10];
				LibHac.Crypto.DecryptEcb(keyset.TitleKeks[Header.KeyGeneration], titleKey, TitleKeyDec, 0x10);
				Out.Print($"titleKey: {Utils.BytesToString(titleKey)}\r\n");
				Out.Print($"TitleKeyDec: {Utils.BytesToString(TitleKeyDec)}\r\n");
				DecryptedKeys[2] = TitleKeyDec;
			}

			var SectionsByOffset = new Dictionary<long, int>();
			var lowestOffset = long.MaxValue;
			for (var i = 0; i < 4; ++i)
			{
				var offset = Header.GetSectionStartOffset(i);
				SectionsByOffset.Add(offset, i);
				if (offset < lowestOffset)
				{
					lowestOffset = offset;
				}
			}

			Out.Print($"HeaderKey: {Utils.BytesToString(keyset.HeaderKey)}\r\n");
			Out.Print("Encrypting and writing header to NCA...\r\n");
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

			Out.Print("Encrypting and writing sectors to NCA...\r\n");
			Out.Print("Sections:\r\n");
			foreach (var i in SectionsByOffset.OrderBy(i => i.Key).Select(item => item.Value))
			{
				var sect = Header.GetFsHeader(i);
				var sectOffset = Header.GetSectionStartOffset(i);
				var sectSize = Header.GetSectionSize(i);

				var isExefs = Header.ContentType == ContentType.Program && i == (int)NcaFormatType.Romfs;
				var PartitionType = isExefs ? "ExeFS" : sect.FormatType.ToString();
				Out.Print($"    Section {i}:\r\n");
				Out.Print($"        Offset: 0x{sectOffset:x12}\r\n");
				Out.Print($"        Size: 0x{sectSize:x12}\r\n");
				Out.Print($"        Partition Type: {PartitionType}\r\n");
				Out.Print($"        Section CTR: {sect.Counter}\r\n");

				var initialCounter = new byte[0x10];
				SetCtrOffset(initialCounter, sect.Counter);

				if (Input.Position != sectOffset)
				{
					//Input.Seek(sectOffset, SeekOrigin.Begin);
					//Output.Seek(sectOffset, SeekOrigin.Begin);
					//Todo: sha256NCA Gap support
					throw new NotImplementedException("Gaps between NCA sections aren't implemented yet!");
				}

				const int maxBS = 10485760; //10 MB
				int bs;
				var DecryptedSectionBlock = new byte[maxBS];
				var sectOffsetEnd = sectOffset + sectSize;
				var AesCtrEncrypter = new Aes128CtrTransform(DecryptedKeys[2], initialCounter);
				switch (sect.EncryptionType)
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
							SetCtrOffset(initialCounter, (ulong)Input.Position);
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
						var info = sect.GetPatchInfo();
						var MyBucketTree = new MyBucketTree<AesSubsectionEntry>(
							new MemoryStream(info.EncryptionTreeHeader.ToArray()), Input,
							sectOffset + info.EncryptionTreeOffset);
						var SubsectionEntries = MyBucketTree.GetEntryList();
						var SubsectionOffsets = SubsectionEntries.Select(x => x.Offset).ToList();

						var subsectionEntryCounter = new byte[0x10];
						Array.Copy(initialCounter, subsectionEntryCounter, 0x10);
						foreach (var entry in SubsectionEntries)
						{
							//Array.Copy(initialCounter, subsectionEntryCounter, 0x10);
							SetCtrOffset(subsectionEntryCounter, (ulong)Input.Position);
							subsectionEntryCounter[7] = (byte)entry.Counter;
							subsectionEntryCounter[6] = (byte)(entry.Counter >> 8);
							subsectionEntryCounter[5] = (byte)(entry.Counter >> 16);
							subsectionEntryCounter[4] = (byte)(entry.Counter >> 24);

							//bs = (int)Math.Min((sectOffset + entry.OffsetEnd) - Input.Position, maxBS);
							bs = (int)(entry.OffsetEnd - entry.Offset);
							var DecryptedSectionBlockLUL = new byte[bs];
							Out.Print($"Encrypted: {Input.Position / 0x100000} MB\r\n");
							Out.Print($"{Input.Position}: {Utils.BytesToString(subsectionEntryCounter)}\r\n");
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
							SetCtrOffset(subsectionEntryCounter, (ulong)Input.Position);
							bs = (int)Math.Min(sectOffsetEnd - Input.Position, maxBS);
							Out.Print($"EncryptedAfter: {Input.Position / 0x100000} MB\r\n");
							Input.Read(DecryptedSectionBlock, 0, bs);
							Out.Print($"{Input.Position}: {Utils.BytesToString(subsectionEntryCounter)}\r\n");
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
					Out.Print($"[VERIFIED] {sha256NCAHashString}\r\n");
				}
				else
				{
					throw new Exception($"[INVALID HASH] {sha256NCAHashString}\r\n");
				}
			}
		}

		private static void SetCtrOffset(byte[] ctr, ulong offset)
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
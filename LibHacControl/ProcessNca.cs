using System.IO;
using System.Linq;
using System.Text;
using LibHac;
using LibHac.IO;

namespace nsZip.LibHacControl
{
	internal static class ProcessNca
	{

		public static void Extract(Stream inFileStream, string outDir, bool verify, Keyset keyset, Output Out, bool isDecryptedNca = false)
		{
			using (var file = new StreamStorage(inFileStream, false))
			{
				var nca = new Nca(keyset, file, false, isDecryptedNca);
				Out.Log(nca.Print());
				if (verify)
				{
					Out.Log($"ValidateMasterHashes...\r\n");
					nca.ValidateMasterHashes();
				}

				for (var i = 0; i < 3; ++i)
				{
					if (nca.Sections[i] != null)
					{
						if (verify)
						{
							nca.VerifySection(i, Out);
						}
						nca.ExtractSection(i, Path.Combine(outDir, i.ToString()), IntegrityCheckLevel.None, Out);
					}
				}
			}
		}

		public static void Process(IFile inFile, IFile outFile, bool verifyBeforeDecrypting, Keyset keyset, Output Out)
		{
			using (var file = new StreamStorage(inFile.AsStream(), false))
			{
				var nca = new Nca(keyset, file, false);
				Out.Log(nca.Print());
				if (verifyBeforeDecrypting)
				{
					Out.Log($"ValidateMasterHashes...\r\n");
					nca.ValidateMasterHashes();
					//nca.ParseNpdm();
					for (var i = 0; i < 3; ++i)
					{
						if (nca.Sections[i] != null)
						{
							nca.VerifySection(i, Out);
						}
					}
				}
				Out.Log($"Decripting...\r\n");
				nca.OpenDecryptedNca().CopyToStream(outFile.AsStream());
			}
		}

		private static string Print(this Nca nca)
		{
			var colLen = 36;
			var sb = new StringBuilder();
			sb.AppendLine();

			sb.AppendLine("NCA:");
			LibHacControl.Print.PrintItem(sb, colLen, "Magic:", nca.Header.Magic);
			LibHacControl.Print.PrintItem(sb, colLen,
				$"Fixed-Key Signature{nca.Header.FixedSigValidity.GetValidityString()}:", nca.Header.Signature1);
			LibHacControl.Print.PrintItem(sb, colLen,
				$"NPDM Signature{nca.Header.NpdmSigValidity.GetValidityString()}:", nca.Header.Signature2);
			LibHacControl.Print.PrintItem(sb, colLen, "Content Size:", $"0x{nca.Header.NcaSize:x12}");
			LibHacControl.Print.PrintItem(sb, colLen, "TitleID:", $"{nca.Header.TitleId:X16}");
			LibHacControl.Print.PrintItem(sb, colLen, "SDK Version:", nca.Header.SdkVersion);
			LibHacControl.Print.PrintItem(sb, colLen, "Distribution type:", nca.Header.Distribution);
			LibHacControl.Print.PrintItem(sb, colLen, "Content Type:", nca.Header.ContentType);
			LibHacControl.Print.PrintItem(sb, colLen, "Master Key Revision:",
				$"{nca.CryptoType} ({Util.GetKeyRevisionSummary(nca.CryptoType)})");
			LibHacControl.Print.PrintItem(sb, colLen, "Encryption Type:",
				$"{(nca.HasRightsId ? "Titlekey crypto" : "Standard crypto")}");

			if (nca.HasRightsId)
			{
				LibHacControl.Print.PrintItem(sb, colLen, "Rights ID:", nca.Header.RightsId);
			}
			else
			{
				LibHacControl.Print.PrintItem(sb, colLen, "Key Area Encryption Key:", nca.Header.KaekInd);
				sb.AppendLine("Key Area (Encrypted):");
				for (var i = 0; i < 4; i++)
				{
					LibHacControl.Print.PrintItem(sb, colLen, $"    Key {i} (Encrypted):", nca.Header.EncryptedKeys[i]);
				}

				sb.AppendLine("Key Area (Decrypted):");
				for (var i = 0; i < 4; i++)
				{
					LibHacControl.Print.PrintItem(sb, colLen, $"    Key {i} (Decrypted):", nca.DecryptedKeys[i]);
				}
			}

			PrintSections();

			return sb.ToString();

			void PrintSections()
			{
				sb.AppendLine("Sections:");

				for (var i = 0; i < 4; i++)
				{
					var sect = nca.Sections[i];
					if (sect == null)
					{
						continue;
					}

					var isExefs = nca.Header.ContentType == ContentType.Program && i == (int) ProgramPartitionType.Code;

					sb.AppendLine($"    Section {i}:");
					LibHacControl.Print.PrintItem(sb, colLen, "        Offset:", $"0x{sect.Offset:x12}");
					LibHacControl.Print.PrintItem(sb, colLen, "        Size:", $"0x{sect.Size:x12}");
					LibHacControl.Print.PrintItem(sb, colLen, "        Partition Type:",
						isExefs ? "ExeFS" : sect.Type.ToString());
					LibHacControl.Print.PrintItem(sb, colLen, "        Section CTR:", sect.Header.Ctr);

					switch (sect.Header.HashType)
					{
						case NcaHashType.Sha256:
							PrintSha256Hash(sect);
							break;
						case NcaHashType.Ivfc:
							LibHacControl.Print.PrintIvfcHash(sb, colLen, 8, sect.Header.IvfcInfo,
								IntegrityStorageType.RomFs);
							break;
						default:
							sb.AppendLine("        Unknown/invalid superblock!");
							break;
					}
				}
			}

			void PrintSha256Hash(NcaSection sect)
			{
				var hashInfo = sect.Header.Sha256Info;

				LibHacControl.Print.PrintItem(sb, colLen,
					$"        Master Hash{sect.MasterHashValidity.GetValidityString()}:", hashInfo.MasterHash);
				sb.AppendLine($"        Hash Table{sect.Header.Sha256Info.HashValidity.GetValidityString()}:");

				LibHacControl.Print.PrintItem(sb, colLen, "            Offset:", $"0x{hashInfo.HashTableOffset:x12}");
				LibHacControl.Print.PrintItem(sb, colLen, "            Size:", $"0x{hashInfo.HashTableSize:x12}");
				LibHacControl.Print.PrintItem(sb, colLen, "            Block Size:", $"0x{hashInfo.BlockSize:x}");
				LibHacControl.Print.PrintItem(sb, colLen, "        PFS0 Offset:", $"0x{hashInfo.DataOffset:x12}");
				LibHacControl.Print.PrintItem(sb, colLen, "        PFS0 Size:", $"0x{hashInfo.DataSize:x12}");
			}
		}
	}
}
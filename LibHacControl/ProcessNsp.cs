using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using LibHac;
using LibHac.IO;
using nsZip.Crypto;
using nsZip.LibHacExtensions;

namespace nsZip.LibHacControl
{
	internal static class ProcessNsp
	{

		public static void Extract(string inFile, string outDirPath, Output Out)
		{
			using (var file = new FileStream(inFile, FileMode.Open, FileAccess.Read))
			{
				var pfs = new PartitionFileSystem(file.AsStorage());
				Out.Log(pfs.Print());
				pfs.Extract(outDirPath);
			}
		}

		public static void ExtractRomFS(string inFile, string outDirPath, Keyset keyset, Output Out)
		{
			using (var file = new FileStream(inFile, FileMode.Open, FileAccess.Read))
			{
				var pfs = new PartitionFileSystem(file.AsStorage());
				var OutDirFs = new LocalFileSystem(outDirPath);
				IDirectory sourceRoot = pfs.OpenDirectory("/", OpenDirectoryMode.All);
				IFileSystem sourceFs = sourceRoot.ParentFileSystem;
				Out.Log(pfs.Print());

				foreach (var entry in FileIterator(sourceRoot))
				{
					if (entry.Name.EndsWith(".nca"))
					{
						var fullOutDirPath = $"{outDirPath}/{entry.Name}";
						Out.Log($"Extracting {entry.Name}...\r\n");
						using (IFile srcFile = sourceFs.OpenFile(entry.Name, OpenMode.Read))
						{
							ProcessNca.Extract(srcFile.AsStream(), fullOutDirPath, true, keyset, Out);
						}
					}
					else if (entry.Name.EndsWith(".nca.nsz"))
					{
						var fullOutDirPath = $"{outDirPath}/{entry.Name}";
						Out.Log($"Extracting {entry.Name}...\r\n");
						using (IFile srcFile = sourceFs.OpenFile(entry.Name, OpenMode.Read))
						using (var decompressedFile = new DecompressionStorage(srcFile))
						{
							ProcessNca.Extract(decompressedFile.AsStream(), fullOutDirPath, true, keyset, Out, true);

							// Header can't be patched for now due to OpenSection
							// and ValidateMasterHash needs to know if AesCtrEx
							// so Nca.cs was patched and now accepts isDecryptedNca
							// as constructor argument which disables decryption
							/*
							var DecryptedHeader = new byte[0xC00];
							decompressedFile.AsStream().Read(DecryptedHeader, 0, 0xC00);
							DecryptedHeader[1028] = (int)NcaEncryptionType.None;
							DecryptedHeader[1540] = (int)NcaEncryptionType.None;
							DecryptedHeader[2052] = (int)NcaEncryptionType.None;
							DecryptedHeader[2564] = (int)NcaEncryptionType.None;
							var HeaderKey1 = new byte[16];
							var HeaderKey2 = new byte[16];
							Buffer.BlockCopy(keyset.HeaderKey, 0, HeaderKey1, 0, 16);
							Buffer.BlockCopy(keyset.HeaderKey, 16, HeaderKey2, 0, 16);
							var headerEncrypted = CryptoInitialisers.AES_XTS(HeaderKey1, HeaderKey2, 0x200, DecryptedHeader, 0);
							var ncaStorageList = new List<IStorage>() { new MemoryStorage(headerEncrypted), decompressedFile.Slice(0xC00) };
							var cleanDecryptedNca = new ConcatenationStorage(ncaStorageList, true);
							ProcessNca.Extract(cleanDecryptedNca.AsStream(), fullOutDirPath, true, keyset, Out);
							*/
						}
					}
				}
			}
		}

		public static IEnumerable<DirectoryEntry> FileIterator(IDirectory sourceRoot)
		{
			foreach (var entry in sourceRoot.Read())
			{
				if (entry.Type == DirectoryEntryType.Directory)
				{
					throw new InvalidDataException(
						"Error: Directory inside NSP!\r\n" +
						"Please report this as there are curently no known NSP containing a directory.");
				}

				yield return entry;
			}
		}

		public static void Decompress(string inFile, string outDirPath, Output Out)
		{
			using (var file = new FileStream(inFile, FileMode.Open, FileAccess.Read))
			{
				var pfs = new PartitionFileSystem(file.AsStorage());
				Out.Log(pfs.Print());
				DecompressFs.ProcessFs(pfs, outDirPath, Out);				
			}
		}

		public static void Decrypt(PartitionFileSystem pfs, string outDirPath, bool verifyBeforeDecrypting, Keyset keyset, Output Out)
		{
			Out.Log(pfs.Print());
			ProcessNsp.GetTitlekey(pfs, keyset, Out);
			var OutDirFs = new LocalFileSystem(outDirPath);
			IDirectory sourceRoot = pfs.OpenDirectory("/", OpenDirectoryMode.All);
			IDirectory destRoot = OutDirFs.OpenDirectory("/", OpenDirectoryMode.All);
			IFileSystem sourceFs = sourceRoot.ParentFileSystem;
			IFileSystem destFs = destRoot.ParentFileSystem;			

			foreach (var entry in FileIterator(sourceRoot))
			{
				destFs.CreateFile(entry.Name, entry.Size, CreateFileOptions.None);
				using (IFile srcFile = sourceFs.OpenFile(entry.Name, OpenMode.Read))
				using (IFile dstFile = destFs.OpenFile(entry.Name, OpenMode.Write))
				{
					if (entry.Name.EndsWith(".nca"))
					{
						ProcessNca.Process(srcFile, dstFile, verifyBeforeDecrypting, keyset, Out);
					}
					else
					{
						srcFile.CopyTo(dstFile);
					}
				}
			}
		}

		private static string Print(this PartitionFileSystem pfs)
		{
			const int colLen = 36;
			const int fileNameLen = 39;

			var sb = new StringBuilder();
			sb.AppendLine();

			sb.AppendLine("PFS0:");

			LibHacControl.Print.PrintItem(sb, colLen, "Magic:", pfs.Header.Magic);
			LibHacControl.Print.PrintItem(sb, colLen, "Number of files:", pfs.Header.NumFiles);

			for (var i = 0; i < pfs.Files.Length; i++)
			{
				var file = pfs.Files[i];

				var label = i == 0 ? "Files:" : "";
				var offsets = $"{file.Offset:x12}-{file.Offset + file.Size:x12}{file.HashValidity.GetValidityString()}";
				var data = $"pfs0:/{file.Name}".PadRight(fileNameLen) + offsets;

				LibHacControl.Print.PrintItem(sb, colLen, label, data);
			}

			return sb.ToString();
		}

		public static void CreateNsp(ulong TitleId, string nspFilename, SwitchFs switchFs, IProgressReport logger)
		{
			if (TitleId == 0)
			{
				logger.LogMessage("Title ID must be specified to save title");
				return;
			}

			if (!switchFs.Titles.TryGetValue(TitleId, out var title))
			{
				logger.LogMessage($"Could not find title {TitleId:X16}");
				return;
			}

			var builder = new PartitionFileSystemBuilder();

			foreach (var nca in title.Ncas)
			{
				builder.AddFile(nca.Filename, new StorageFile(nca.GetStorage(), OpenMode.Read));
			}

			var ticket = new Ticket
			{
				SignatureType = TicketSigType.Rsa2048Sha256,
				Signature = new byte[0x200],
				Issuer = "Root-CA00000003-XS00000020",
				FormatVersion = 2,
				RightsId = title.MainNca.Header.RightsId,
				TitleKeyBlock = title.MainNca.TitleKey,
				CryptoType = title.MainNca.Header.CryptoType2,
				SectHeaderOffset = 0x2C0
			};
			var ticketBytes = ticket.GetBytes();
			builder.AddFile($"{ticket.RightsId.ToHexString()}.tik", new StorageFile(new MemoryStorage(ticketBytes), OpenMode.Read));

			var thisAssembly = Assembly.GetExecutingAssembly();
			var cert = thisAssembly.GetManifestResourceStream("hactoolnet.CA00000003_XS00000020");
			builder.AddFile($"{ticket.RightsId.ToHexString()}.cert", new StreamFile(cert, OpenMode.Read));


			using (var outStream = new FileStream(nspFilename, FileMode.Create, FileAccess.ReadWrite))
			{
				builder.Build(PartitionFileSystemType.Standard).CopyToStream(outStream);
			}
		}

		public static void GetTitlekey(PartitionFileSystem pfs, Keyset keyset, Output Out)
		{
			IDirectory sourceRoot = pfs.OpenDirectory("/", OpenDirectoryMode.All);
			IFileSystem sourceFs = sourceRoot.ParentFileSystem;
			foreach (var entry in FileIterator(sourceRoot))
			{
				if (entry.Name.EndsWith(".tik"))
				{
					using (var TicketFile = sourceFs.OpenFile(entry.Name, OpenMode.Read).AsStream())
					{
						TitleKeyTools.ExtractKey(TicketFile, entry.Name, keyset, Out);
					}
				}
			}
		}

		public static void ExtractTickets(PartitionFileSystem pfs, string outDirPath, Keyset keyset, Output Out)
		{
			var OutDirFs = new LocalFileSystem(outDirPath);
			IDirectory sourceRoot = pfs.OpenDirectory("/", OpenDirectoryMode.All);
			IDirectory destRoot = OutDirFs.OpenDirectory("/", OpenDirectoryMode.All);
			IFileSystem sourceFs = sourceRoot.ParentFileSystem;
			IFileSystem destFs = destRoot.ParentFileSystem;

			foreach (var entry in FileIterator(sourceRoot))
			{
				if (entry.Name.EndsWith(".tik") || entry.Name.EndsWith(".cert"))
				{
					destFs.CreateFile(entry.Name, entry.Size, CreateFileOptions.None);
					using (IFile srcFile = sourceFs.OpenFile(entry.Name, OpenMode.Read))
					using (IFile dstFile = destFs.OpenFile(entry.Name, OpenMode.Write))
					{
						srcFile.CopyTo(dstFile);
					}
				}
			}
		}

	}
}
using System.IO;
using System.Reflection;
using System.Text;
using LibHac;
using LibHac.IO;
using nsZip.LibHacExtensions;

namespace nsZip.LibHacControl
{
	internal static class ProcessNsp
	{

		public static void Process(string inFile, string outDirPath, Output Out)
		{
			using (var file = new FileStream(inFile, FileMode.Open, FileAccess.Read))
			{
				var pfs = new PartitionFileSystem(file.AsStorage());
				Out.Print(pfs.Print());
				pfs.Extract(outDirPath);
			}
		}

		public static void Decompress(string inFile, string outDirPath, Output Out)
		{
			using (var file = new FileStream(inFile, FileMode.Open, FileAccess.Read))
			{
				var pfs = new PartitionFileSystem(file.AsStorage());
				Out.Print(pfs.Print());
				DecompressFs.ProcessFs(pfs, outDirPath, Out);				
			}
		}

		public static void Decrypt(PartitionFileSystem pfs, string outDirPath, bool verifyBeforeDecrypting, Keyset keyset, Output Out)
		{
			Out.Print(pfs.Print());
			ProcessNsp.GetTitlekey(pfs, keyset, Out);
			var OutDirFs = new LocalFileSystem(outDirPath);
			IDirectory sourceRoot = pfs.OpenDirectory("/", OpenDirectoryMode.All);
			IDirectory destRoot = OutDirFs.OpenDirectory("/", OpenDirectoryMode.All);
			IFileSystem sourceFs = sourceRoot.ParentFileSystem;
			IFileSystem destFs = destRoot.ParentFileSystem;			

			foreach (DirectoryEntry entry in sourceRoot.Read())
			{
				if (entry.Type == DirectoryEntryType.Directory)
				{
					throw new InvalidDataException(
						"Error: Directory inside NSP!\r\n" +
						"Please report this as there are curently no known NSP containing a directory.");
				}

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

			var builder = new Pfs0Builder();

			foreach (var nca in title.Ncas)
			{
				builder.AddFile(nca.Filename, nca.GetStorage().AsStream());
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
			builder.AddFile($"{ticket.RightsId.ToHexString()}.tik", new MemoryStream(ticketBytes));

			var thisAssembly = Assembly.GetExecutingAssembly();
			var cert = thisAssembly.GetManifestResourceStream("hactoolnet.CA00000003_XS00000020");
			builder.AddFile($"{ticket.RightsId.ToHexString()}.cert", cert);


			using (var outStream = new FileStream(nspFilename, FileMode.Create, FileAccess.ReadWrite))
			{
				builder.Build(outStream, logger);
			}
		}

		public static void GetTitlekey(PartitionFileSystem pfs, Keyset keyset, Output Out)
		{
			IDirectory sourceRoot = pfs.OpenDirectory("/", OpenDirectoryMode.All);
			IFileSystem sourceFs = sourceRoot.ParentFileSystem;
			foreach (DirectoryEntry entry in sourceRoot.Read())
			{
				if (entry.Type == DirectoryEntryType.Directory)
				{
					throw new InvalidDataException(
						"Error: Directory inside NSP!\r\n" +
						"Please report this as there are curently no known NSP containing a directory.");
				}

				if (entry.Name.EndsWith(".tik"))
				{
					using (var TicketFile = sourceFs.OpenFile(entry.Name, OpenMode.Read).AsStream())
					{
						TitleKeyTools.ExtractKey(TicketFile, entry.Name, keyset, Out);
					}
				}
			}
		}
	}
}
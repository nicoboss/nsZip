using System.Collections.Generic;
using System.IO;
using System.Text;
using LibHac;
using LibHac.IO;
using nsZip.LibHacExtensions;

namespace nsZip.LibHacControl
{
	internal static class ProcessXci
	{
		enum XciTaskType
		{
			extract,
			decrypt,
			extractRomFS
		}

		public static void Extract(string inputFilePath, string outDirPath, Keyset keyset, Output Out)
		{
			Process(inputFilePath, outDirPath, XciTaskType.extract, keyset, Out);
		}

		public static void Decrypt(string inputFilePath, string outDirPath, bool verifyBeforeDecrypting, Keyset keyset, Output Out)
		{
			Process(inputFilePath, outDirPath, XciTaskType.decrypt, keyset, Out, verifyBeforeDecrypting);
		}

		public static void ExtractRomFS(string inputFilePath, string outDirPath, Keyset keyset, Output Out)
		{
			Process(inputFilePath, outDirPath, XciTaskType.extractRomFS, keyset, Out);
		}

		private static void Process(string inputFilePath, string outDirPath, XciTaskType taskType, Keyset keyset, Output Out, bool verifyBeforeDecrypting = true)
		{
			using (var inputFile = File.Open(inputFilePath, FileMode.Open, FileAccess.Read).AsStorage())
			using (var outputFile = File.Open($"{outDirPath}/xciMeta.dat", FileMode.Create))
			{
				var OutDirFs = new LocalFileSystem(outDirPath);
				IDirectory destRoot = OutDirFs.OpenDirectory("/", OpenDirectoryMode.All);
				IFileSystem destFs = destRoot.ParentFileSystem;

				var header = new byte[] { 0x6e, 0x73, 0x5a, 0x69, 0x70, 0x4d, 0x65, 0x74, 0x61, 0x58, 0x43, 0x49, 0x00 };
				outputFile.Write(header, 0, header.Length);

				var xci = new Xci(keyset, inputFile);
				var xciHeaderData = new byte[0x200];
				var xciCertData = new byte[0x200];
				inputFile.Read(xciHeaderData, 0);
				inputFile.Read(xciCertData, 0x7000);
				outputFile.Write(xciHeaderData, 0, 0x200);
				outputFile.Write(xciCertData, 0, 0x200);

				Out.Log(Print.PrintXci(xci));

				var root = xci.OpenPartition(XciPartitionType.Root);
				if (root == null)
				{
					throw new InvalidDataException("Could not find root partition");
				}

				ProcessXci.GetTitleKeys(xci, keyset, Out);
				foreach (var sub in root.Files)
				{
					outputFile.WriteByte(0x0A);
					outputFile.WriteByte(0x0A);
					var subDirNameChar = Encoding.ASCII.GetBytes(sub.Name);
					outputFile.Write(subDirNameChar, 0, subDirNameChar.Length);
					var subPfs = new PartitionFileSystem(new FileStorage(root.OpenFile(sub, OpenMode.Read)));
					foreach (var subPfsFile in subPfs.Files)
					{
						outputFile.WriteByte(0x0A);
						var subPfsFileNameChar = Encoding.ASCII.GetBytes(subPfsFile.Name);
						outputFile.Write(subPfsFileNameChar, 0, subPfsFileNameChar.Length);
						using (IFile srcFile = subPfs.OpenFile(subPfsFile.Name, OpenMode.Read))
						{
							if (taskType == XciTaskType.extractRomFS && subPfsFile.Name.EndsWith(".nca"))
							{
								var fullOutDirPath = $"{outDirPath}/{sub.Name}/{subPfsFile.Name}";
								Out.Log($"Extracting {subPfsFile.Name}...\r\n");
								ProcessNca.Extract(srcFile.AsStream(), fullOutDirPath, verifyBeforeDecrypting, keyset, Out);
							}
							else
							{
								var destFileName = Path.Combine(sub.Name, subPfsFile.Name);
								if (!destFs.DirectoryExists(sub.Name))
								{
									destFs.CreateDirectory(sub.Name);
								}
								destFs.CreateFile(destFileName, subPfsFile.Size, CreateFileOptions.None);
								using (IFile dstFile = destFs.OpenFile(destFileName, OpenMode.Write))
								{
									if (taskType == XciTaskType.decrypt && subPfsFile.Name.EndsWith(".nca"))
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
					}
				}

				outputFile.WriteByte(0x0A);
				outputFile.Dispose();
			}
		}

		private static void ExtractRoot(IFile inputFileBase, IFileSystem destFs, Keyset keyset, Output Out)
		{
			using (var inputFile = new FilePositionStorage(inputFileBase))
			{
				var xci = new Xci(keyset, inputFile);
				ProcessXci.GetTitleKeys(xci, keyset, Out);
				var root = xci.OpenPartition(XciPartitionType.Root);
				if (root == null)
				{
					throw new InvalidDataException("Could not find root partition");
				}
				foreach (var sub in root.Files)
				{
					using (IFile srcFile = root.OpenFile(sub.Name, OpenMode.Read))
					{
						destFs.CreateFile(sub.Name, srcFile.GetSize(), CreateFileOptions.None);
						using (IFile dstFile = destFs.OpenFile(sub.Name, OpenMode.Write))
						{
							srcFile.CopyTo(dstFile);
						}
					}
				}
			}
		}

		public static IEnumerable<(PartitionFileSystem subPfs, PartitionFileEntry subPfsFile)> FileIterator(Xci xci, Keyset keyset, Output Out)
		{
			var root = xci.OpenPartition(XciPartitionType.Root);
			if (root == null)
			{
				throw new InvalidDataException("Could not find root partition");
			}

			foreach (var sub in root.Files)
			{
				var subPfs = new PartitionFileSystem(new FileStorage(root.OpenFile(sub, OpenMode.Read)));
				foreach (var subPfsFile in subPfs.Files)
				{
					yield return (subPfs, subPfsFile);
				}
			}
		}

		public static void GetTitleKeys(Xci xci, Keyset keyset, Output Out)
		{
			foreach (var item in FileIterator(xci, keyset, Out))
			{
				var fileName = item.subPfsFile.Name;
				if (fileName.EndsWith(".tik"))
				{
					using (var TicketFile = item.subPfs.OpenFile(fileName, OpenMode.Read).AsStream())
					{
						TitleKeyTools.ExtractKey(TicketFile, fileName, keyset, Out);
					}
				}
			}
		}

		public static void ExtractTickets(Xci xci, string outDirPath, Keyset keyset, Output Out)
		{
			var OutDirFs = new LocalFileSystem(outDirPath);
			IDirectory destRoot = OutDirFs.OpenDirectory("/", OpenDirectoryMode.All);
			IFileSystem destFs = destRoot.ParentFileSystem;

			foreach (var entry in FileIterator(xci, keyset, Out))
			{
				var fileName = entry.subPfsFile.Name;
				Out.Log($"{fileName}\r\n");
				if (fileName.EndsWith(".tik") || fileName.EndsWith(".cert"))
				{
					destFs.CreateFile(fileName, entry.subPfsFile.Size, CreateFileOptions.None);
					using (IFile srcFile = entry.subPfs.OpenFile(fileName, OpenMode.Read))
					using (IFile dstFile = destFs.OpenFile(fileName, OpenMode.Write))
					{
						srcFile.CopyTo(dstFile);
					}
				}
			}
		}
	}
}
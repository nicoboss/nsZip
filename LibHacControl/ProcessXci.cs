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

		public static void Extract(string inputFilePath, string outDirPath, Keyset keyset, Output Out)
		{
			Process(inputFilePath, outDirPath, false, true, keyset, Out);
		}

		public static void Decrypt(string inputFilePath, string outDirPath, bool verifyBeforeDecrypting, Keyset keyset, Output Out)
		{
			Process(inputFilePath, outDirPath, true, false, keyset, Out, verifyBeforeDecrypting);
		}

		private static void Process(string inputFilePath, string outDirPath, bool decrypt, bool folderStructure, Keyset keyset, Output Out, bool verifyBeforeDecrypting = true)
		{
			using (var inputFile = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read))
			using (var outputFile = File.Open($"{outDirPath}/xciMeta.dat", FileMode.Create))
			{
				var OutDirFs = new LocalFileSystem(outDirPath);
				IDirectory destRoot = OutDirFs.OpenDirectory("/", OpenDirectoryMode.All);
				IFileSystem destFs = destRoot.ParentFileSystem;

				var header = new byte[] { 0x6e, 0x73, 0x5a, 0x69, 0x70, 0x4d, 0x65, 0x74, 0x61, 0x58, 0x43, 0x49, 0x00 };
				outputFile.Write(header, 0, header.Length);

				var xci = new Xci(keyset, inputFile.AsStorage());
				var xciHeaderData = new byte[0x400];
				inputFile.Read(xciHeaderData, 0, 0x400);
				outputFile.Write(xciHeaderData, 0, 0x400);

				Out.Log(Print.PrintXci(xci));

				var root = xci.RootPartition;
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

						var destFileName = folderStructure ? $"{sub.Name}/{subPfsFile.Name}" : subPfsFile.Name;
						destFs.CreateFile(destFileName, subPfsFile.Size, CreateFileOptions.None);
						using (IFile srcFile = subPfs.OpenFile(subPfsFile.Name, OpenMode.Read))
						using (IFile dstFile = destFs.OpenFile(destFileName, OpenMode.Write))
						{
							if (decrypt && subPfsFile.Name.EndsWith(".nca"))
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

				outputFile.WriteByte(0x0A);
				outputFile.Dispose();
			}
		}

		public static IEnumerable<(PartitionFileSystem subPfs, PartitionFileEntry subPfsFile)> FileIterator(Xci xci, Keyset keyset, Output Out)
		{
			var root = xci.RootPartition;
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
using System.IO;
using System.Text;
using LibHac;
using LibHac.Fs;
using nsZip.LibHacExtensions;

namespace nsZip.LibHacControl
{
	internal static class ProcessXci
	{
		public static void Decrypt(string inputFilePath, string outDirPath, bool verifyBeforeDecrypting, Keyset keyset, Output Out)
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

				Out.Print(Print.PrintXci(xci));

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
					var subDirName = Encoding.ASCII.GetBytes(sub.Name);
					outputFile.Write(subDirName, 0, subDirName.Length);
					var subPfs = new PartitionFileSystem(new FileStorage(root.OpenFile(sub, OpenMode.Read)));
					foreach (var subPfsFile in subPfs.Files)
					{
						outputFile.WriteByte(0x0A);
						var subPfsFileName = Encoding.ASCII.GetBytes(subPfsFile.Name);
						outputFile.Write(subPfsFileName, 0, subPfsFileName.Length);

						destFs.CreateFile(subPfsFile.Name, subPfsFile.Size, CreateFileOptions.None);
						using (IFile srcFile = subPfs.OpenFile(subPfsFile.Name, OpenMode.Read))
						using (IFile dstFile = destFs.OpenFile(subPfsFile.Name, OpenMode.Write))
						{
							if (subPfsFile.Name.EndsWith(".nca"))
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

		public static void GetTitleKeys(Xci xci, Keyset keyset, Output Out)
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
					if (subPfsFile.Name.EndsWith(".tik"))
					{
						using (var TicketFile = subPfs.OpenFile(subPfsFile.Name, OpenMode.Read).AsStream())
						{
							TitleKeyTools.ExtractKey(TicketFile, subPfsFile.Name, keyset, Out);
						}
					}
				}
			}
		}
	}
}
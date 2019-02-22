using System.IO;
using System.Text;
using LibHac;
using LibHac.IO;

namespace nsZip.LibHacControl
{
	internal static class ProcessXci
	{
		public static void Process(string inFile, string outDirPath, Keyset keyset, Output Out)
		{
			using (var file = new FileStream(inFile, FileMode.Open, FileAccess.Read))
			{
				var outputFile = File.Open($"{outDirPath}/xciMeta.dat", FileMode.Create);
				var header = new byte[] {0x6e, 0x73, 0x5a, 0x69, 0x70, 0x4d, 0x65, 0x74, 0x61, 0x58, 0x43, 0x49, 0x00};
				outputFile.Write(header, 0, header.Length);

				var xci = new Xci(keyset, file.AsStorage());
				var xciHeaderData = new byte[0x400];
				file.Read(xciHeaderData, 0, 0x400);
				outputFile.Write(xciHeaderData, 0, 0x400);

				Out.Print(Print.PrintXci(xci));

				if (xci.RootPartition != null)
				{
					var root = xci.RootPartition;
					if (root == null)
					{
						Out.Print("Could not find root partition");
						return;
					}

					foreach (var sub in root.Files)
					{
						outputFile.WriteByte(0x0A);
						outputFile.WriteByte(0x0A);
						var subDirName = Encoding.ASCII.GetBytes(sub.Name);
						outputFile.Write(subDirName, 0, subDirName.Length);
						var subPfs = new PartitionFileSystem(new FileStorage(root.OpenFile(sub, OpenMode.Read)));
						var subDir = Path.Combine(outDirPath, sub.Name);
						foreach (var subPfsFile in subPfs.Files)
						{
							outputFile.WriteByte(0x0A);
							var subPfsFileName = Encoding.ASCII.GetBytes(subPfsFile.Name);
							outputFile.Write(subPfsFileName, 0, subPfsFileName.Length);
						}

						subPfs.Extract(outDirPath);
					}

					outputFile.WriteByte(0x0A);
				}

				outputFile.Dispose();
			}
		}
	}
}
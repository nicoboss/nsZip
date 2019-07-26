using System;
using System.Collections.Generic;
using System.IO;
using LibHac;
using LibHac.IO;

namespace nsZip.LibHacExtensions
{
	public static class FolderTools
	{
		public static void FolderToNSP(string inFolder, string nspFile)
		{
			using (var outfile = new FileStream(nspFile, FileMode.Create, FileAccess.Write))
			{
				var inFolderFs = new LocalFileSystem(inFolder);
				var newNSP = new PartitionFileSystemBuilder(inFolderFs);
				newNSP.Build(PartitionFileSystemType.Standard).CopyToStream(outfile);
			}
		}

		public static void FolderToXCI(string inFolder, string nspFile)
		{
			using (var outfile = new FileStream(nspFile, FileMode.Create, FileAccess.Write))
			{
				var inFolderFs = new LocalFileSystem(inFolder);
				var newNSP = new PartitionFileSystemBuilder(inFolderFs);
				newNSP.Build(PartitionFileSystemType.Hashed).CopyToStream(outfile);
			}
		}

		public static void ExtractTitlekeys(string inFolder, Keyset keyset, Output Out)
		{
			var dirExtracted = new DirectoryInfo(inFolder);
			var TikFiles = dirExtracted.GetFiles("*.tik");
			foreach (var file in TikFiles)
			{
				using (var TicketFile = File.Open($"{inFolder}/{file.Name}", FileMode.Open))
				{
					TitleKeyTools.ExtractKey(TicketFile, file.Name, keyset, Out);
				}
			}
		}
	}
}
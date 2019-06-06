using System.Collections.Generic;
using System.IO;
using LibHac;

namespace nsZip.LibHacExtensions
{
	public static class FolderTools
	{
		public static void FolderToNSP(string inFolder, string nspFile)
		{
			var newNSP = new Pfs0Builder();
			var dirEncrypted = new DirectoryInfo(inFolder);
			var fileStreamList = new List<FileStream>();
			foreach (var file in dirEncrypted.GetFiles())
			{
				var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
				fileStreamList.Add(fs);
				newNSP.AddFile(file.Name, fs);
			}

			var outfile = new FileStream(nspFile, FileMode.Create, FileAccess.Write);
			newNSP.Build(outfile);

			foreach (var fs in fileStreamList)
			{
				fs.Dispose();
			}

			outfile.Dispose();
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
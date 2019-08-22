using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LibHac;
using LibHac.IO;
using nsZip.LibHacExtensions;
using Zstandard.Net;

namespace nsZip
{
	public static class DecompressFs
	{

		public static void ProcessFs(IFileSystem sourceFs, IFileSystem destFs, Output Out)
		{
			foreach (var file in sourceFs.EnumerateEntries().Where(item => item.Type == DirectoryEntryType.File))
			{
				using (IFile srcFile = sourceFs.OpenFile(file.FullPath, OpenMode.Read))
				using (var decStorage = new DecompressionStorage(srcFile))
				{
					var destName = $"{file.Name.Substring(0, file.Name.LastIndexOf('.'))}.nca";
					using (IFile outputFile = FolderTools.createAndOpen(file, destFs, destName, decStorage.GetSize()))
					{

						decStorage.CopyTo(outputFile.AsStorage());
					}
				}
			}
		}

		public static void GetTitleKeys(IFileSystem sourceFs, Keyset keyset, Output Out)
		{
			foreach (var entry in sourceFs.EnumerateEntries().Where(item => item.Type == DirectoryEntryType.File))
			{
				if (entry.Name.EndsWith(".tik.nsz"))
				{
					using (IFile srcFile = sourceFs.OpenFile(entry.Name, OpenMode.Read))
					using (var decStorage = new DecompressionStorage(srcFile))
					{
						TitleKeyTools.ExtractKey(decStorage.AsStream(), entry.Name, keyset, Out);
					}
				}
			}
		}

		public static void ExtractTickets(IFileSystem sourceFs, string outDirPath, Keyset keyset, Output Out)
		{
			var OutDirFs = new LocalFileSystem(outDirPath);
			IDirectory destRoot = OutDirFs.OpenDirectory("/", OpenDirectoryMode.All);

			foreach (var entry in sourceFs.EnumerateEntries().Where(item => item.Type == DirectoryEntryType.File))
			{
				if (entry.Name.EndsWith(".tik.nsz") || entry.Name.EndsWith(".cert.nsz"))
				{
					var outFilePath = Path.Combine(outDirPath, Path.GetFileNameWithoutExtension(entry.Name));
					using (IFile srcFile = sourceFs.OpenFile(entry.Name, OpenMode.Read))
					using (var decStorage = new DecompressionStorage(srcFile))
					using (FileStream outputFile = File.OpenWrite(outFilePath))
					{;
						decStorage.CopyToStream(outputFile);
					}
				}
			}
		}
	}
}
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using LibHac;
using LibHac.IO;
using Zstandard.Net;

namespace nsZip
{
	public static class DecompressFs
	{

		public static IEnumerable<DirectoryEntry> FileIterator(IFileSystem sourceFs)
		{
			IDirectory sourceRoot = sourceFs.OpenDirectory("/", OpenDirectoryMode.All);
			foreach (var entry in sourceRoot.Read())
			{
				if (entry.Type == DirectoryEntryType.Directory)
				{
					throw new InvalidDataException("Error: Directory inside NSPZ/XCIZ!");
				}

				yield return entry;
			}
		}

		public static void ProcessFs(IFileSystem sourceFs, string outDirPath, Output Out)
		{
			foreach (var entry in FileIterator(sourceFs))
			{
				var outFilePath = Path.Combine(outDirPath, Path.GetFileNameWithoutExtension(entry.Name));
				using (IFile srcFile = sourceFs.OpenFile(entry.Name, OpenMode.Read))
				using (var decStorage = new DecompressionStorage(srcFile))
				using (FileStream outputFile = File.OpenWrite(outFilePath))
				{
					decStorage.CopyToStream(outputFile);
				}
			}
		}

		public static void GetTitleKeys(IFileSystem sourceFs, Keyset keyset, Output Out)
		{
			foreach (var entry in FileIterator(sourceFs))
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

			foreach (var entry in FileIterator(sourceFs))
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
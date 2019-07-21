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
				using (FileStream outputFile = File.OpenWrite(outFilePath))
				{
					ProcessFile(srcFile, outputFile, entry, Out);
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
					using (var dstStream = new MemoryStream())
					{
						ProcessFile(srcFile, dstStream, entry, Out);
						TitleKeyTools.ExtractKey(dstStream, entry.Name, keyset, Out);
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
					using (FileStream outputFile = File.OpenWrite(outFilePath))
					{
						ProcessFile(srcFile, outputFile, entry, Out);
					}
				}
			}
		}

		public static void ProcessFile(IFile inputFileObj, Stream outputFile, DirectoryEntry inputFileEntry, Output Out)
		{
			var inputFile = inputFileObj.AsStream();
			var nsZipMagic = new byte[] { 0x6e, 0x73, 0x5a, 0x69, 0x70 };
			var nsZipMagicEncrypted = new byte[5];
			inputFile.Read(nsZipMagicEncrypted, 0, 5);
			var nsZipMagicRandomKey = new byte[5];
			inputFile.Read(nsZipMagicRandomKey, 0, 5);
			Util.XorArrays(nsZipMagicEncrypted, nsZipMagicRandomKey);
			if (!Util.ArraysEqual(nsZipMagicEncrypted, nsZipMagic))
			{
				Out.Warn($"Invalid magic: Skipping {inputFileEntry.Name}\r\n");
				return;
			}

			var version = inputFile.ReadByte();
			var type = inputFile.ReadByte();
			var bsArray = new byte[5];
			inputFile.Read(bsArray, 0, 5);
			long bsReal = (bsArray[0] << 32)
						  + (bsArray[1] << 24)
						  + (bsArray[2] << 16)
						  + (bsArray[3] << 8)
						  + bsArray[4];
			if (bsReal > int.MaxValue)
			{
				throw new NotImplementedException("Block sizes above 2 GB aren't supported yet!");
			}

			var bs = (int)bsReal;
			var amountOfBlocksArray = new byte[4];
			inputFile.Read(amountOfBlocksArray, 0, 4);
			var amountOfBlocks = (amountOfBlocksArray[0] << 24)
								 + (amountOfBlocksArray[1] << 16)
								 + (amountOfBlocksArray[2] << 8)
								 + amountOfBlocksArray[3];
			var sizeOfSize = (int)Math.Ceiling(Math.Log(bs, 2) / 8);
			var perBlockHeaderSize = sizeOfSize + 1;

			var compressionAlgorithm = new int[amountOfBlocks];
			var compressedBlockSize = new int[amountOfBlocks];
			for (var currentBlockID = 0; currentBlockID < amountOfBlocks; ++currentBlockID)
			{
				compressionAlgorithm[currentBlockID] = inputFile.ReadByte();
				compressedBlockSize[currentBlockID] = 0;
				for (var j = 0; j < sizeOfSize; ++j)
				{
					compressedBlockSize[currentBlockID] += inputFile.ReadByte() << ((sizeOfSize - j - 1) * 8);
				}
			}

			var outBuff = new byte[bs];
			for (var currentBlockID = 0; currentBlockID < amountOfBlocks; ++currentBlockID)
			{
				switch (compressionAlgorithm[currentBlockID])
				{
					case 0:
						var rawBS = compressedBlockSize[currentBlockID];

						//This safety check doesn't work for the last block and so must be excluded!
						if (rawBS != bs && currentBlockID < amountOfBlocks - 1)
						{
							throw new FormatException("NSZ header seems to be corrupted!");
						}

						inputFile.Read(outBuff, 0, rawBS);
						outputFile.Write(outBuff, 0, rawBS);
						break;
					case 1:
						var inBuff = new byte[compressedBlockSize[currentBlockID]];
						inputFile.Read(inBuff, 0, inBuff.Length);
						DecompressBlock(ref inBuff, ref outputFile);
						break;
					default:
						throw new NotImplementedException(
							"The specified compression algorithm isn't implemented yet!");
				}
			}
		}

		private static void DecompressBlock(ref byte[] input, ref Stream output)
		{
			// decompress
			using (var memoryStream = new MemoryStream(input))
			using (var decompressionStream = new ZstandardStream(memoryStream, CompressionMode.Decompress))
			{
				decompressionStream.CopyTo(output);
			}
		}
	}
}
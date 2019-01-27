using System;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;
using LibHac;
using Zstandard.Net;

namespace nsZip
{
	internal class DecompressFolder
	{
		private readonly RichTextBox DebugOutput;
		private readonly string folderPath;

		private DecompressFolder(RichTextBox debugOutputArg, string folderPathArg)
		{
			DebugOutput = debugOutputArg;
			folderPath = folderPathArg;
			DecompressFunct();
		}

		public static void Decompress(RichTextBox debugOutputArg, string folderPathArg)
		{
			new DecompressFolder(debugOutputArg, folderPathArg);
		}

		private void DecompressFunct()
		{
			var dirDecrypted = new DirectoryInfo(folderPath);
			foreach (var file in dirDecrypted.GetFiles())
			{
				var inputFile = File.Open(file.FullName, FileMode.Open);

				var nsZipMagic = new byte[] {0x6e, 0x73, 0x5a, 0x69, 0x70};
				var nsZipMagicEncrypted = new byte[5];
				inputFile.Read(nsZipMagicEncrypted, 0, 5);
				var nsZipMagicRandomKey = new byte[5];
				inputFile.Read(nsZipMagicRandomKey, 0, 5);
				Util.XorArrays(nsZipMagicEncrypted, nsZipMagicRandomKey);
				if (!Util.ArraysEqual(nsZipMagicEncrypted, nsZipMagic))
				{
					DebugOutput.AppendText($"Invalid magic: Skipping {file}\r\n");
					continue;
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

				var bs = (int) bsReal;
				var amountOfBlocksArray = new byte[4];
				inputFile.Read(amountOfBlocksArray, 0, 4);
				var amountOfBlocks = (amountOfBlocksArray[0] << 24)
				                     + (amountOfBlocksArray[1] << 16)
				                     + (amountOfBlocksArray[2] << 8)
				                     + amountOfBlocksArray[3];
				var sizeOfSize = (int) Math.Ceiling(Math.Log(bs, 2) / 8);
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

				var outputFile = File.Open($"NSZ/{Path.GetFileNameWithoutExtension(file.Name)}", FileMode.Create);
				var outBuff = new byte[bs];
				for (var currentBlockID = 0; currentBlockID < amountOfBlocks; ++currentBlockID)
				{
					switch (compressionAlgorithm[currentBlockID])
					{
						case 0:
							if (bs != compressedBlockSize[currentBlockID])
							{
								throw new FormatException("NSZ header seems to be corrupted!");
							}

							inputFile.Read(outBuff, 0, bs);
							outputFile.Write(outBuff, 0, bs);
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

				inputFile.Dispose();
				outputFile.Dispose();
			}
		}

		private void DecompressBlock(ref byte[] input, ref FileStream output)
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
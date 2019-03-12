using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.Tasks;
using LibHac;
using Zstandard.Net;

namespace nsZip
{
	internal class CompressFolder
	{
		private static readonly RNGCryptoServiceProvider secureRNG = new RNGCryptoServiceProvider();
		private readonly int bs;
		private readonly string inFolderPath;
		private readonly Output Out;
		private readonly string outFolderPath;
		private readonly int ZstdLevel;
		private int amountOfBlocks;
		private int currentBlockID;
		private byte[] nsZipHeader;
		private SHA256Cng sha256Compressed;
		private SHA256Cng sha256Header;
		private int sizeOfSize;

		private CompressFolder(Output OutArg, string inFolderPathArg, string outFolderPathArg,
			int bsArg, int ZstdLevelArg)
		{
			bs = bsArg;
			ZstdLevel = ZstdLevelArg;
			Out = OutArg;
			inFolderPath = inFolderPathArg;
			outFolderPath = outFolderPathArg;
			CompressFunct();
		}

		public static void Compress(Output OutArg, string inFolderPathArg, string outFolderPathArg,
			int bsArg, int ZstdLevel)
		{
			new CompressFolder(OutArg, inFolderPathArg, outFolderPathArg, bsArg, ZstdLevel);
		}

		private void CompressFunct()
		{
			var threadsUsedToCompress = Environment.ProcessorCount;
			// To not exceed the 2 GB RAM Limit
			if (!Environment.Is64BitProcess)
			{
				threadsUsedToCompress = Math.Min(16, threadsUsedToCompress);
			}

			var CompressionIO = new byte[104857600];
			var blocksPerChunk = CompressionIO.Length/bs + CompressionIO.Length % bs > 0 ? 1 : 0;
			var dirDecrypted = new DirectoryInfo(inFolderPath);
			foreach (var file in dirDecrypted.GetFiles())
			{
				var doneFlag = false;
				var outputFile = File.Open($"{outFolderPath}/{file.Name}.nsz", FileMode.Create);
				var inputFile = File.Open(file.FullName, FileMode.Open);
				amountOfBlocks = (int) Math.Ceiling((decimal) inputFile.Length / bs);
				sizeOfSize = (int) Math.Ceiling(Math.Log(bs, 2) / 8);
				var perBlockHeaderSize = sizeOfSize + 1;
				var headerSize = 0x15 + perBlockHeaderSize * amountOfBlocks;
				outputFile.Position = headerSize;
				var nsZipMagic = new byte[] {0x6e, 0x73, 0x5a, 0x69, 0x70};
				var nsZipMagicRandomKey = new byte[5];
				secureRNG.GetBytes(nsZipMagicRandomKey);
				Util.XorArrays(nsZipMagic, nsZipMagicRandomKey);
				currentBlockID = 0;
				nsZipHeader = new byte[headerSize];
				Array.Copy(nsZipMagic, 0x00, nsZipHeader, 0x00, 0x05);
				Array.Copy(nsZipMagicRandomKey, 0x00, nsZipHeader, 0x05, 0x05);
				nsZipHeader[0x0A] = 0x00; //Version
				nsZipHeader[0x0B] = 0x01; //Type
				nsZipHeader[0x0C] = (byte) (bs >> 32);
				nsZipHeader[0x0D] = (byte) (bs >> 24);
				nsZipHeader[0x0E] = (byte) (bs >> 16);
				nsZipHeader[0x0F] = (byte) (bs >> 8);
				nsZipHeader[0x10] = (byte) bs;
				nsZipHeader[0x11] = (byte) (amountOfBlocks >> 24);
				nsZipHeader[0x12] = (byte) (amountOfBlocks >> 16);
				nsZipHeader[0x13] = (byte) (amountOfBlocks >> 8);
				nsZipHeader[0x14] = (byte) amountOfBlocks;
				sha256Compressed = new SHA256Cng();


				var maxPos = inputFile.Length;

				do
				{
					var outputLen = new int[blocksPerChunk]; //Filled with 0
					inputFile.Read(CompressionIO, 0, CompressionIO.Length);
					//Parallel.For(0, 400, index => {
					for (int index = 0; index < blocksPerChunk; ++index)
					{
						++currentBlockID;
						if (currentBlockID > amountOfBlocks)
						{
							//Out.Print($"Skip Block: {currentBlockID}\r\n");
							doneFlag = true;
							continue;
						}

						var startPos = index * bs;
						var blockSize = Math.Min(bs, (int)(maxPos - startPos));
						var endPos = startPos + blockSize;

						Out.Print($"{currentBlockID}/{amountOfBlocks} Blocks written\r\n");
						CompressionAlgorithm compressionAlgorithm;
						if (endPos > maxPos)
						{
							outputLen[index] = CompressBlock(ref CompressionIO, startPos, (int)(maxPos - startPos), out compressionAlgorithm);
						}
						else
						{
							outputLen[index] = CompressBlock(ref CompressionIO, startPos, blockSize, out compressionAlgorithm);
						}
						var offset = (currentBlockID - 1) * (sizeOfSize + 1);
						switch (compressionAlgorithm)
						{
							case CompressionAlgorithm.None:
								nsZipHeader[0x15 + offset] = 0x00;
								break;
							case CompressionAlgorithm.Zstandard:
								nsZipHeader[0x15 + offset] = 0x01;
								break;
							case CompressionAlgorithm.LZMA:
								nsZipHeader[0x15 + offset] = 0x02;
								break;
							default:
								throw new ArgumentOutOfRangeException();
						}
						for (var j = 0; j < sizeOfSize; ++j)
						{
							nsZipHeader[0x16 + offset + j] = (byte)(outputLen[index] >> ((sizeOfSize - j - 1) * 8));
						}


					}//});

					for (int index = 0; index < blocksPerChunk; ++index)
					{
						if (outputLen[index] == 0) break;
						var startPos = index * bs;
						sha256Compressed.TransformBlock(CompressionIO, startPos, outputLen[index], null, 0);
						outputFile.Write(CompressionIO, startPos, outputLen[index]);
					}
					
				} while (!doneFlag);

				outputFile.Position = 0;
				outputFile.Write(nsZipHeader, 0, headerSize);
				sha256Header = new SHA256Cng();
				sha256Header.ComputeHash(nsZipHeader);
				var sha256Hash = new byte[0x20];
				Array.Copy(sha256Header.Hash, sha256Hash, 0x20);
				sha256Compressed.TransformFinalBlock(new byte[0], 0, 0);
				Util.XorArrays(sha256Hash, sha256Compressed.Hash);
				outputFile.Seek(0, SeekOrigin.End);
				outputFile.Write(sha256Hash, 0, 0x10);
				outputFile.Dispose();
				inputFile.Dispose();
				//break;
			}
		}



		enum CompressionAlgorithm { None, Zstandard, LZMA };

		private int CompressBlock(ref byte[] input, int startPos, int blockSize, out CompressionAlgorithm compressionAlgorithm)
		{
			// compress
			int outputLen;
			using (var memoryStream = new MemoryStream())
			using (var compressionStream = new ZstandardStream(memoryStream, CompressionMode.Compress))
			{
				compressionStream.CompressionLevel = ZstdLevel;
				compressionStream.Write(input, startPos, blockSize);
				compressionStream.Close();
				var tmp = memoryStream.ToArray();
				outputLen = tmp.Length;
				if (tmp.Length < blockSize)
				{
					compressionAlgorithm = CompressionAlgorithm.Zstandard;
					Array.Copy(tmp, 0, input, startPos, tmp.Length);
				}
				else
				{
					compressionAlgorithm = CompressionAlgorithm.None;
				}
			}

			return outputLen;
		}
	}
}
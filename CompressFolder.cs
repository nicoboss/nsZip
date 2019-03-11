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

			var CompressionIO_1 = new byte[104857600];
			var CompressionIO_2 = new byte[104857600];
			var output = new byte[threadsUsedToCompress][];
			var task = new Task[threadsUsedToCompress];
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
					inputFile.Read(CompressionIO_1, 0, CompressionIO_1.Length);
					//Parallel.For(0, 400, index => {
					for (int index = 0; index < 399; ++index)
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
						if (endPos > maxPos)
						{
							CompressBlock(ref CompressionIO_1, startPos, (int)(maxPos - startPos));
						}
						else
						{
							CompressBlock(ref CompressionIO_1, startPos, blockSize);
						}

					}
					//});

					sha256Compressed.TransformBlock(CompressionIO_1, 0, CompressionIO_1.Length, null, 0);
					outputFile.Write(CompressionIO_1, 0, CompressionIO_1.Length);
				} while (!doneFlag);

				outputFile.Dispose();
				inputFile.Dispose();
				//break;
			}
		}


		private void CompressBlock(ref byte[] input, int startPos, int blockSize)
		{
			// compress
			using (var memoryStream = new MemoryStream())
			using (var compressionStream = new ZstandardStream(memoryStream, CompressionMode.Compress))
			{
				compressionStream.CompressionLevel = ZstdLevel;
				compressionStream.Write(input, startPos, blockSize);
				compressionStream.Close();
				var tmp = memoryStream.ToArray();
				Array.Copy(tmp, 0, input, startPos, tmp.Length);
			}
		}
	}
}
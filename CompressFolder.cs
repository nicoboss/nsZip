using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using LibHac;
using Zstandard.Net;

namespace nsZip
{
	internal class CompressFolder
	{
		private static readonly RNGCryptoServiceProvider secureRNG = new RNGCryptoServiceProvider();
		private int amountOfBlocks;
		private readonly int bs;
		private int currentBlockID;
		private readonly RichTextBox DebugOutput;
		private readonly string folderPath;
		private byte[] nsZipHeader;
		private int sizeOfSize;
		private SHA256 sha256Compressed;
		private SHA256 sha256Header;

		private CompressFolder(RichTextBox debugOutputArg, string folderPathArg, int bsArg = 262144)
		{
			bs = bsArg;
			DebugOutput = debugOutputArg;
			folderPath = folderPathArg;
			CompressFunct();
		}

		public static void Compress(RichTextBox debugOutputArg, string folderPathArg, int bsArg = 262144)
		{
			new CompressFolder(debugOutputArg, folderPathArg, bsArg);
		}

		private void CompressFunct()
		{
			var threadsUsedToCompress = Environment.ProcessorCount;
			// To not exceed the 2 GB RAM Limit
			if (!Environment.Is64BitProcess)
			{
				threadsUsedToCompress = Math.Min(16, threadsUsedToCompress);
			}

			var input = Utils.CreateJaggedArray<byte[][]>(threadsUsedToCompress, bs);
			var output = new byte[threadsUsedToCompress][];
			var task = new Task[threadsUsedToCompress];
			var dirDecrypted = new DirectoryInfo(folderPath);

			foreach (var file in dirDecrypted.GetFiles())
			{
				var outputFile = File.Open($"NSZ/{file.Name}.nsz", FileMode.Create);
				var inputFile = File.Open(file.FullName, FileMode.Open);
				amountOfBlocks = (int) Math.Ceiling((decimal) inputFile.Length / bs);
				sizeOfSize = (int) Math.Ceiling(Math.Log(bs, 2) / 8);
				var perBlockHeaderSize = sizeOfSize + 1;
				var headerSize = 0x15 + perBlockHeaderSize * amountOfBlocks;
				outputFile.Position = headerSize;
				var breakCondition = -1;
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
				nsZipHeader[0x11] = (byte)(amountOfBlocks >> 24);
				nsZipHeader[0x12] = (byte)(amountOfBlocks >> 16);
				nsZipHeader[0x13] = (byte)(amountOfBlocks >> 8);
				nsZipHeader[0x14] = (byte)amountOfBlocks;
				sha256Compressed = SHA256.Create();
				

				while (true)
				{
					for (var i = 0; i < threadsUsedToCompress; ++i)
					{
						var iNow = i;
						if (task[iNow] != null)
						{
							task[iNow].Wait();
							WriteBlock(outputFile, input[iNow], output[iNow], false);
							task[iNow] = null;
						}

						if (breakCondition > -1)
						{
							if (iNow == breakCondition)
							{
								goto LastBlock;
							}
						}
						else if (inputFile.Position + input[iNow].Length >= inputFile.Length)
						{
							breakCondition = iNow;
						}
						else
						{
							inputFile.Read(input[iNow], 0, input[iNow].Length);
							task[iNow] = Task.Factory.StartNew(() => CompressBlock(ref input[iNow], ref output[iNow]));
						}
					}
				}

				LastBlock:
				var lastBlockInput = new byte[inputFile.Length - inputFile.Position];
				byte[] lastBlockOutput = null;
				inputFile.Read(lastBlockInput, 0, lastBlockInput.Length);
				CompressBlock(ref lastBlockInput, ref lastBlockOutput);
				WriteBlock(outputFile, lastBlockInput, lastBlockOutput, true);
				outputFile.Position = 0;
				outputFile.Write(nsZipHeader, 0, headerSize);
				sha256Header = SHA256.Create();
				sha256Header.ComputeHash(nsZipHeader);
				var sha256Hash = new byte[0x20];
				Array.Copy(sha256Header.Hash, sha256Hash, 0x20);
				Util.XorArrays(sha256Hash, sha256Compressed.Hash);
				outputFile.Seek(0, SeekOrigin.End);
				outputFile.Write(sha256Hash, 0, 0x10);
				inputFile.Dispose();
				outputFile.Dispose();
			}
		}

		private void WriteBlock(FileStream outputFile, byte[] input, byte[] output, bool lastBlock)
		{
			var offset = currentBlockID * (sizeOfSize + 1);
			var inputLen = input.Length;
			var outputLen = output.Length;
			if (outputLen >= inputLen)
			{
				nsZipHeader[0x15 + offset] = 0x00;
				for (var j = 0; j < sizeOfSize; ++j)
				{
					nsZipHeader[0x16 + offset + j] = (byte) (inputLen >> ((sizeOfSize - j - 1) * 8));
				}
				outputFile.Write(input, 0, inputLen);
				if (lastBlock)
				{
					sha256Compressed.TransformFinalBlock(input, 0, inputLen);
				}
				else
				{
					sha256Compressed.TransformBlock(input, 0, inputLen, input, 0);
				}
			}
			else
			{
				nsZipHeader[0x15 + offset] = 0x01;
				for (var j = 0; j < sizeOfSize; ++j)
				{
					nsZipHeader[0x16 + offset + j] = (byte) (outputLen >> ((sizeOfSize - j - 1) * 8));
				}
				outputFile.Write(output, 0, outputLen);
				if (lastBlock)
				{
					sha256Compressed.TransformFinalBlock(output, 0, outputLen);
				}
				else
				{
					sha256Compressed.TransformBlock(output, 0, outputLen, output, 0);
				}
				
			}

			DebugOutput.AppendText($"{currentBlockID + 1}/{amountOfBlocks} Blocks written\r\n");
			++currentBlockID;
		}


		private void CompressBlock(ref byte[] input, ref byte[] output)
		{
			// compress
			using (var memoryStream = new MemoryStream())
			using (var compressionStream = new ZstandardStream(memoryStream, CompressionMode.Compress))
			{
				compressionStream.CompressionLevel = 19;
				compressionStream.Write(input, 0, input.Length);
				compressionStream.Close();
				output = memoryStream.ToArray();
			}
		}
	}
}
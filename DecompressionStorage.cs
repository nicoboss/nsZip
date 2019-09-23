using LibHac;
using LibHac.Fs;
using System;
using System.IO;
using System.IO.Compression;
using Zstandard.Net;

namespace nsZip
{
	class DecompressionStorage : StorageBase
	{
		private int bs;
		private IStorage[] compressedBlocks;
		private int[] compressionAlgorithm;
		private int amountOfBlocks;
		private int lastBlockSize = -1;
		private long length = 0;
		private byte[] decompressBuff;

		public DecompressionStorage(IFile inputFile)
		{
			var inputFileStream = inputFile.AsStream();
			var nsZipMagic = new byte[] { 0x6e, 0x73, 0x5a, 0x69, 0x70 };
			var nsZipMagicEncrypted = new byte[5];
			inputFileStream.Read(nsZipMagicEncrypted, 0, 5);
			var nsZipMagicRandomKey = new byte[5];
			inputFileStream.Read(nsZipMagicRandomKey, 0, 5);
			Util.XorArrays(nsZipMagicEncrypted, nsZipMagicRandomKey);
			if (!Util.ArraysEqual(nsZipMagicEncrypted, nsZipMagic))
			{
				throw new InvalidDataException($"Invalid nsZip magic!\r\n");
			}

			var version = inputFileStream.ReadByte();
			var type = inputFileStream.ReadByte();
			var bsArray = new byte[5];
			inputFileStream.Read(bsArray, 0, 5);
			long bsReal = (bsArray[0] << 32)
						  + (bsArray[1] << 24)
						  + (bsArray[2] << 16)
						  + (bsArray[3] << 8)
						  + bsArray[4];
			if (bsReal > int.MaxValue)
			{
				throw new NotImplementedException("Block sizes above 2 GB aren't supported yet!");
			}

			bs = (int)bsReal;
			var amountOfBlocksArray = new byte[4];
			inputFileStream.Read(amountOfBlocksArray, 0, 4);
			amountOfBlocks = (amountOfBlocksArray[0] << 24)
				+ (amountOfBlocksArray[1] << 16)
				+ (amountOfBlocksArray[2] << 8)
				+ amountOfBlocksArray[3];
			var sizeOfSize = (int)Math.Ceiling(Math.Log(bs, 2) / 8);
			var perBlockHeaderSize = sizeOfSize + 1;

			decompressBuff = new byte[bs];
			compressionAlgorithm = new int[amountOfBlocks];
			var compressedBlockSize = new int[amountOfBlocks];
			var compressedBlockOffset = new long[amountOfBlocks];
			long currentOffset = 0;

			for (var currentBlockID = 0; currentBlockID < amountOfBlocks; ++currentBlockID)
			{
				compressedBlockOffset[currentBlockID] = currentOffset;
				compressionAlgorithm[currentBlockID] = inputFileStream.ReadByte();
				compressedBlockSize[currentBlockID] = 0;
				for (var j = 0; j < sizeOfSize; ++j)
				{
					compressedBlockSize[currentBlockID] += inputFileStream.ReadByte() << ((sizeOfSize - j - 1) * 8);
				}

				currentOffset += compressedBlockSize[currentBlockID];
			}

			compressedBlocks = new IStorage[amountOfBlocks];
			var compressedData = new FileStorage(inputFile).Slice(inputFileStream.Position);
			for(int i = 0; i < amountOfBlocks; ++i)
			{
				compressedBlocks[i] = compressedData.Slice(compressedBlockOffset[i], compressedBlockSize[i]);
			}

			// Cast to long is VERY important or files larger than 2 GB will have a negative size!
			lastBlockSize = getSizeOfLastBlock();
			length = ((long)(amountOfBlocks-1) * bs) + lastBlockSize;
			Console.WriteLine($"length={length} lastBlockSize={lastBlockSize}");
		}

		private int getSizeOfLastBlock()
		{
			if (lastBlockSize > -1)
			{
				return lastBlockSize;
			}

			switch (compressionAlgorithm[amountOfBlocks - 1])
			{
				case 0:
					var rawBS = (int)compressedBlocks[amountOfBlocks - 1].GetSize();
					return rawBS; //DON'T return bs here as the last block will be smaller!
				case 1:
					using (var decompressionStream = new ZstandardStream(compressedBlocks[amountOfBlocks - 1].AsStream(), CompressionMode.Decompress))
					using (var memoryStream = new MemoryStream())
					{
						decompressionStream.CopyTo(memoryStream);
						return (int)memoryStream.Length;
					}
				default:
					throw new NotImplementedException(
						"The specified compression algorithm isn't implemented yet!");
			}
		}

		protected override void ReadImpl(Span<byte> destination, long offset)
		{
			int size = destination.Length;
			var startBlockID = (int)(offset / bs);
			var initialRelativeOffset = (int)(offset % bs);
			var relativeOffset = initialRelativeOffset;
			var maxRelativeBlockID = (int)((relativeOffset + size - 1) / bs);
			//Console.WriteLine($"ReadImpl: startBlockID={startBlockID} offset={offset} relativeOffset={relativeOffset} size={size}, maxRelativeBlockID={maxRelativeBlockID} totalSize={length}");
			//Console.WriteLine(new System.Diagnostics.StackTrace());
			for (int relativeBlockID = 0; relativeBlockID <= maxRelativeBlockID; ++relativeBlockID)
			{
				int currentBlockID = startBlockID + relativeBlockID;
				int destinationOffset = 0;
				if (relativeBlockID > 0)
				{
					destinationOffset = bs - initialRelativeOffset + bs * (relativeBlockID - 1);
				}

				int readSize = -1;
				if (maxRelativeBlockID == 0)
				{
					readSize = size;
				}
				else if (relativeBlockID == 0)
				{
					readSize = bs - initialRelativeOffset;
				}
				else if (relativeBlockID == maxRelativeBlockID)
				{
					readSize = size + initialRelativeOffset - (bs * maxRelativeBlockID);
				}
				else
				{
					readSize = bs;
				}

				if (currentBlockID == amountOfBlocks - 1 && lastBlockSize < readSize)
				{
					readSize = lastBlockSize;
				}

				switch (compressionAlgorithm[currentBlockID])
				{
					case 0:
						//Console.WriteLine("memcpy");
						var rawBS = compressedBlocks[currentBlockID].GetSize();

						//This safety check doesn't work for the last block and so must be excluded!
						if (rawBS != bs && currentBlockID < amountOfBlocks - 1)
						{
							throw new FormatException("NSZ header seems to be corrupted!");
						}

						compressedBlocks[currentBlockID].Read(destination.Slice(destinationOffset, readSize), relativeOffset);
						break;
					case 1:
						//Console.WriteLine("ZStandard");
						var cachedBlock = DecompressBlock(compressedBlocks[currentBlockID]);
						cachedBlock.Slice(relativeOffset, readSize).CopyTo(destination.Slice(destinationOffset));
						//Console.Out.WriteLine(System.Text.Encoding.ASCII.GetString(destination.ToArray()));
						break;
					default:
						throw new NotImplementedException(
							"The specified compression algorithm isn't implemented yet!");
				}

				relativeOffset = 0;
			}
			
		}

		protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
		{
			throw new NotImplementedException("DecompressionStorage is read only!");
		}

		public override void Flush()
		{
		}

		private Span<byte> DecompressBlock(IStorage input)
		{
			// decompress
			using (var decompressionStream = new ZstandardStream(input.AsStream(), CompressionMode.Decompress))
			{
				decompressionStream.Read(decompressBuff, 0, bs);
				return new Span<byte>(decompressBuff);
			}
		}

		public override long GetSize()
		{
			return length;
		}
	}
}

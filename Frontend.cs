using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LibHac;
using nsZip.Crypto;
using nsZip.LibHacControl;
using nsZip.LibHacExtensions;
using Zstandard.Net;
using ProgressBar = LibHac.ProgressBar;

namespace nsZip
{
	public partial class Frontend : Form
	{
		internal static readonly string[] KakNames = {"application", "ocean", "system"};

		public Frontend()
		{
			InitializeComponent();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
		}

		private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
		{
			if (outLine.Data != null)
			{
				BeginInvoke((MethodInvoker) delegate { DebugOutput.AppendText($"{outLine.Data}\r\n"); });
			}
		}

		private static Keyset OpenKeyset()
		{
			var homeKeyFile = Path.Combine("keys.txt");

			if (!File.Exists(homeKeyFile))
			{
				throw new ArgumentException("Keys.txt not found!", "Keys.txt missing!");
			}

			return ExternalKeys.ReadKeyFile(homeKeyFile);
		}

		private NcaSection ParseSection(NcaHeader Header, int index)
		{
			var entry = Header.SectionEntries[index];
			var header = Header.FsHeaders[index];
			if (entry.MediaStartOffset == 0)
			{
				return null;
			}

			var sect = new NcaSection();

			sect.SectionNum = index;
			sect.Offset = Utils.MediaToReal(entry.MediaStartOffset);
			sect.Size = Utils.MediaToReal(entry.MediaEndOffset) - sect.Offset;
			sect.Header = header;
			sect.Type = header.Type;

			return sect;
		}

		private void cleanFolder(string folderName)
		{
			if (Directory.Exists(folderName))
			{
				Directory.Delete(folderName, true);
			}

			Thread.Sleep(50); //Wait for folder deletion!
			Directory.CreateDirectory(folderName);
		}

		private void RunButton_Click(object sender, EventArgs e)
		{
			var keyset = OpenKeyset();
			IProgressReport logger = new ProgressBar();

			cleanFolder("extracted");
			cleanFolder("decrypted");
			cleanFolder("encrypted");
			DebugOutput.Clear();

			var nspFile = (string) TaskQueue.Items[0];
			TaskQueue.Items.RemoveAt(0);
			ProcessNsp.Process(nspFile, "extracted/", logger);

			var dirExtracted = new DirectoryInfo("extracted");
			var TikFiles = dirExtracted.GetFiles("*.tik");
			var titleKey = new byte[0x10];
			foreach (var file in TikFiles)
			{
				var TicketFile = File.Open($"extracted/{file.Name}", FileMode.Open);
				TicketFile.Seek(0x180, SeekOrigin.Begin);
				TicketFile.Read(titleKey, 0, 0x10);
				var ticketNameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
				if (!ticketNameWithoutExtension.TryToBytes(out var rightsId))
				{
					throw new InvalidDataException(
						$"Invalid rights ID \"{ticketNameWithoutExtension}\" as ticket file name");
				}

				keyset.TitleKeys[rightsId] = titleKey;
				TicketFile.Dispose();
			}

			DebugOutput.AppendText($"titleKey: {Utils.BytesToString(titleKey)}\r\n");

			foreach (var file in dirExtracted.GetFiles())
			{
				if (file.Name.EndsWith(".nca"))
				{
					ProcessNca.Process($"extracted/{file.Name}", $"decrypted/{file.Name}", keyset, logger);
					EncryptNCA(file.Name, keyset, DebugOutput);
				}
				else
				{
					File.Copy($"extracted/{file.Name}", $"encrypted/{file.Name}");
				}
			}


			var threadsUsedToCompress = Environment.ProcessorCount;
			// To not exceed the 2 GB RAM Limit
			if (!Environment.Is64BitProcess)
			{
				threadsUsedToCompress = Math.Min(16, threadsUsedToCompress);
			}

			var bs = 262144;
			var input = Utils.CreateJaggedArray<byte[][]>(threadsUsedToCompress, bs);
			var output = new byte[threadsUsedToCompress][];
			var task = new Task[threadsUsedToCompress];
			var dirDecrypted = new DirectoryInfo("decrypted");

			foreach (var file in dirDecrypted.GetFiles())
			{
				var outputFile = File.Open($"NSZ/{file.Name}.nsz", FileMode.Create);
				var inputFile = File.Open(file.FullName, FileMode.Open);
				amountOfBlocks = (int)Math.Ceiling((decimal)inputFile.Length / bs);
				sizeOfSize = (int)Math.Ceiling(Math.Log(bs, 2) / 8);
				var perBlockHeaderSize = sizeOfSize + 1;
				var headerSize = 0x0C + perBlockHeaderSize * amountOfBlocks;
				outputFile.Position = headerSize;
				var breakCondition = -1;
				var nsZipMagic = new byte[] {0x6e, 0x73, 0x5a, 0x69, 0x70};
				currentBlockID = 0;
				nsZipHeader = new byte[headerSize];
				Array.Copy(nsZipMagic, nsZipHeader, 0x05);
				nsZipHeader[0x5] = 0x00; //Version
				nsZipHeader[0x6] = 0x01; //Type
				nsZipHeader[0x7] = (byte)(bs >> 32);
				nsZipHeader[0x8] = (byte)(bs >> 24);
				nsZipHeader[0x9] = (byte)(bs >> 16);
				nsZipHeader[0xA] = (byte)(bs >> 8);
				nsZipHeader[0xB] = (byte)(bs);

				DebugOutput.AppendText(Utils.BytesToString(nsZipHeader) + "\n\r");

				while (true)
				{
					for (var i = 0; i < threadsUsedToCompress; ++i)
					{
						var iNow = i;
						if (task[iNow] != null)
						{
							task[iNow].Wait();
							WriteBlock(outputFile, input[iNow], output[iNow]);
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
				WriteBlock(outputFile, lastBlockInput, lastBlockOutput);
				outputFile.Position = 0;
				outputFile.Write(nsZipHeader, 0, headerSize);
				inputFile.Dispose();
				outputFile.Dispose();
			}
		}

		private byte[] nsZipHeader;
		private int sizeOfSize;
		private int amountOfBlocks;
		private int currentBlockID;
		private void WriteBlock(FileStream outputFile, byte[] input, byte[] output)
		{
			var offset = currentBlockID * (sizeOfSize+1);
			var inputLen = input.Length;
			var outputLen = output.Length;
			if (outputLen >= inputLen)
			{
				nsZipHeader[0x0C + offset] = 0x00;
				for (var j = 0; j < sizeOfSize; ++j)
				{
					nsZipHeader[0x0D + offset + j] = (byte)(inputLen >> (sizeOfSize - j - 1) * 8);
				}
				outputFile.Write(input, 0, inputLen);
			}
			else
			{
				nsZipHeader[0x0C + offset] = 0x01;
				for (var j = 0; j < sizeOfSize; ++j)
				{
					DebugOutput.ScrollToCaret();
					DebugOutput.Refresh();
					nsZipHeader[0x0D + offset + j] = (byte)(outputLen >> (sizeOfSize - j - 1) * 8);
				}
				outputFile.Write(output, 0, outputLen);
			}

			DebugOutput.AppendText($"{currentBlockID+1}/{amountOfBlocks} Blocks written\r\n");
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

		private void EncryptNCA(string ncaName, Keyset keyset, RichTextBox TB)
		{
			var DecryptedKeys = Utils.CreateJaggedArray<byte[][]>(4, 0x10);
			var HeaderKey1 = new byte[16];
			var HeaderKey2 = new byte[16];
			Buffer.BlockCopy(keyset.HeaderKey, 0, HeaderKey1, 0, 16);
			Buffer.BlockCopy(keyset.HeaderKey, 16, HeaderKey2, 0, 16);

			var Input = File.Open($"decrypted/{ncaName}", FileMode.Open);
			DebugOutput.AppendText($"Input: {Input}\r\n");
			var DecryptedHeader = new byte[0xC00];
			Input.Read(DecryptedHeader, 0, 0xC00);

			var Header = new NcaHeader(new BinaryReader(new MemoryStream(DecryptedHeader)), keyset);
			var CryptoType = Math.Max(Header.CryptoType, Header.CryptoType2);
			if (CryptoType > 0)
			{
				CryptoType--;
			}

			var HasRightsId = !Header.RightsId.IsEmpty();

			if (!HasRightsId)
			{
				TB.AppendText("Key Area (Encrypted):\r\n");
				if (keyset.KeyAreaKeys[CryptoType][Header.KaekInd].IsEmpty())
				{
					throw new ArgumentException($"key_area_key_{KakNames[Header.KaekInd]}_{CryptoType:x2}",
						"Missing area key!");
				}

				TB.AppendText(
					$"key_area_key_{KakNames[Header.KaekInd]}_{CryptoType:x2}: {Utils.BytesToString(keyset.KeyAreaKeys[CryptoType][Header.KaekInd])}\r\n");

				for (var i = 0; i < 4; ++i)
				{
					Crypto.Crypto.DecryptEcb(keyset.KeyAreaKeys[CryptoType][Header.KaekInd], Header.EncryptedKeys[i],
						DecryptedKeys[i], 0x10);
					TB.AppendText($"Key {i} (Encrypted): {Utils.BytesToString(Header.EncryptedKeys[i])}\r\n");
					TB.AppendText($"Key {i} (Decrypted): {Utils.BytesToString(DecryptedKeys[i])}\r\n");
				}
			}
			else
			{
				var TicketFile = File.Open($"extracted/{Utils.BytesToString(Header.RightsId)}.tik", FileMode.Open);
				TicketFile.Seek(0x180, SeekOrigin.Begin);
				var titleKey = new byte[0x10];
				var TitleKeyDec = new byte[0x10];
				TicketFile.Read(titleKey, 0, 0x10);
				TicketFile.Dispose();
				Crypto.Crypto.DecryptEcb(keyset.Titlekeks[CryptoType], titleKey, TitleKeyDec, 0x10);
				TB.AppendText($"titleKey: {Utils.BytesToString(titleKey)}\r\n");
				TB.AppendText($"TitleKeyDec: {Utils.BytesToString(TitleKeyDec)}\r\n");
				DecryptedKeys[2] = TitleKeyDec;
			}

			var Sections = new NcaSection[4];
			var SectionsByOffset = new Dictionary<long, int>();
			var lowestOffset = long.MaxValue;
			for (var i = 0; i < 4; ++i)
			{
				var section = ParseSection(Header, i);
				if (section == null)
				{
					continue;
				}

				SectionsByOffset.Add(section.Offset, i);
				if (section.Offset < lowestOffset)
				{
					lowestOffset = section.Offset;
				}

				Sections[i] = section;
			}

			var Output = File.Open($"encrypted/{ncaName}", FileMode.Create);
			TB.AppendText("Opened NCA for writing...\r\n");
			TB.AppendText($"HeaderKey: {Utils.BytesToString(keyset.HeaderKey)}\r\n");
			TB.ScrollToCaret();
			TB.AppendText("Encrypting and writing header to NCA...\r\n");
			TB.ScrollToCaret();
			Output.Write(CryptoInitialisers.AES_XTS(HeaderKey1, HeaderKey2, 0x200, DecryptedHeader, 0), 0,
				DecryptedHeader.Length);

			var dummyHeader = new byte[0xC00];
			ulong dummyHeaderSector = 6;
			long dummyHeaderPos;

			for (dummyHeaderPos = 0xC00; dummyHeaderPos < lowestOffset; dummyHeaderPos += 0xC00)
			{
				Input.Read(dummyHeader, 0, 0xC00);
				var dummyHeaderWriteCount = (int) Math.Min(lowestOffset - dummyHeaderPos, DecryptedHeader.Length);
				Output.Write(CryptoInitialisers.AES_XTS(HeaderKey1, HeaderKey2, 0x200, dummyHeader, dummyHeaderSector),
					0, dummyHeaderWriteCount);
				dummyHeaderSector += 6;
			}

			TB.AppendText("Encrypting and writing sectors to NCA...\r\n");
			TB.ScrollToCaret();


			TB.AppendText("Sections:");
			foreach (var i in SectionsByOffset.OrderBy(i => i.Key).Select(item => item.Value))
			{
				var sect = Sections[i];
				if (sect == null)
				{
					continue;
				}

				var isExefs = Header.ContentType == ContentType.Program && i == (int) ProgramPartitionType.Code;
				var PartitionType = isExefs ? "ExeFS" : sect.Type.ToString();
				TB.AppendText($"    Section {i}:\r\n");
				TB.AppendText($"        Offset: 0x{sect.Offset:x12}\r\n");
				TB.AppendText($"        Size: 0x{sect.Size:x12}\r\n");
				TB.AppendText($"        Partition Type: {PartitionType}\r\n");
				TB.AppendText($"        Section CTR: {Utils.BytesToString(sect.Header.Ctr)}\r\n");

				var initialCounter = new byte[0x10];


				if (sect.Header.Ctr != null)
				{
					Array.Copy(sect.Header.Ctr, initialCounter, 8);
				}

				TB.AppendText($"initialCounter: {Utils.BytesToString(initialCounter)}\r\n");

				Input.Seek(sect.Offset, SeekOrigin.Begin);
				Output.Seek(sect.Offset, SeekOrigin.Begin);

				const int maxBS = 10485760; //10 MB
				int bs;
				var DecryptedSectionBlock = new byte[maxBS];
				var sectOffsetEnd = sect.Offset + sect.Size;
				switch (sect.Header.EncryptionType)
				{
					case NcaEncryptionType.None:
						while (Input.Position < sectOffsetEnd)
						{
							bs = (int) Math.Min(sectOffsetEnd - Input.Position, maxBS);
							TB.AppendText($"Encrypted: {Input.Position / 0x100000} MB\r\n");
							TB.ScrollToCaret();
							Input.Read(DecryptedSectionBlock, 0, bs);
							Output.Write(DecryptedSectionBlock, 0, bs);
						}

						break;
					case NcaEncryptionType.AesCtr:
						while (Input.Position < sectOffsetEnd)
						{
							SetCtrOffset(initialCounter, Input.Position);
							bs = (int) Math.Min(sectOffsetEnd - Input.Position, maxBS);
							TB.AppendText($"Encrypted: {Input.Position / 0x100000} MB\r\n");
							TB.ScrollToCaret();
							Input.Read(DecryptedSectionBlock, 0, bs);
							Output.Write(
								AesCTR.AesCtrTransform(DecryptedKeys[2], initialCounter, DecryptedSectionBlock, bs), 0,
								bs);
						}

						break;
					case NcaEncryptionType.AesCtrEx:

						var info = sect.Header.BktrInfo;
						var MyBucketTree = new MyBucketTree<AesSubsectionEntry>(
							new MemoryStream(sect.Header.BktrInfo.EncryptionHeader.Header), Input,
							sect.Offset + info.EncryptionHeader.Offset);
						var SubsectionEntries = MyBucketTree.GetEntryList();
						var SubsectionOffsets = SubsectionEntries.Select(x => x.Offset).ToList();

						var subsectionEntryCounter = new byte[0x10];
						Array.Copy(initialCounter, subsectionEntryCounter, 0x10);
						foreach (var entry in SubsectionEntries)
						{
							//Array.Copy(initialCounter, subsectionEntryCounter, 0x10);
							SetCtrOffset(subsectionEntryCounter, Input.Position);
							subsectionEntryCounter[7] = (byte) entry.Counter;
							subsectionEntryCounter[6] = (byte) (entry.Counter >> 8);
							subsectionEntryCounter[5] = (byte) (entry.Counter >> 16);
							subsectionEntryCounter[4] = (byte) (entry.Counter >> 24);

							//bs = (int)Math.Min((sect.Offset + entry.OffsetEnd) - Input.Position, maxBS);
							bs = (int) (entry.OffsetEnd - entry.Offset);
							var DecryptedSectionBlockLUL = new byte[bs];
							TB.AppendText($"Encrypted: {Input.Position / 0x100000} MB\r\n");
							TB.ScrollToCaret();
							TB.AppendText($"{Input.Position}: {Utils.BytesToString(subsectionEntryCounter)}\r\n");
							Input.Read(DecryptedSectionBlockLUL, 0, bs);
							Output.Write(
								AesCTR.AesCtrTransform(DecryptedKeys[2], subsectionEntryCounter,
									DecryptedSectionBlockLUL, bs), 0, bs);
						}

						while (Input.Position < sectOffsetEnd)
						{
							SetCtrOffset(subsectionEntryCounter, Input.Position);
							bs = (int) Math.Min(sectOffsetEnd - Input.Position, maxBS);
							TB.AppendText($"EncryptedAfter: {Input.Position / 0x100000} MB\r\n");
							TB.ScrollToCaret();
							Input.Read(DecryptedSectionBlock, 0, bs);
							TB.AppendText($"{Input.Position}: {Utils.BytesToString(subsectionEntryCounter)}\r\n");
							Output.Write(
								AesCTR.AesCtrTransform(DecryptedKeys[2], subsectionEntryCounter, DecryptedSectionBlock,
									bs), 0, bs);
						}

						break;

					default:
						throw new NotImplementedException();
				}
			}

			Input.Dispose();
			Output.Dispose();
			TB.AppendText("Done!");
			TB.ScrollToCaret();
			//MessageBox.Show("Done!");
		}

		private static void SetCtrOffset(byte[] ctr, long offset)
		{
			ctr[0xF] = (byte) (offset >> 4);
			ctr[0xE] = (byte) (offset >> 12);
			ctr[0xD] = (byte) (offset >> 20);
			ctr[0xC] = (byte) (offset >> 28);
			ctr[0xB] = (byte) (offset >> 36);
			ctr[0xA] = (byte) (offset >> 44);
			ctr[0x9] = (byte) (offset >> 52);
			ctr[0x8] = (byte) (offset >> 60);
		}

		private void runCommand(string ExeName, string arguments)
		{
			//* Create your Process
			var process = new Process
			{
				StartInfo =
				{
					FileName = ExeName,
					Arguments = arguments,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				}
			};
			//* Set your output and error (asynchronous) handlers
			process.OutputDataReceived += OutputHandler;
			process.ErrorDataReceived += OutputHandler;
			//* Start process and handlers
			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			process.WaitForExit();
		}

		private void SelectNspFileToCompressButton_Click(object sender, EventArgs e)
		{
			if (SelectNspDialog.ShowDialog() == DialogResult.OK)
			{
				foreach (var filename in SelectNspDialog.FileNames)
				{
					TaskQueue.Items.Add(filename);
				}
			}
		}

		private void SelectNszFileToDecompressButton_Click(object sender, EventArgs e)
		{
			if (SelectNszDialog.ShowDialog() == DialogResult.OK)
			{
				foreach (var filename in SelectNszDialog.FileNames)
				{
					TaskQueue.Items.Add(filename);
				}
			}
		}

		private void listBox_KeyDown(object sender, PreviewKeyDownEventArgs e)
		{
			var listBox = (ListBox) sender;
			if (e.KeyCode == Keys.Back ||
			    e.KeyCode == Keys.Delete)
			{
				var selected = new int[listBox.SelectedIndices.Count];
				listBox.SelectedIndices.CopyTo(selected, 0);
				foreach (var selectedItemIndex in selected.Reverse())
				{
					listBox.Items.RemoveAt(selectedItemIndex);
				}
			}
		}
	}
}
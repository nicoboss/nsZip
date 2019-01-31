﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LibHac;
using nsZip.LibHacControl;
using nsZip.LibHacExtensions;

namespace nsZip
{
	public partial class Frontend : Form
	{
		private readonly bool VerifWhenCompressing = true;

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
			if (TaskQueue.Items.Count == 0)
			{
				CompressFolder.Compress(DebugOutput, "decrypted", "compressed");
				DecompressFolder.Decompress(DebugOutput, "compressed", "extracted");
				DebugOutput.AppendText("Nothing to do - TaskQueue empty! Please add an NSP or NSPZ!");
				return;
			}

			do
			{

				cleanFolder("extracted");
				cleanFolder("decrypted");
				cleanFolder("encrypted");
				cleanFolder("compressed");
				DebugOutput.Clear();

				var inFile = (string) TaskQueue.Items[0];
				var infileLowerCase = inFile.ToLower();
				TaskQueue.Items.RemoveAt(0);
				if (infileLowerCase.EndsWith("nsp"))
				{
					CompressNSP(inFile);
				}
				else if (infileLowerCase.EndsWith("xci"))
				{
					CompressXCI(inFile);
				}
				else if (infileLowerCase.EndsWith("nspz"))
				{
					DecompressNSPZ(inFile);
				}
				else
				{
					throw new InvalidDataException($"Invalid file type {inFile}");
				}

				var dataString = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
				var inFileNoExtension = Path.GetFileNameWithoutExtension(inFile);
				File.WriteAllLines($"DebugOutput_{dataString}_{inFileNoExtension}.log", DebugOutput.Text.Split('\n'));
			} while (TaskQueue.Items.Count > 0);
		}

		private void CompressNSP(string nspFile)
		{
			var nspFileNoExtension = Path.GetFileNameWithoutExtension(nspFile);
			DebugOutput.AppendText($"Task CompressNSP \"{nspFileNoExtension}\" started\r\n");
			var keyset = OpenKeyset();
			ProcessNsp.Process(nspFile, "extracted/", DebugOutput);
			CompressExtracted(keyset);
			FolderTools.FolderToNSP("compressed", $"{nspFileNoExtension}.nspz");
			DebugOutput.AppendText($"Task CompressNSP \"{nspFileNoExtension}\" completed!\r\n");
		}

		private void CompressXCI(string xciFile)
		{
			var xciFileNoExtension = Path.GetFileNameWithoutExtension(xciFile);
			DebugOutput.AppendText($"Task CompressXCI \"{xciFileNoExtension}\" started\r\n");
			var keyset = OpenKeyset();
			ProcessXci.Process(xciFile, "extracted/", keyset, DebugOutput);
			CompressExtracted(keyset);
			FolderTools.FolderToNSP("compressed", $"{xciFileNoExtension}.xciz");
			DebugOutput.AppendText($"Task CompressXCI \"{xciFileNoExtension}\" completed!\r\n");
		}

		private void CompressExtracted(Keyset keyset)
		{
			FolderTools.ExtractTitlekeys("extracted", keyset, DebugOutput);

			var dirExtracted = new DirectoryInfo("extracted");
			foreach (var file in dirExtracted.GetFiles())
			{
				if (file.Name.EndsWith(".nca"))
				{
					ProcessNca.Process($"extracted/{file.Name}", $"decrypted/{file.Name}", keyset, DebugOutput);
				}
				else
				{
					file.CopyTo($"decrypted/{file.Name}");
				}
			}

			TrimDeltaNCA.Process("decrypted", keyset, DebugOutput);
			CompressFolder.Compress(DebugOutput, "decrypted", "compressed");

			if (VerifWhenCompressing)
			{
				cleanFolder("decrypted");
				cleanFolder("encrypted");
				DecompressFolder.Decompress(DebugOutput, "compressed", "decrypted");
				UntrimDeltaNCA.Process("decrypted", "extracted", keyset, DebugOutput);

				var dirDecrypted = new DirectoryInfo("decrypted");
				foreach (var file in dirDecrypted.GetFiles("*.nca"))
				{
					EncryptNCA.Encrypt(file.Name, false, true, keyset, DebugOutput);
				}
			}
		}

		private void DecompressNSPZ(string nspzFile)
		{
			var nspzFileNoExtension = Path.GetFileNameWithoutExtension(nspzFile);
			DebugOutput.AppendText($"Task DecompressNSPZ \"{nspzFileNoExtension}\" started\r\n");
			var keyset = OpenKeyset();
			ProcessNsp.Process(nspzFile, "extracted/", DebugOutput);
			DecompressFolder.Decompress(DebugOutput, "extracted", "decrypted");
			UntrimAndEncrypt(keyset);
			var newFileName = $"{Path.GetFileNameWithoutExtension(nspzFile)}.nsp";
			FolderTools.FolderToNSP("encrypted", newFileName);
			DebugOutput.AppendText($"Task DecompressNSPZ \"{nspzFileNoExtension}\" completed!\r\n");
		}

		public void UntrimAndEncrypt(Keyset keyset)
		{
			FolderTools.ExtractTitlekeys("decrypted", keyset, DebugOutput);

			var dirDecrypted = new DirectoryInfo("decrypted");
			foreach (var file in dirDecrypted.GetFiles())
			{
				if (file.Name.EndsWith(".tca"))
				{
					continue;
				}

				if (file.Name.EndsWith(".nca"))
				{
					EncryptNCA.Encrypt(file.Name, true, true, keyset, DebugOutput);
					file.Delete();
				}
				else
				{
					file.MoveTo($"encrypted/{file.Name}");
				}
			}

			UntrimDeltaNCA.Process("decrypted", "encrypted", keyset, DebugOutput);

			foreach (var file in dirDecrypted.GetFiles("*.nca"))
			{
				EncryptNCA.Encrypt(file.Name, true, true, keyset, DebugOutput);
			}
		}

		private void SelectNspFileToCompressButton_Click(object sender, EventArgs e)
		{
			if (SelectNspXciDialog.ShowDialog() == DialogResult.OK)
			{
				foreach (var filename in SelectNspXciDialog.FileNames)
				{
					TaskQueue.Items.Add(filename);
				}
			}
		}

		private void SelectNszFileToDecompressButton_Click(object sender, EventArgs e)
		{
			if (SelectNspzDialog.ShowDialog() == DialogResult.OK)
			{
				foreach (var filename in SelectNspzDialog.FileNames)
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

		private void DebugOutput_TextChanged(object sender, EventArgs e)
		{
			var richTextBox = (RichTextBox) sender;
			// set the current caret position to the end
			richTextBox.SelectionStart = richTextBox.Text.Length;
			// scroll it automatically
			richTextBox.ScrollToCaret();
		}
	}
}
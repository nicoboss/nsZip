using System;
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
		private int BlockSize = 262144;
		private string OutputFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
		private bool VerifyWhenCompressing = true;
		private int ZstdLevel = 18;

		public Frontend()
		{
			InitializeComponent();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			MaximumSize = Screen.FromControl(this).WorkingArea.Size;
			CompressionLevelComboBox.SelectedIndex = 3;
			BlockSizeComboBox.SelectedIndex = 0;
			VerifyAfterCompressCheckBox_CheckedChanged(null, null);
		}

		//To properly fit the Form to if moved to a screen with another resolution
		private void Frontend_Move(object sender, EventArgs e)
		{
			var newMaxSize = Screen.FromControl(this).WorkingArea.Size;
			if (!MaximumSize.Equals(newMaxSize))
			{
				MaximumSize = newMaxSize;

				//This line is so dumb but required
				//for it to refresh it's size properly
				Size = Size;
			}
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
			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			var homeSwitch = Path.Combine(home, ".switch");
			var homeKeyFile = Path.Combine(home, ".switch", "prod.keys");
			var homeTitleKeyFile = Path.Combine(home, ".switch", "title.keys");
			var homeConsoleKeyFile = Path.Combine(home, ".switch", "console.keys");
			string keyFile = null;
			string titleKeyFile = null;
			string consoleKeyFile = null;


			while (true)
			{
				if (File.Exists(homeKeyFile))
				{
					keyFile = homeKeyFile;
					break;
				}

				if (File.Exists("keys.txt"))
				{
					keyFile = "keys.txt";
					break;
				}

				Directory.CreateDirectory(homeSwitch);
				Process.Start(homeSwitch);
				var dialogResult = MessageBox.Show(
					@"prod.keys not found! Dump them using Lockpick and put prod.keys (and title.keys if your NSP files have no ticket included) in """ +
					homeSwitch + @""" Press OK when you're done.", @"prod.keys not found! Press OK when you're done.",
					MessageBoxButtons.OKCancel, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
				if (dialogResult == DialogResult.Cancel)
				{
					throw new ArgumentException(
						@"prod.keys not found! Please put prod.keys in " + homeKeyFile);
				}
			}

			if (File.Exists(homeTitleKeyFile))
			{
				titleKeyFile = homeTitleKeyFile;
			}

			if (File.Exists(homeConsoleKeyFile))
			{
				consoleKeyFile = homeConsoleKeyFile;
			}

			return ExternalKeys.ReadKeyFile(keyFile, titleKeyFile, consoleKeyFile);
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
				DebugOutput.AppendText("Nothing to do - TaskQueue empty! Please add an NSP or NSPZ!\r\n");
				return;
			}

			do
			{
				cleanFolders();
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
				else if (infileLowerCase.EndsWith("xciz"))
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

			cleanFolders();
		}

		private void cleanFolders()
		{
			cleanFolder("extracted");
			cleanFolder("decrypted");
			cleanFolder("encrypted");
			cleanFolder("compressed");
		}

		private void CompressNSP(string nspFile)
		{
			var nspFileNoExtension = Path.GetFileNameWithoutExtension(nspFile);
			DebugOutput.AppendText($"Task CompressNSP \"{nspFileNoExtension}\" started\r\n");
			var keyset = OpenKeyset();
			ProcessNsp.Process(nspFile, "extracted/", DebugOutput);
			CompressExtracted(keyset);
			var nspzOutPath = Path.Combine(OutputFolderPath, nspFileNoExtension);
			FolderTools.FolderToNSP("compressed", $"{nspzOutPath}.nspz");
			DebugOutput.AppendText($"Task CompressNSP \"{nspFileNoExtension}\" completed!\r\n");
		}

		private void CompressXCI(string xciFile)
		{
			var xciFileNoExtension = Path.GetFileNameWithoutExtension(xciFile);
			DebugOutput.AppendText($"Task CompressXCI \"{xciFileNoExtension}\" started\r\n");
			var keyset = OpenKeyset();
			ProcessXci.Process(xciFile, "extracted/", keyset, DebugOutput);
			CompressExtracted(keyset);
			var xciOutPath = Path.Combine(OutputFolderPath, xciFileNoExtension);
			FolderTools.FolderToNSP("compressed", $"{xciOutPath}.xciz");
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
			CompressFolder.Compress(DebugOutput, "decrypted", "compressed", BlockSize, ZstdLevel);

			if (VerifyWhenCompressing)
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
			var nspOutPath = Path.Combine(OutputFolderPath, nspzFileNoExtension);
			FolderTools.FolderToNSP("encrypted", $"{nspOutPath}.nsp");
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

		private void SelectOutputDictionaryButton_Click(object sender, EventArgs e)
		{
			SelectOutputDictionaryDialog.SelectedPath = OutputFolderPath;
			if (SelectOutputDictionaryDialog.ShowDialog() == DialogResult.OK)
			{
				OutputFolderPath = SelectOutputDictionaryDialog.SelectedPath;
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

		private void nsZipGitHubLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			Process.Start("https://github.com/nicoboss/nsZip");
		}

		private void VerifyAfterCompressCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			if (!VerifyAfterCompressCheckBox.Checked)
			{
				var dialogResult = MessageBox.Show(
					@"Without verification corrupted unrecoverable nspz/nciz caused by bugs won't be discovered until you try to decompress them. Due to the early state of nsZip I highly recommend to leave it ON if you don't keep a backup copy of your nsp/xci. Do you really want to turn OFF verification?",
					@"Do you really want to turn of verification after compression?", MessageBoxButtons.YesNo,
					MessageBoxIcon.Warning,
					MessageBoxDefaultButton.Button2);
				if (dialogResult != DialogResult.Yes)
				{
					VerifyAfterCompressCheckBox.Checked = true;
				}
			}

			VerifyWhenCompressing = VerifyAfterCompressCheckBox.Checked;
			DebugOutput.AppendText($"Set VerifyWhenCompressing to {VerifyWhenCompressing}\r\n");
		}

		private void CompressionLevelComboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			switch (CompressionLevelComboBox.SelectedIndex)
			{
				case 0:
					ZstdLevel = 8;
					break;
				case 1:
					ZstdLevel = 12;
					break;
				case 2:
					ZstdLevel = 14;
					break;
				case 3:
					ZstdLevel = 18;
					break;
				case 4:
					ZstdLevel = 22;
					break;
				default:
					throw new NotImplementedException();
			}

			DebugOutput.AppendText($"Set ZstdLevel to {ZstdLevel}\r\n");
		}

		private void BlockSizeComboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			switch (BlockSizeComboBox.SelectedIndex)
			{
				case 0:
					BlockSize = 262144;
					break;
				case 1:
					BlockSize = 524288;
					break;
				default:
					throw new NotImplementedException();
			}

			DebugOutput.AppendText($"Set BlockSize to {BlockSize} bytes\r\n");
		}
	}
}
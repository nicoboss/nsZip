using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LibHac;
using nsZip.LibHacControl;
using nsZip.LibHacExtensions;
using ProgressBar = LibHac.ProgressBar;

namespace nsZip
{
	public partial class Frontend : Form
	{
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
				DebugOutput.AppendText("Nothing to do - TaskQueue empty! Please add an NSP or NSPZ!");
				return;
			}

			cleanFolder("extracted");
			cleanFolder("decrypted");
			cleanFolder("encrypted");
			cleanFolder("compressed");
			DebugOutput.Clear();
			var infile = (string) TaskQueue.Items[0];
			var infileLowerCase = infile.ToLower();
			TaskQueue.Items.RemoveAt(0);
			if (infileLowerCase.EndsWith("nsp"))
			{
				CompressNSP(infile);
			}
			else if (infileLowerCase.EndsWith("nspz"))
			{
				DecompressNSPZ(infile);
			}
			else
			{
				throw new InvalidDataException($"Invalid file type {infile}");
			}
		}


		private void CompressNSP(string nspFile)
		{
			DebugOutput.AppendText($"Task CompressNSP {nspFile} started\r\n");
			var keyset = OpenKeyset();
			IProgressReport logger = new ProgressBar();
			ProcessNsp.Process(nspFile, "extracted/", logger);
			FolderTools.ExtractTitlekeys("extracted", keyset, DebugOutput);

			var dirExtracted = new DirectoryInfo("extracted");
			foreach (var file in dirExtracted.GetFiles())
			{
				if (file.Name.EndsWith(".nca"))
				{
					ProcessNca.Process($"extracted/{file.Name}", $"decrypted/{file.Name}", keyset, logger);
				}
				else
				{
					file.CopyTo($"decrypted/{file.Name}");
				}
			}

			TrimDeltaNCA.Process("decrypted", keyset, DebugOutput);
			CompressFolder.Compress(DebugOutput, "decrypted", "compressed");
			var newFileName = $"{Path.GetFileNameWithoutExtension(nspFile)}.nspz";
			FolderTools.FolderToNSP("compressed", newFileName);
			DebugOutput.AppendText($"Task CompressNSP {nspFile} completed!\r\n");
		}


		private void DecompressNSPZ(string nspzFile)
		{
			DebugOutput.AppendText($"Task DecompressNSPZ {nspzFile} started\r\n");
			var keyset = OpenKeyset();
			IProgressReport logger = new ProgressBar();
			ProcessNsp.Process(nspzFile, "extracted/", logger);
			DecompressFolder.Decompress(DebugOutput, "extracted", "decrypted");
			UntrimDeltaNCA.Process("decrypted", keyset, DebugOutput);
			FolderTools.ExtractTitlekeys("decrypted", keyset, DebugOutput);

			var dirExtracted = new DirectoryInfo("decrypted");
			foreach (var file in dirExtracted.GetFiles())
			{
				if (file.Name.EndsWith(".nca"))
				{
					EncryptNCA.Encrypt(file.Name, keyset, DebugOutput);
				}
				else
				{
					file.CopyTo($"encrypted/{file.Name}");
				}
			}

			var newFileName = $"{Path.GetFileNameWithoutExtension(nspzFile)}.nsp";
			FolderTools.FolderToNSP("encrypted", newFileName);
			DebugOutput.AppendText($"Task DecompressNSPZ {nspzFile} completed!\r\n");
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
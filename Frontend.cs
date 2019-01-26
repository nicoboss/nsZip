using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LibHac;
using nsZip.LibHacControl;
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
			var keyset = OpenKeyset();
			IProgressReport logger = new ProgressBar();

			cleanFolder("extracted");
			cleanFolder("decrypted");
			cleanFolder("encrypted");
			cleanFolder("NSZ");
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
					EncryptNCA.Encrypt(file.Name, keyset, DebugOutput);
				}
			}

			CompressFolder.Compress(DebugOutput, "decrypted");
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
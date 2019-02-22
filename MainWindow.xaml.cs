using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using LibHac;
using nsZip.LibHacControl;
using nsZip.LibHacExtensions;

namespace nsZip
{
	/// <summary>
	///     Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private readonly int BlockSize = 262144;
		private readonly Output Out;
		private readonly string OutputFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
		private readonly OpenFileDialog SelectNspXciDialog = new OpenFileDialog();
		private readonly OpenFileDialog SelectNspzDialog = new OpenFileDialog();
		private readonly FolderBrowserDialog SelectOutputDictionaryDialog = new FolderBrowserDialog();
		private readonly bool VerifyWhenCompressing = true;
		private readonly int ZstdLevel = 18;

		public MainWindow()
		{
			InitializeComponent();
			Out = new Output();

			SelectNspzDialog.Filter =
				"Compressed Switch File (*.nspz)|*.nspz|XCIZ to not-installable NSP (*.xciz)|*.xciz";
			SelectNspzDialog.Multiselect = true;
			SelectNspzDialog.Title = "Select input nspz fIles...";

			SelectNspXciDialog.Filter =
				"Switch Games (*.nsp;*.xci)|*.nsp;*.xci|Switch Package (*.nsp)|*.ns|Switch Cartridge (*.xci)|*.xci";
			SelectNspXciDialog.Multiselect = true;
			SelectNspXciDialog.Title = "Select input NSP fIles...";

			SelectOutputDictionaryDialog.RootFolder = Environment.SpecialFolder.MyComputer;

			//CompressionLevelComboBox.SelectedIndex = 3;
			//BlockSizeComboBox.SelectedIndex = 0;
			//VerifyAfterCompressCheckBox_CheckedChanged(null, null);
			Console.WriteLine("nsZip initialized");
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
			Out.Print($"Task CompressNSP \"{nspFileNoExtension}\" started\r\n");
			var keyset = ProcessKeyset.OpenKeyset();
			ProcessNsp.Process(nspFile, "extracted/", Out);
			CompressExtracted(keyset);
			var nspzOutPath = Path.Combine(OutputFolderPath, nspFileNoExtension);
			FolderTools.FolderToNSP("compressed", $"{nspzOutPath}.nspz");
			Out.Print($"Task CompressNSP \"{nspFileNoExtension}\" completed!\r\n");
		}

		private void CompressXCI(string xciFile)
		{
			var xciFileNoExtension = Path.GetFileNameWithoutExtension(xciFile);
			Out.Print($"Task CompressXCI \"{xciFileNoExtension}\" started\r\n");
			var keyset = ProcessKeyset.OpenKeyset();
			ProcessXci.Process(xciFile, "extracted/", keyset, Out);
			CompressExtracted(keyset);
			var xciOutPath = Path.Combine(OutputFolderPath, xciFileNoExtension);
			FolderTools.FolderToNSP("compressed", $"{xciOutPath}.xciz");
			Out.Print($"Task CompressXCI \"{xciFileNoExtension}\" completed!\r\n");
		}

		private void CompressExtracted(Keyset keyset)
		{
			FolderTools.ExtractTitlekeys("extracted", keyset, Out);

			var dirExtracted = new DirectoryInfo("extracted");
			foreach (var file in dirExtracted.GetFiles())
			{
				if (file.Name.EndsWith(".nca"))
				{
					ProcessNca.Process($"extracted/{file.Name}", $"decrypted/{file.Name}", keyset, Out);
				}
				else
				{
					file.CopyTo($"decrypted/{file.Name}");
				}
			}

			TrimDeltaNCA.Process("decrypted", keyset, Out);
			CompressFolder.Compress(Out, "decrypted", "compressed", BlockSize, ZstdLevel);

			if (VerifyWhenCompressing)
			{
				cleanFolder("decrypted");
				cleanFolder("encrypted");
				DecompressFolder.Decompress(Out, "compressed", "decrypted");
				UntrimDeltaNCA.Process("decrypted", "extracted", keyset, Out);

				var dirDecrypted = new DirectoryInfo("decrypted");
				foreach (var file in dirDecrypted.GetFiles("*.nca"))
				{
					EncryptNCA.Encrypt(file.Name, false, true, keyset, Out);
				}
			}
		}

		private void DecompressNSPZ(string nspzFile)
		{
			var nspzFileNoExtension = Path.GetFileNameWithoutExtension(nspzFile);
			Out.Print($"Task DecompressNSPZ \"{nspzFileNoExtension}\" started\r\n");
			var keyset = ProcessKeyset.OpenKeyset();
			ProcessNsp.Process(nspzFile, "extracted/", Out);
			DecompressFolder.Decompress(Out, "extracted", "decrypted");
			UntrimAndEncrypt(keyset);
			var nspOutPath = Path.Combine(OutputFolderPath, nspzFileNoExtension);
			FolderTools.FolderToNSP("encrypted", $"{nspOutPath}.nsp");
			Out.Print($"Task DecompressNSPZ \"{nspzFileNoExtension}\" completed!\r\n");
		}

		public void UntrimAndEncrypt(Keyset keyset)
		{
			FolderTools.ExtractTitlekeys("decrypted", keyset, Out);

			var dirDecrypted = new DirectoryInfo("decrypted");
			foreach (var file in dirDecrypted.GetFiles())
			{
				if (file.Name.EndsWith(".tca"))
				{
					continue;
				}

				if (file.Name.EndsWith(".nca"))
				{
					EncryptNCA.Encrypt(file.Name, true, true, keyset, Out);
					file.Delete();
				}
				else
				{
					file.MoveTo($"encrypted/{file.Name}");
				}
			}

			UntrimDeltaNCA.Process("decrypted", "encrypted", keyset, Out);

			foreach (var file in dirDecrypted.GetFiles("*.nca"))
			{
				EncryptNCA.Encrypt(file.Name, true, true, keyset, Out);
			}
		}

		private void SelectFileToCompressButton_Click(object sender, RoutedEventArgs e)
		{
			if (SelectNspXciDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				foreach (var filename in SelectNspXciDialog.FileNames)
				{
					TaskQueue.Items.Add(filename);
				}
			}
		}

		private void SelectNspFileToDecompressButton_Click(object sender, RoutedEventArgs e)
		{
			if (SelectNspzDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				foreach (var filename in SelectNspzDialog.FileNames)
				{
					TaskQueue.Items.Add(filename);
				}
			}
		}

		private void RunButton_Click(object sender, RoutedEventArgs e)
		{
			if (TaskQueue.Items.Count == 0)
			{
				Out.Print("Nothing to do - TaskQueue empty! Please add an NSP or NSPZ!\r\n");
				return;
			}

			do
			{
				cleanFolders();

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
			} while (TaskQueue.Items.Count > 0);

			cleanFolders();
		}
	}
}
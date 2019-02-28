using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Navigation;
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
		private bool CheckForUpdates;
		private int BlockSize = 262144;
		private readonly Output Out;
		private string OutputFolderPath;
		private string TempFolderPath;
		private readonly OpenFileDialog SelectNspXciDialog = new OpenFileDialog();
		private readonly OpenFileDialog SelectNspzDialog = new OpenFileDialog();
		private readonly FolderBrowserDialog SelectOutputDictionaryDialog = new FolderBrowserDialog();
		private readonly FolderBrowserDialog SelectTempDictionaryDialog = new FolderBrowserDialog();
		private bool VerifyWhenCompressing = true;
		private int ZstdLevel = 18;
		private bool KeepTempFilesAfterTask = false;
		private int StandByWhenTaskDone = 0;

		public MainWindow()
		{
			InitializeComponent();
			Out = new Output();

			MainGrid.Visibility = Visibility.Visible;
			MainGridBusy.Visibility = Visibility.Hidden;

			SelectNspzDialog.Filter =
				"Compressed Switch File (*.nspz)|*.nspz|XCIZ to not-installable NSP (*.xciz)|*.xciz";
			SelectNspzDialog.Multiselect = true;
			SelectNspzDialog.Title = "Select input nspz fIles...";

			SelectNspXciDialog.Filter =
				"Switch Games (*.nsp;*.xci)|*.nsp;*.xci|Switch Package (*.nsp)|*.ns|Switch Cartridge (*.xci)|*.xci";
			SelectNspXciDialog.Multiselect = true;
			SelectNspXciDialog.Title = "Select input NSP fIles...";

			SelectOutputDictionaryDialog.RootFolder = Environment.SpecialFolder.MyComputer;
			OutputFolderTextBox.Text = Properties.Settings.Default.OutputFolder != ""
				? Properties.Settings.Default.OutputFolder
				: Environment.GetFolderPath(Environment.SpecialFolder.Desktop);		
			VerificationComboBox.SelectedIndex = Properties.Settings.Default.Verification ? 0 : 1;
			CheckForUpdatesComboBox.SelectedIndex = Properties.Settings.Default.CheckForUpdates ? 0 : 1;

			foreach (var item in CompressionLevelComboBox.Items.Cast<ComboBoxItem>())
			{
				if ((int)item.Tag == Properties.Settings.Default.CompressionLevel)
				{
					CompressionLevelComboBox.SelectedItem = item;
					break;
				}
			}

			foreach (var item in BlockSizeComboBox.Items.Cast<ComboBoxItem>())
			{
				if ((int)item.Tag == Properties.Settings.Default.BlockSize)
				{
					BlockSizeComboBox.SelectedItem = item;
					break;
				}
			}

			SelectTempDictionaryDialog.RootFolder = Environment.SpecialFolder.MyComputer;
			if (Properties.Settings.Default.TempFolder != "")
			{
				TempFolderTextBox.Text = Properties.Settings.Default.TempFolder;
			}
			else
			{
				TempFolderTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			}
			KeepTempFilesAfterTaskComboBox.SelectedIndex = Properties.Settings.Default.KeepTempFiles ? 0: 1;
			StandByWhenTaskDoneComboBox.SelectedIndex = Properties.Settings.Default.StandbyWhenDone;

			try
			{
				LicenseTextBox.Text = File.ReadAllText(@"LICENSE");
			} catch(Exception ex)
			{
				Out.Print("LICENSE file not found!\r\n");
			}

			//System.Windows.Forms.Application.SetSuspendState(PowerState.Suspend, false, false);
			
			//CompressionLevelComboBox.SelectedIndex = 3;
			//BlockSizeComboBox.SelectedIndex = 0;
			//VerifyAfterCompressCheckBox_CheckedChanged(null, null);
			Out.Print("nsZip initialized\r\n");
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			System.Diagnostics.Process.Start(e.Uri.ToString());
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
			var t = Task.Run(() => {
				RunTask();
			});
		}

		private void RunTask()
		{
			if (TaskQueue.Items.Count == 0)
			{
				Out.Print("Nothing to do - TaskQueue empty! Please add an NSP or NSPZ!\r\n");
				return;
			}

			Dispatcher.Invoke(() =>
			{
				BusyTextBlock.Text = "Working...";
				  MainGrid.Visibility = Visibility.Hidden;
				MainGridBusy.Visibility = Visibility.Visible;
			});
			
			try
			{
				do
				{
					cleanFolders();

					var inFile = (string) TaskQueue.Items[0];
					var infileLowerCase = inFile.ToLower();
					Dispatcher.Invoke(() =>
					{
						BusyTextBlock.Text = $"Task \"{Path.GetFileNameWithoutExtension(inFile)}\" in progress...\r\nThis might take quite some time.\r\nPlease take a look at the Console Window for more Information.";
						TaskQueue.Items.RemoveAt(0);
					});

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
			} catch(Exception ex)
			{
				Out.Print(ex.StackTrace);
				Out.Print(ex.Message);
			}

			cleanFolders();

			Dispatcher.Invoke(() =>
			{
				MainGrid.Visibility = Visibility.Visible;
				MainGridBusy.Visibility = Visibility.Hidden;
			});

		}

		private void VerificationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			VerifyWhenCompressing = VerificationComboBox.SelectedIndex != 1;
			Properties.Settings.Default.Verification = VerifyWhenCompressing;
			Properties.Settings.Default.Save();
			Out.Print($"Set VerifyWhenCompressing to {VerifyWhenCompressing}\r\n");
		}

		private void CheckForUpdatesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			CheckForUpdates = CheckForUpdatesComboBox.SelectedIndex != 1;
			Properties.Settings.Default.CheckForUpdates = CheckForUpdates;
			Properties.Settings.Default.Save();
			Out.Print($"Set CheckForUpdates to {CheckForUpdates}\r\n");
		}

		private void CompressionLevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ZstdLevel = (int)((ComboBoxItem) CompressionLevelComboBox.SelectedItem).Tag;
			Properties.Settings.Default.CompressionLevel = ZstdLevel;
			Properties.Settings.Default.Save();
			Out.Print($"Set ZstdLevel to {ZstdLevel}\r\n");
		}

		private void BlockSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			BlockSize = (int)((ComboBoxItem)BlockSizeComboBox.SelectedItem).Tag;
			Properties.Settings.Default.BlockSize = BlockSize;
			Properties.Settings.Default.Save();
			Out.Print($"Set BlockSize to {BlockSize} bytes\r\n");
		}

		private void OutputFolderTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			OutputFolderPath = OutputFolderTextBox.Text;
			Properties.Settings.Default.OutputFolder = OutputFolderPath;
			Properties.Settings.Default.Save();
			Out.Print($"Set OutputFolderPath to {OutputFolderPath}\r\n");
		}

		private void OutputFolderButton_Click(object sender, RoutedEventArgs e)
		{
			SelectOutputDictionaryDialog.SelectedPath = OutputFolderPath;
			if (SelectOutputDictionaryDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
				 && !string.IsNullOrWhiteSpace(SelectOutputDictionaryDialog.SelectedPath))
			{
				OutputFolderTextBox.Text = SelectOutputDictionaryDialog.SelectedPath;
			}
		}

		private void TempFolderTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			TempFolderPath = TempFolderTextBox.Text;
			Properties.Settings.Default.TempFolder = TempFolderPath;
			Properties.Settings.Default.Save();
			Out.Print($"Set TempFolderPath to {TempFolderPath}\r\n");
		}

		private void TempFolderButton_Click(object sender, RoutedEventArgs e)
		{
			SelectTempDictionaryDialog.SelectedPath = TempFolderPath;
			if (SelectTempDictionaryDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
				 && !string.IsNullOrWhiteSpace(SelectTempDictionaryDialog.SelectedPath))
			{
				TempFolderTextBox.Text = SelectTempDictionaryDialog.SelectedPath;
			}
		}
		private void KeepTempFilesAfterTaskComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			KeepTempFilesAfterTask = KeepTempFilesAfterTaskComboBox.SelectedIndex != 1;
			Properties.Settings.Default.KeepTempFiles = KeepTempFilesAfterTask;
			Properties.Settings.Default.Save();
			Out.Print($"Set KeepTempFilesAfterTask to {KeepTempFilesAfterTask}\r\n");
		}

		private void StandByWhenTaskDoneComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			StandByWhenTaskDone = StandByWhenTaskDoneComboBox.SelectedIndex;
			Properties.Settings.Default.StandbyWhenDone = StandByWhenTaskDone;
			Properties.Settings.Default.Save();
			Out.Print($"Set StandByWhenTaskDone to {StandByWhenTaskDone}\r\n");
		}

	}
}
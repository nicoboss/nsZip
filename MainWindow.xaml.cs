using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Navigation;
using nsZip.Properties;


namespace nsZip
{
	/// <summary>
	///     Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private readonly Output Out;
		private readonly OpenFileDialog SelectNspXciDialog = new OpenFileDialog();
		private readonly OpenFileDialog SelectNspzDialog = new OpenFileDialog();
		private readonly OpenFileDialog SelectInputFileDialog = new OpenFileDialog();
		private readonly FolderBrowserDialog SelectOutputDictionaryDialog = new FolderBrowserDialog();
		private readonly FolderBrowserDialog SelectTempDictionaryDialog = new FolderBrowserDialog();
		private int BlockSize = 262144;
		private bool CheckForUpdates;
		private bool KeepTempFilesAfterTask;
		private string OutputFolderPath;

		private enum TaskDonePowerState
		{
			None,
			Suspend,
			Hibernate
		};

		private TaskDonePowerState StandByWhenTaskDone;
		private string TempFolderPath;
		private bool VerifyHashes = true;
		private int ZstdLevel = 18;

		public MainWindow()
		{
			InitializeComponent();
			Out = new Output();

			MainGrid.Visibility = Visibility.Visible;
			MainGridBusy.Visibility = Visibility.Hidden;

			SelectNspzDialog.Filter =
				"Compressed Switch File (*.nspz)|*.nspz|" +
				"XCIZ to not-installable NSP (*.xciz)|*.xciz";
			SelectNspzDialog.Multiselect = true;
			SelectNspzDialog.Title = "Select input nspz files...";

			SelectInputFileDialog.Filter =
				"All Switch Games (*.nsp;*.xci;*.nspz;*.xciz)|*.nsp;*.xci;*.nspz;*.xciz|" +
				"Switch Package (*.nsp)|*.nsp|Switch Cartridge (*.xci)|*.xci|" +
				"Compressed Switch File (*.nspz)|*.nspz|" +
				"Compressed Switch Cart (*.xciz)|*.xciz";
			SelectInputFileDialog.Multiselect = true;
			SelectInputFileDialog.Title = "Select input files...";

			SelectNspXciDialog.Filter =
				"Switch Games (*.nsp;*.xci)|*.nsp;*.xci|" +
				"Switch Package (*.nsp)|*.ns|Switch Cartridge (*.xci)|*.xci";
			SelectNspXciDialog.Multiselect = true;
			SelectNspXciDialog.Title = "Select input NSP files...";

			SelectOutputDictionaryDialog.RootFolder = Environment.SpecialFolder.MyComputer;
			OutputFolderTextBox.Text = Settings.Default.OutputFolder != ""
				? Settings.Default.OutputFolder
				: Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			VerificationComboBox.SelectedIndex = Settings.Default.Verification ? 0 : 1;
			CheckForUpdatesComboBox.SelectedIndex = Settings.Default.CheckForUpdates ? 0 : 1;

			foreach (var item in CompressionLevelComboBox.Items.Cast<ComboBoxItem>())
			{
				if ((int) item.Tag == Settings.Default.CompressionLevel)
				{
					CompressionLevelComboBox.SelectedItem = item;
					break;
				}
			}

			foreach (var item in BlockSizeComboBox.Items.Cast<ComboBoxItem>())
			{
				if ((int) item.Tag == Settings.Default.BlockSize)
				{
					BlockSizeComboBox.SelectedItem = item;
					break;
				}
			}

			SelectTempDictionaryDialog.RootFolder = Environment.SpecialFolder.MyComputer;
			if (Settings.Default.TempFolder != "")
			{
				TempFolderTextBox.Text = Settings.Default.TempFolder;
			}
			else
			{
				TempFolderTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			}

			KeepTempFilesAfterTaskComboBox.SelectedIndex = Settings.Default.KeepTempFiles ? 0 : 1;
			StandByWhenTaskDoneComboBox.SelectedIndex = Settings.Default.StandbyWhenDone;

			try
			{
				LicenseTextBox.Text = File.ReadAllText(@"LICENSE");
			}
			catch (Exception ex)
			{
				Out.Log("LICENSE file not found!\r\n");
			}

			Out.Log("nsZip initialized\r\n");
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			Process.Start(e.Uri.ToString());
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
			var t = Task.Run(() => { RunTask(); });
		}

		private void RunTask()
		{
			if (TaskQueue.Items.Count == 0)
			{
				Out.Log("Nothing to do - TaskQueue empty! Please add an NSP or NSPZ!\r\n");
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

					var inFile = (string) TaskQueue.Items[0];
					var infileLowerCase = inFile.ToLower();
					var inFileNoExtension = Path.GetFileNameWithoutExtension(inFile);

					Dispatcher.Invoke(() =>
					{
						BusyTextBlock.Text =
							$"Task \"{Path.GetFileNameWithoutExtension(inFile)}\" in progress...\r\nThis might take quite some time.\r\n" +
							$"Please take a look at the console window for more information.";
						TaskQueue.Items.RemoveAt(0);
					});

					var tl = new TaskLogic(OutputFolderPath, TempFolderPath, VerifyHashes, BlockSize, ZstdLevel, Out);
					if (tl.checkIfAlreadyExist(inFile))
					{
						continue;
					}

					tl.cleanFolders();

					try
					{
						if (infileLowerCase.EndsWith("nsp"))
						{
							tl.CompressNSP(inFile);
						}
						else if (infileLowerCase.EndsWith("xci"))
						{
							tl.CompressXCI(inFile);
						}
						else if (infileLowerCase.EndsWith("nspz"))
						{
							tl.DecompressNSPZ(inFile);
						}
						else if (infileLowerCase.EndsWith("xciz"))
						{
							tl.DecompressNSPZ(inFile);
						}
						else
						{
							throw new InvalidDataException($"Invalid file type {inFile}");
						}
					}
					catch (Exception ex)
					{
						Out.Error(ex.StackTrace + "\r\n");
						Out.Error(ex.Message + "\r\n\r\n");
					}
					finally
					{
						if (!KeepTempFilesAfterTask && tl != null)
						{
							tl.cleanFolders();
						}
					}
				} while (TaskQueue.Items.Count > 0);
			}
			catch (Exception ex)
			{
				Out.Log(ex.StackTrace + "\r\n");
				Out.Log(ex.Message);
				throw ex;
			}
			finally
			{
				Dispatcher.Invoke(() =>
				{
					MainGrid.Visibility = Visibility.Visible;
					MainGridBusy.Visibility = Visibility.Hidden;
				});

				switch (StandByWhenTaskDone)
				{
					case TaskDonePowerState.Suspend:
						Out.Log("Activate standby mode...\r\n");
						Thread.Sleep(1000);
						System.Windows.Forms.Application.SetSuspendState(PowerState.Suspend, false, false);
						break;
					case TaskDonePowerState.Hibernate:
						Out.Log("Activate hibernate mode...\r\n");
						Thread.Sleep(1000);
						System.Windows.Forms.Application.SetSuspendState(PowerState.Hibernate, false, false);
						break;
				}
			}
		}

		private void VerificationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			VerifyHashes = VerificationComboBox.SelectedIndex != 1;
			Settings.Default.Verification = VerifyHashes;
			Settings.Default.Save();
			Out.Log($"Set VerifyHashes to {VerifyHashes}\r\n");
		}

		private void CheckForUpdatesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			CheckForUpdates = CheckForUpdatesComboBox.SelectedIndex != 1;
			Settings.Default.CheckForUpdates = CheckForUpdates;
			Settings.Default.Save();
			Out.Log($"Set CheckForUpdates to {CheckForUpdates}\r\n");
		}

		private void CompressionLevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ZstdLevel = (int) ((ComboBoxItem) CompressionLevelComboBox.SelectedItem).Tag;
			Settings.Default.CompressionLevel = ZstdLevel;
			Settings.Default.Save();
			Out.Log($"Set ZstdLevel to {ZstdLevel}\r\n");
		}

		private void BlockSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			BlockSize = (int) ((ComboBoxItem) BlockSizeComboBox.SelectedItem).Tag;
			Settings.Default.BlockSize = BlockSize;
			Settings.Default.Save();
			Out.Log($"Set BlockSize to {BlockSize} bytes\r\n");
		}

		private void OutputFolderTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			OutputFolderPath = OutputFolderTextBox.Text;
			Settings.Default.OutputFolder = OutputFolderPath;
			Settings.Default.Save();
			Out.Log($"Set OutputFolderPath to {OutputFolderPath}\r\n");
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
			Settings.Default.TempFolder = TempFolderPath;
			Settings.Default.Save();
			Out.Log($"Set TempFolderPath to {TempFolderPath}\r\n");
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
			Settings.Default.KeepTempFiles = KeepTempFilesAfterTask;
			Settings.Default.Save();
			Out.Log($"Set KeepTempFilesAfterTask to {KeepTempFilesAfterTask}\r\n");
		}

		private void StandByWhenTaskDoneComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			switch (StandByWhenTaskDoneComboBox.SelectedIndex)
			{
				case 0:
					StandByWhenTaskDone = TaskDonePowerState.None;
					break;
				case 1:
					StandByWhenTaskDone = TaskDonePowerState.Suspend;
					break;
				case 2:
					StandByWhenTaskDone = TaskDonePowerState.Hibernate;
					break;
			}

			Settings.Default.StandbyWhenDone = StandByWhenTaskDoneComboBox.SelectedIndex;
			Settings.Default.Save();
			Out.Log($"Set StandByWhenTaskDone to {StandByWhenTaskDone}\r\n");
		}

		private void SelectInputFilesButton_Click(object sender, RoutedEventArgs e)
		{
			if (SelectInputFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				foreach (var filename in SelectInputFileDialog.FileNames)
				{
					ToolsTaskQueue.Items.Add(filename);
				}
			}
		}

		enum ToolsTaskType {
			ExtractTitlekeys,
			ExtractTickets,
			ExtractPfsHfs,
			ExtractRomFS
		}

		private void ExtractTitlekeys_Click(object sender, RoutedEventArgs e)
		{
			var t = Task.Run(() => { RunToolsTask(ToolsTaskType.ExtractTitlekeys); });
		}

		private void ExtractTickets_Click(object sender, RoutedEventArgs e)
		{
			var t = Task.Run(() => { RunToolsTask(ToolsTaskType.ExtractTickets); });
		}

		private void ExtractPfsHfs_Click(object sender, RoutedEventArgs e)
		{
			var t = Task.Run(() => { RunToolsTask(ToolsTaskType.ExtractPfsHfs); });
		}

		private void ExtractRomFS_Click(object sender, RoutedEventArgs e)
		{
			var t = Task.Run(() => { RunToolsTask(ToolsTaskType.ExtractRomFS); });
		}

		private void RunToolsTask(ToolsTaskType toolsTaskType)
		{
			if (ToolsTaskQueue.Items.Count == 0)
			{
				Out.Log("Nothing to do - ToolsTaskQueue empty! Please select any input file first!\r\n");
				return;
			}

			Dispatcher.Invoke(() =>
			{
				BusyTextBlock.Text = "Working...";
				MainGrid.Visibility = Visibility.Hidden;
				MainGridBusy.Visibility = Visibility.Visible;
			});

			var extractedTitleKeys = new LibHac.Keyset();
			try
			{

				var TitlekeysOutputFilePath = $"{OutputFolderPath}/titlekeys.txt";
				var TicketOutputPath = $"{OutputFolderPath}/Tickets";
				if (!Directory.Exists(TicketOutputPath))
				{
					Directory.CreateDirectory(TicketOutputPath);
				}

				do
				{
					var inFile = (string)ToolsTaskQueue.Items[0];

					var ToolsTaskText = $"ToolsTask \"{Path.GetFileNameWithoutExtension(inFile)}\" in progress...";
					Out.Event($"{ToolsTaskText}\r\n");
					Dispatcher.Invoke(() =>
					{
						BusyTextBlock.Text =
							$"{ToolsTaskText}\r\nThis might take quite some time.\r\n" +
							$"Please take a look at the console window for more information.";
						ToolsTaskQueue.Items.RemoveAt(0);
					});

					switch(toolsTaskType)
					{
						case ToolsTaskType.ExtractTitlekeys:
							FileTools.File2Titlekey(inFile, extractedTitleKeys, Out);
							break;
						case ToolsTaskType.ExtractTickets:
							FileTools.File2Tickets(inFile, TicketOutputPath, extractedTitleKeys, Out);
							break;
						default:
							throw new NotImplementedException($"Unknown ToolsTaskType: {toolsTaskType}!");
					}
					Thread.Sleep(10);

				} while (ToolsTaskQueue.Items.Count > 0);

				if (toolsTaskType == ToolsTaskType.ExtractTitlekeys)
				{
					Out.Log($"Writing to {TitlekeysOutputFilePath}\r\n");
					using (var titlekeys = new StreamWriter(File.Open(TitlekeysOutputFilePath, FileMode.Create), Encoding.ASCII))
					{
						foreach (var entry in extractedTitleKeys.TitleKeys)
						{
							var line = $"{Utils.BytesToString(entry.Key)},{Utils.BytesToString(entry.Value)}\r\n";
							titlekeys.Write(line);
							Out.Log(line);
						}
					}
				}

				Out.Event("ToolsTask done!\r\n");
			}
			catch (Exception ex)
			{
				Out.Log(ex.StackTrace + "\r\n");
				Out.Log(ex.Message);
				throw ex;
			}
			finally
			{
				Dispatcher.Invoke(() =>
				{
					MainGrid.Visibility = Visibility.Visible;
					MainGridBusy.Visibility = Visibility.Hidden;
				});
			}
		}
	}
}
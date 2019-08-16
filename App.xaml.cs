using CommandLine;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace nsZip
{
	/// <summary>
	///     Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private void Application_Startup(object sender, StartupEventArgs e)
		{
			//var outDebug = new Output();
			//var tl1 = new TaskLogic(@"T:\OUT", @"T:\", true, 262144, 18, outDebug);
			//tl1.VerifyCompressedFolder(@"T:\NSP\input.nsp");
			//return;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				try
				{
					if (ConsoleMode.TryDisablingConsoleQuickEdit())
					{
						Console.WriteLine("Console's QuickEdit mode disabled successfully");
					}
					else
					{
						Console.WriteLine("Failed to disable the Console's QuickEdit mode");
					}
				}
				catch (Exception)
				{
					Console.WriteLine("Unimportant exception occurred while disabling Console's QuickEdit mode");
				}
			}
			if (e.Args.Length > 0)
			{
				var args = Parser.Default.ParseArguments<Options>(e.Args);
				if (e.Args.Length > 0)
				{
					args.WithParsed(opts => {
						var Out = new Output();
						Directory.CreateDirectory(opts.OutputFolderPath);
						var tl = new TaskLogic(opts.OutputFolderPath, opts.TempFolderPath, true, opts.BlockSize, opts.ZstdLevel, Out);
						var inFile = opts.InputFile;
						if (tl.checkIfAlreadyExist(inFile))
						{
							Environment.Exit(0);
						}

						try
						{
							tl.cleanFolders();
							var infileLowerCase = inFile.ToLower();

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
								tl.DecompressXCIZ(inFile);
							}
							else
							{
								throw new InvalidDataException($"Invalid file type {inFile}");
							}
						}
						catch (Exception ex)
						{
							Out.LogException(ex);
						}
						finally
						{
							tl.cleanFolders();
						}
					});
				}
				Environment.Exit(0);
			}
			else
			{
				MainWindow wnd = new MainWindow();
				wnd.Show();
			}
		}
	}
}

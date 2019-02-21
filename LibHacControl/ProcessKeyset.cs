using LibHac;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace nsZip.LibHacControl
{
    static class ProcessKeyset
    {
		public static Keyset OpenKeyset()
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
					MessageBoxButton.OKCancel, MessageBoxImage.Error, MessageBoxResult.OK);
				if (dialogResult == MessageBoxResult.Cancel)
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
	}
}

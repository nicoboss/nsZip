using System;
using System.Runtime.InteropServices;

namespace nsZip
{
	public static class ConsoleMode
	{
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll")]
		private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

		[DllImport("kernel32.dll")]
		private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

		public static bool TryDisablingConsoleQuickEdit()
		{
			//Standard input device
			const int STD_INPUT_HANDLE = -10;

			const uint ENABLE_QUICK_EDIT = 0x40;
			var consoleHandle = GetStdHandle(STD_INPUT_HANDLE);

			if (GetConsoleMode(consoleHandle, out var consoleMode))
			{
				consoleMode &= ~ENABLE_QUICK_EDIT;
				if (SetConsoleMode(consoleHandle, consoleMode))
				{
					return true;
				}
			}
			return false;
		}
	}
}

using System;
using System.IO;
using System.Text;

namespace nsZip
{
	public class Output
	{

		StreamWriter debug;
		StreamWriter error;
		
		public Output()
		{
			debug = new StreamWriter(File.Open("debug.log", FileMode.Append), Encoding.UTF8);
			error = new StreamWriter(File.Open("error.log", FileMode.Append), Encoding.UTF8);
			debug.WriteLine();
			error.WriteLine();
		}

		public void Print(string text)
		{
			Console.Write(text);
		}

		public void Log(string text)
		{
			Console.Write(text);
			debug.Write(text);
			debug.Flush();
		}

		private void LogImportant(string text)
		{
			var time = DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss] ");
			var timedText = time + text;
			error.Write(timedText);
			error.Flush();
			Log(timedText);
		}

		public void Warn(string text)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			LogImportant(text);
			Console.ForegroundColor = ConsoleColor.White;
		}

		public void Event(string text)
		{
			Console.ForegroundColor = ConsoleColor.Cyan;
			LogImportant(text);
			Console.ForegroundColor = ConsoleColor.White;
		}

		public void Error(string text)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			LogImportant(text);
			Console.ForegroundColor = ConsoleColor.White;
		}

		public void LogException(Exception ex)
		{
			Print("\r\n");
			Error($"{ex.GetType()} StackTrace:\r\n"
				+ $"{ex.StackTrace}\r\n\r\n"
				+ $"{ex.GetType()}:\r\n"
				+ $"{ex.Message}\r\n");
		}

	}
}
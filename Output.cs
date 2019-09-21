using LibHac;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace nsZip
{
	public class Output : IProgressReport
	{

		StreamWriter debug;
		StreamWriter error;
		long current;
		long total;
		long cooldown = 500;
		DateTime cooldownStart;

		public Output()
			: this(1)
		{
		}

		public Output(int log)
		{
			if(log != 0)
			{
				debug = new StreamWriter(File.Open("debug.log", FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8);
				error = new StreamWriter(File.Open("error.log", FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8);
				debug.WriteLine();
				error.WriteLine();
			}
		}

		public void Print(string text)
		{
			Console.Write(text);
		}

		public void Log(string text)
		{
			Console.Write(text);

			if(debug != null)
			{
				debug.Write(text);
				debug.Flush();
			}
		}

		private void LogImportant(string text)
		{
			var time = DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss] ");
			var timedText = time + text;

			if(error != null)
			{
				error.Write(timedText);
				error.Flush();
			}

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

		public void Report(long value)
		{
			current = value;
			ReportAdd(0);
		}

		public void ReportAdd(long value)
		{
			current += value;
			var timeNow = DateTime.Now;
			var timeDiff = timeNow - cooldownStart;
			if (timeDiff.Milliseconds > cooldown && current < total)
			{
				cooldownStart = timeNow;
				var percentage = ((float)current / (float)total) * 100f;
				Console.WriteLine(string.Format("{0:00.00}%", percentage));
			}
		}

		public void SetTotal(long value)
		{
			current = 0;
			total = value;
			cooldownStart = DateTime.Now;
		}

		public void LogMessage(string message)
		{
			Console.Write($"{message}\r\n");

			if(debug != null)
			{
				debug.Write($"{message}\r\n");
				debug.Flush();
			}
		}
	}
}
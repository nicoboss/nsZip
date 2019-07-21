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

		public void Warn(string text)
		{
			var time = DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss] ");
			var timedText = time + text;
			error.Write(timedText);
			error.Flush();
			Log(timedText);
		}

		public void Event(string text)
		{
			Warn(text);
		}

		public void Error(string text)
		{
			Warn("Error:\r\n" + text);
		}

	}
}
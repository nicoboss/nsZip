using CommandLine;
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
			if (e.Args.Length > 0)
			{
				Parser.Default.ParseArguments<Options>(e.Args);
				if (e.Args.Length == 1)
					MessageBox.Show("Now opening file: \n\n" + e.Args[0]);

				System.Environment.Exit(1);
			}
			else
			{
				MainWindow wnd = new MainWindow();
				wnd.Show();
			}
		}
	}
}
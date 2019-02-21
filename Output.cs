using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace nsZip
{
	public class Output
    {
		public TextBlock TB;
		public Output(TextBlock TB_arg)
		{
			TB = TB_arg;
		}
		
		public void Print(String text)
		{
			TB.Text += text;
		}

	}
}

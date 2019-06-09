using LibHac;
using LibHac.Fs;
using nsZip.LibHacControl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nsZip
{
	public static class FileTools
	{
		public static void File2Titlekey(string inFile, Keyset keyset, Output Out)
		{
			var infileLowerCase = inFile.ToLower();
			var inFileNoExtension = Path.GetFileNameWithoutExtension(inFile);
			var inFileExtension = Path.GetExtension(inFile).ToLower();

			using (var inputFile = new FileStream(inFile, FileMode.Open, FileAccess.Read))
			{
				var titleKey = new byte[0x10];
				switch (inFileExtension)
				{
					case ".nsp":
						var pfs = new PartitionFileSystem(inputFile.AsStorage());
						ProcessNsp.GetTitlekey(pfs, keyset, Out);
						break;
					case ".xci":
						var xci = new Xci(keyset, inputFile.AsStorage());
						ProcessXci.GetTitleKeys(xci, keyset, Out);
						break;
					case ".nspz":
					case ".xciz":
						var pfsz = new PartitionFileSystem(inputFile.AsStorage());
						DecompressFs.GetTitleKeys(pfsz, keyset, Out);
						break;
					default:
						throw new NotImplementedException();
				}
			}

		}
	}
}

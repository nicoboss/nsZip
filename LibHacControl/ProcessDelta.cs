using System.IO;
using System.Text;
using LibHac;
using LibHac.IO;

namespace nsZip.LibHacControl
{
	internal static class ProcessDelta
	{
		private const string FragmentFileName = "fragment";

		public static void Process(string baseFile, string inFile, string outFile, Keyset keyset,
			IProgressReport logger)
		{
			using (var deltaFile = new StreamStorage(new FileStream(inFile, FileMode.Open, FileAccess.Read), false))
			{
				IStorage deltaStorage = deltaFile;
				try
				{
					var nca = new Nca(keyset, deltaStorage, true);
					var fs = nca.OpenSectionFileSystem(0, IntegrityCheckLevel.ErrorOnInvalid);

					if (!fs.FileExists(FragmentFileName))
					{
						throw new FileNotFoundException("Specified NCA does not contain a delta fragment");
					}

					deltaStorage = new FileStorage(fs.OpenFile(FragmentFileName, OpenMode.Read));
				}
				catch (InvalidDataException)
				{
				} // Ignore non-NCA3 files

				var delta = new DeltaFragment(deltaStorage);

				if (baseFile != null)
				{
					using (var baseFileStorage =
						new StreamStorage(new FileStream(baseFile, FileMode.Open, FileAccess.Read), false))
					{
						delta.SetBaseStorage(baseFileStorage);

						if (outFile != null)
						{
							using (var outFileStream =
								new FileStream(outFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
							{
								var patchedStorage = delta.GetPatchedStorage();
								patchedStorage.CopyToStream(outFileStream, patchedStorage.Length, logger);
							}
						}
					}
				}

				logger.LogMessage(delta.Print());
			}
		}

		private static string Print(this DeltaFragment delta)
		{
			var colLen = 36;
			var sb = new StringBuilder();
			sb.AppendLine();

			sb.AppendLine("Delta Fragment:");
			LibHacControl.Print.PrintItem(sb, colLen, "Magic:", delta.Header.Magic);
			LibHacControl.Print.PrintItem(sb, colLen, "Base file size:", $"0x{delta.Header.OriginalSize:x12}");
			LibHacControl.Print.PrintItem(sb, colLen, "New file size:", $"0x{delta.Header.NewSize:x12}");
			LibHacControl.Print.PrintItem(sb, colLen, "Fragment header size:",
				$"0x{delta.Header.FragmentHeaderSize:x12}");
			LibHacControl.Print.PrintItem(sb, colLen, "Fragment body size:", $"0x{delta.Header.FragmentBodySize:x12}");

			return sb.ToString();
		}
	}
}
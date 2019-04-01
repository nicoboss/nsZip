using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Navigation;
using LibHac;
using LibHac.IO;
using nsZip.LibHacControl;
using nsZip.LibHacExtensions;
using nsZip.Properties;
using CommandLine;

namespace nsZip
{
	class TaskLogic
	{
		Output Out;
		int BlockSize = 262144;
		int ZstdLevel = 18;
		string OutputFolderPath;
		string decryptedDir;
		string encryptedDir;
		string compressedDir;
		bool VerifyHashes = true;

		public TaskLogic(string OutputFolderPathArg, string TempFolderPathArg, bool VerifyHashesArg, int bs, int lv, Output OutArg)
		{
			OutputFolderPath = OutputFolderPathArg;
			decryptedDir = Path.Combine(TempFolderPathArg, "decrypted");
			encryptedDir = Path.Combine(TempFolderPathArg, "encrypted");
			compressedDir = Path.Combine(TempFolderPathArg, "compressed");
			BlockSize = bs;
			int ZstdLevel = lv;
			VerifyHashes = VerifyHashesArg;
			Out = OutArg;
		}

		public void cleanFolder(string folderName)
		{
			Thread.Sleep(50); //Wait for files to be closed!
			if (Directory.Exists(folderName))
			{
				Directory.Delete(folderName, true);
			}
			Thread.Sleep(50); //Wait for folder deletion!
			Directory.CreateDirectory(folderName);
		}

		public void cleanFolders()
		{
			cleanFolder(decryptedDir);
			cleanFolder(encryptedDir);
			cleanFolder(compressedDir);
		}

		public void CompressNSP(string nspFile)
		{
			var nspFileNoExtension = Path.GetFileNameWithoutExtension(nspFile);
			Out.Print($"Task CompressNSP \"{nspFileNoExtension}\" started\r\n");
			var keyset = ProcessKeyset.OpenKeyset();
			using (var inputFile = new FileStream(nspFile, FileMode.Open, FileAccess.Read))
			{
				var pfs = new PartitionFileSystem(inputFile.AsStorage());
				ProcessNsp.Decrypt(pfs, decryptedDir, VerifyHashes, keyset, Out);
				TrimDeltaNCA.Process(decryptedDir, keyset, Out);
				CompressFolder.Compress(Out, decryptedDir, compressedDir, BlockSize, ZstdLevel);

				if (VerifyHashes)
				{
					var dirDecryptedReal = new DirectoryInfo(decryptedDir);
					var dirDecryptedRealCount = dirDecryptedReal.GetFiles().Length;
					cleanFolder(decryptedDir);
					var compressedFs = new LocalFileSystem(compressedDir);
					DecompressFs.ProcessFs(compressedFs, decryptedDir, Out);
					UntrimDeltaNCA.Process(decryptedDir, pfs, keyset, Out);

					var dirDecrypted = new DirectoryInfo(decryptedDir);
					var dirDecryptedCount = dirDecrypted.GetFiles().Length;
					if (dirDecryptedRealCount != dirDecryptedCount)
					{
						throw new FileNotFoundException();
					}

					foreach (var file in dirDecrypted.GetFiles("*.nca"))
					{
						EncryptNCA.Encrypt(file.FullName, encryptedDir, false, true, keyset, Out);
					}
				}
			}
			var nspzOutPath = Path.Combine(OutputFolderPath, nspFileNoExtension);
			FolderTools.FolderToNSP(compressedDir, $"{nspzOutPath}.nspz");
			Out.Print($"Task CompressNSP \"{nspFileNoExtension}\" completed!\r\n");
		}

		public void CompressXCI(string xciFile)
		{
			var xciFileNoExtension = Path.GetFileNameWithoutExtension(xciFile);
			Out.Print($"Task CompressXCI \"{xciFileNoExtension}\" started\r\n");
			var keyset = ProcessKeyset.OpenKeyset();
			ProcessXci.Decrypt(xciFile, decryptedDir, VerifyHashes, keyset, Out);
			CompressFolder.Compress(Out, decryptedDir, compressedDir, BlockSize, ZstdLevel);

			if (VerifyHashes)
			{
				var dirDecryptedReal = new DirectoryInfo(decryptedDir);
				var dirDecryptedRealCount = dirDecryptedReal.GetFiles().Length;
				cleanFolder(decryptedDir);
				var compressedFs = new LocalFileSystem(compressedDir);
				DecompressFs.ProcessFs(compressedFs, decryptedDir, Out);

				var dirDecrypted = new DirectoryInfo(decryptedDir);
				var dirDecryptedCount = dirDecrypted.GetFiles().Length;
				if (dirDecryptedRealCount != dirDecryptedCount)
				{
					throw new FileNotFoundException();
				}

				foreach (var file in dirDecrypted.GetFiles("*.nca"))
				{
					EncryptNCA.Encrypt(file.FullName, encryptedDir, false, true, keyset, Out);
				}
			}

			var xciOutPath = Path.Combine(OutputFolderPath, xciFileNoExtension);
			FolderTools.FolderToNSP(compressedDir, $"{xciOutPath}.xciz");
			Out.Print($"Task CompressXCI \"{xciFileNoExtension}\" completed!\r\n");
		}

		public void DecompressNSPZ(string nspzFile)
		{
			var nspzFileNoExtension = Path.GetFileNameWithoutExtension(nspzFile);
			Out.Print($"Task DecompressNSPZ \"{nspzFileNoExtension}\" started\r\n");
			var keyset = ProcessKeyset.OpenKeyset();
			ProcessNsp.Decompress(nspzFile, decryptedDir, Out);
			UntrimAndEncrypt(keyset);
			var nspOutPath = Path.Combine(OutputFolderPath, nspzFileNoExtension);
			FolderTools.FolderToNSP(encryptedDir, $"{nspOutPath}.nsp");
			Out.Print($"Task DecompressNSPZ \"{nspzFileNoExtension}\" completed!\r\n");
		}

		public void UntrimAndEncrypt(Keyset keyset)
		{
			FolderTools.ExtractTitlekeys(decryptedDir, keyset, Out);

			var dirDecrypted = new DirectoryInfo(decryptedDir);
			foreach (var file in dirDecrypted.GetFiles())
			{
				if (file.Name.EndsWith(".tca"))
				{
					continue;
				}

				if (file.Name.EndsWith(".nca"))
				{
					EncryptNCA.Encrypt(file.FullName, encryptedDir, true, VerifyHashes, keyset, Out);
					file.Delete();
				}
				else
				{
					file.MoveTo(Path.Combine(encryptedDir, file.Name));
				}
			}

			var encryptedFs = new LocalFileSystem(encryptedDir);
			UntrimDeltaNCA.Process(decryptedDir, encryptedFs, keyset, Out);

			foreach (var file in dirDecrypted.GetFiles("*.nca"))
			{
				EncryptNCA.Encrypt(file.FullName, encryptedDir, true, VerifyHashes, keyset, Out);
			}
		}
	}
}

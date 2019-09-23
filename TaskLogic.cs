using System;
using System.IO;
using System.Linq;
using System.Threading;
using LibHac;
using LibHac.Fs;
using nsZip.LibHacControl;
using nsZip.LibHacExtensions;

namespace nsZip
{
	class TaskLogic
	{
		Output Out;
		int BlockSize = 262144;
		int ZstdLevel = 18;
		int MaxDegreeOfParallelism = 0;
		string OutputFolderPath;
		string decryptedDir;
		string encryptedDir;
		string compressedDir;
		bool VerifyHashes = true;

		public TaskLogic(string OutputFolderPathArg, string TempFolderPathArg, bool VerifyHashesArg, int bs, int lv, Output OutArg)
			: this(OutputFolderPathArg, TempFolderPathArg, VerifyHashesArg, bs, lv, 0, OutArg)
		{
		}

		public TaskLogic(string OutputFolderPathArg, string TempFolderPathArg, bool VerifyHashesArg, int bs, int lv, int mt, Output OutArg)
		{
			OutputFolderPath = OutputFolderPathArg;
			
			if (!Directory.Exists(OutputFolderPath))
			{
				Directory.CreateDirectory(OutputFolderPath);
			}

			decryptedDir = Path.Combine(TempFolderPathArg, "decrypted");
			encryptedDir = Path.Combine(TempFolderPathArg, "encrypted");
			compressedDir = Path.Combine(TempFolderPathArg, "compressed");
			BlockSize = bs;
			ZstdLevel = lv;
			MaxDegreeOfParallelism = mt;
			VerifyHashes = VerifyHashesArg;
			Out = OutArg;
		}

		public bool checkIfAlreadyExist(string inFile)
		{
			var infileLowerCase = inFile.ToLower();
			var inFileNoExtension = Path.GetFileNameWithoutExtension(inFile);

			if (infileLowerCase.EndsWith("nsp") && File.Exists($"{Path.Combine(OutputFolderPath, inFileNoExtension)}.nspz"))
			{
				Out.Event($"Task CompressNSP \"{inFileNoExtension}.nspz\" skipped as it already exists in the output directory\r\n");
				return true;
			}

			if (infileLowerCase.EndsWith("xci") && File.Exists($"{Path.Combine(OutputFolderPath, inFileNoExtension)}.xciz"))
			{
				Out.Event($"Task CompressXCI \"{inFileNoExtension}.xciz\" skipped as it already exists in the output directory\r\n");
				return true;
			}

			if (infileLowerCase.EndsWith("nspz") && File.Exists($"{Path.Combine(OutputFolderPath, inFileNoExtension)}.nsp"))
			{
				Out.Event($"Task DecompressNSPZ \"{inFileNoExtension}.nsp\" skipped as it already exists in the output directory\r\n");
				return true;
			}

			if (infileLowerCase.EndsWith("xciz") && File.Exists($"{Path.Combine(OutputFolderPath, inFileNoExtension)}.xci"))
			{
				Out.Event($"Task DecompressXCIZ \"{inFileNoExtension}.xci\" skipped as it already exists in the output directory\r\n");
				return true;
			}

			return false;
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

		public void VerifyCompressedFolder(string nspFile)
		{
			var nspFileNoExtension = Path.GetFileNameWithoutExtension(nspFile);
			Out.Event($"Task VerifyCompressedFolder \"{nspFileNoExtension}\" started\r\n");
			var keyset = ProcessKeyset.OpenKeyset();
			using (var inputFile = new FileStream(nspFile, FileMode.Open, FileAccess.Read))
			using (var inputFileStorage = inputFile.AsStorage())
			{
				var pfs = new PartitionFileSystem(inputFileStorage);
				ProcessNsp.GetTitlekey(pfs, keyset, Out);

				var dirDecryptedReal = new DirectoryInfo(decryptedDir);
				var dirDecryptedRealCount = dirDecryptedReal.GetFiles().Length;
				cleanFolder(decryptedDir);
				var compressedFs = new LocalFileSystem(compressedDir);
				var decryptedFs = new LocalFileSystem(decryptedDir);
				DecompressFs.ProcessFs(compressedFs, decryptedFs, Out);
				UntrimDeltaNCA.Process(decryptedDir, pfs, keyset, Out);
				EncryptNCA.Encrypt(decryptedFs, null, true, keyset, Out);
			}

			Out.Event($"Task VerifyCompressedFolder \"{nspFileNoExtension}\" completed!\r\n");
		}

		public void CompressNSP(string nspFile)
		{
			var nspFileNoExtension = Path.GetFileNameWithoutExtension(nspFile);
			Out.Event($"Task CompressNSP \"{nspFileNoExtension}\" started\r\n");
			var keyset = ProcessKeyset.OpenKeyset();
			using (var inputFile = new FileStream(nspFile, FileMode.Open, FileAccess.Read))
			using (var inputFileStorage = inputFile.AsStorage())
			{
				var pfs = new PartitionFileSystem(inputFileStorage);
				ProcessNsp.Decrypt(pfs, decryptedDir, VerifyHashes, keyset, Out);
				TrimDeltaNCA.Process(decryptedDir, keyset, Out);
				CompressFolder.Compress(Out, decryptedDir, compressedDir, BlockSize, ZstdLevel, MaxDegreeOfParallelism);

				if (VerifyHashes)
				{
					var dirDecryptedReal = new DirectoryInfo(decryptedDir);
					var dirDecryptedRealCount = dirDecryptedReal.GetFiles().Length;
					cleanFolder(decryptedDir);
					var compressedFs = new LocalFileSystem(compressedDir);
					var decryptedFs = new LocalFileSystem(decryptedDir);
					DecompressFs.ProcessFs(compressedFs, decryptedFs, Out);
					UntrimDeltaNCA.Process(decryptedDir, pfs, keyset, Out);

					var dirDecrypted = new DirectoryInfo(decryptedDir);
					var dirDecryptedCount = dirDecrypted.GetFiles().Length;
					if (dirDecryptedRealCount != dirDecryptedCount)
					{
						throw new FileNotFoundException();
					}

					EncryptNCA.Encrypt(compressedFs, null, true, keyset, Out);
				}
			}

			var compressedDirFs = new LocalFileSystem(compressedDir);
			var OutputFolderPathFs = new LocalFileSystem(OutputFolderPath);

			using (var outFile =
				FolderTools.CreateOrOverwriteFileOpen(OutputFolderPathFs, $"{nspFileNoExtension}.nspz"))
			{
				FolderTools.FolderToNSP(compressedDirFs, outFile);
			}
				
			Out.Event($"Task CompressNSP \"{nspFileNoExtension}\" completed!\r\n");
		}

		public void CompressXCI(string xciFile)
		{
			var xciFileNoExtension = Path.GetFileNameWithoutExtension(xciFile);
			Out.Event($"Task CompressXCI \"{xciFileNoExtension}\" started\r\n");
			var keyset = ProcessKeyset.OpenKeyset();
			ProcessXci.Decrypt(xciFile, decryptedDir, VerifyHashes, keyset, Out);
			CompressFolder.Compress(Out, decryptedDir, compressedDir, BlockSize, ZstdLevel, MaxDegreeOfParallelism);

			if (VerifyHashes)
			{
				var decryptedFs = new LocalFileSystem(decryptedDir);
				var dirDecryptedRealCount = decryptedFs.GetEntryCount(OpenDirectoryMode.Files);
				cleanFolder(decryptedDir);
				var compressedFs = new LocalFileSystem(compressedDir);
				DecompressFs.ProcessFs(compressedFs, decryptedFs, Out);

				var dirDecryptedCount = decryptedFs.GetEntryCount(OpenDirectoryMode.Files);
				if (dirDecryptedRealCount != dirDecryptedCount)
				{
					throw new FileNotFoundException();
				}

				EncryptNCA.Encrypt(decryptedFs, null, true, keyset, Out);
			}

			var compressedDirFs = new LocalFileSystem(compressedDir);
			var xciOutPath = Path.Combine(OutputFolderPath, xciFileNoExtension);
			FolderTools.FolderToXCI(compressedDirFs, $"{xciOutPath}.xciz", keyset);
			Out.Event($"Task CompressXCI \"{xciFileNoExtension}\" completed!\r\n");
		}

		public void DecompressNSPZ(string nspzFile)
		{
			var nspzFileNoExtension = Path.GetFileNameWithoutExtension(nspzFile);
			Out.Event($"Task DecompressNSPZ \"{nspzFileNoExtension}\" started\r\n");
			var keyset = ProcessKeyset.OpenKeyset();
			ProcessNsp.Decompress(nspzFile, decryptedDir, Out);
			UntrimAndEncrypt(keyset);
			var nspOutPath = Path.Combine(OutputFolderPath, nspzFileNoExtension);

			var encryptedDirFs = new LocalFileSystem(encryptedDir);
			var OutputFolderPathFs = new LocalFileSystem(OutputFolderPath);

			using (var outFile =
				FolderTools.CreateOrOverwriteFileOpen(OutputFolderPathFs, $"{nspzFileNoExtension}.nsp"))
			{
				FolderTools.FolderToNSP(encryptedDirFs, outFile);
			}

			Out.Event($"Task DecompressNSPZ \"{nspzFileNoExtension}\" completed!\r\n");
		}

		public void DecompressXCIZ(string nspzFile)
		{
			var nspzFileNoExtension = Path.GetFileNameWithoutExtension(nspzFile);
			Out.Event($"Task DecompressXCIZ \"{nspzFileNoExtension}\" started\r\n");
			var keyset = ProcessKeyset.OpenKeyset();
			ProcessNsp.Decompress(nspzFile, decryptedDir, Out);
			UntrimAndEncrypt(keyset);
			var nspOutPath = Path.Combine(OutputFolderPath, nspzFileNoExtension);
			var encryptedDirFs = new LocalFileSystem(encryptedDir);
			FolderTools.FolderToXCI(encryptedDirFs, $"{nspOutPath}.xci", keyset);
			Out.Event($"Task DecompressXCIZ \"{nspzFileNoExtension}\" completed!\r\n");
		}

		public void UntrimAndEncrypt(Keyset keyset)
		{
			FolderTools.ExtractTitlekeys(decryptedDir, keyset, Out);

			var decryptedFs = new LocalFileSystem(decryptedDir);
			var encryptedFs = new LocalFileSystem(encryptedDir);
			EncryptNCA.Encrypt(decryptedFs, encryptedFs, VerifyHashes, keyset, Out);

			var dirDecrypted = new DirectoryInfo(decryptedDir);

			foreach (var file in decryptedFs.EnumerateEntries()
				.Where(item => item.Type == DirectoryEntryType.File && !item.Name.EndsWith(".tca")))
			{
				if (!file.Name.EndsWith(".nca"))
				{
					using (var srcFile = decryptedFs.OpenFile(file.FullPath, OpenMode.Read))
					using (var destFile = FolderTools.CreateAndOpen(file, encryptedFs, file.Name, file.Size))
					{
						srcFile.CopyTo(destFile);
					}
				}

				decryptedFs.DeleteFile(file.FullPath);
			}

			UntrimDeltaNCA.Process(decryptedDir, encryptedFs, keyset, Out);
			EncryptNCA.Encrypt(decryptedFs, encryptedFs, VerifyHashes, keyset, Out);
		}
	}
}

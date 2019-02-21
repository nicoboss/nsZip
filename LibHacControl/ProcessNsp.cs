using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using LibHac;
using LibHac.IO;

namespace nsZip.LibHacControl
{
	internal static class ProcessNsp
	{
		public static void Process(string inFile, string OutDir, Output Out)
		{
			using (var file = new FileStream(inFile, FileMode.Open, FileAccess.Read))
			{
				var pfs = new PartitionFileSystem(file.AsStorage());
				Out.Print(pfs.Print());
				pfs.Extract(OutDir);
			}
		}

		private static string Print(this PartitionFileSystem pfs)
		{
			const int colLen = 36;
			const int fileNameLen = 39;

			var sb = new StringBuilder();
			sb.AppendLine();

			sb.AppendLine("PFS0:");

			LibHacControl.Print.PrintItem(sb, colLen, "Magic:", pfs.Header.Magic);
			LibHacControl.Print.PrintItem(sb, colLen, "Number of files:", pfs.Header.NumFiles);

			for (var i = 0; i < pfs.Files.Length; i++)
			{
				var file = pfs.Files[i];

				var label = i == 0 ? "Files:" : "";
				var offsets = $"{file.Offset:x12}-{file.Offset + file.Size:x12}{file.HashValidity.GetValidityString()}";
				var data = $"pfs0:/{file.Name}".PadRight(fileNameLen) + offsets;

				LibHacControl.Print.PrintItem(sb, colLen, label, data);
			}

			return sb.ToString();
		}

		public static void CreateNsp(ulong TitleId, string nspFilename, SwitchFs switchFs, IProgressReport logger)
		{
			if (TitleId == 0)
			{
				logger.LogMessage("Title ID must be specified to save title");
				return;
			}

			if (!switchFs.Titles.TryGetValue(TitleId, out var title))
			{
				logger.LogMessage($"Could not find title {TitleId:X16}");
				return;
			}

			var builder = new Pfs0Builder();

			foreach (var nca in title.Ncas)
			{
				builder.AddFile(nca.Filename, nca.GetStorage().AsStream());
			}

			var ticket = new Ticket
			{
				SignatureType = TicketSigType.Rsa2048Sha256,
				Signature = new byte[0x200],
				Issuer = "Root-CA00000003-XS00000020",
				FormatVersion = 2,
				RightsId = title.MainNca.Header.RightsId,
				TitleKeyBlock = title.MainNca.TitleKey,
				CryptoType = title.MainNca.Header.CryptoType2,
				SectHeaderOffset = 0x2C0
			};
			var ticketBytes = ticket.GetBytes();
			builder.AddFile($"{ticket.RightsId.ToHexString()}.tik", new MemoryStream(ticketBytes));

			var thisAssembly = Assembly.GetExecutingAssembly();
			var cert = thisAssembly.GetManifestResourceStream("hactoolnet.CA00000003_XS00000020");
			builder.AddFile($"{ticket.RightsId.ToHexString()}.cert", cert);


			using (var outStream = new FileStream(nspFilename, FileMode.Create, FileAccess.ReadWrite))
			{
				builder.Build(outStream, logger);
			}
		}
	}
}
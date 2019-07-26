using System;
using System.Linq;
using System.Text;
using LibHac;
using LibHac.IO;

namespace nsZip.LibHacControl
{
	internal static class Print
	{
		public static void PrintItem(StringBuilder sb, int colLen, string prefix, object data)
		{
			if (data is byte[] byteData)
			{
				sb.MemDump(prefix.PadRight(colLen), byteData);
			}
			else
			{
				sb.AppendLine(prefix.PadRight(colLen) + data);
			}
		}

		public static string GetValidityString(this Validity validity)
		{
			switch (validity)
			{
				case Validity.Invalid: return " (FAIL)";
				case Validity.Valid: return " (GOOD)";
				default: return string.Empty;
			}
		}

		public static void PrintIvfcHash(StringBuilder sb, int colLen, int indentSize, IvfcHeader ivfcInfo,
			IntegrityStorageType type)
		{
			var prefix = new string(' ', indentSize);
			var prefix2 = new string(' ', indentSize + 4);

			if (type == IntegrityStorageType.RomFs)
			{
				PrintItem(sb, colLen,
					$"{prefix}Master Hash{ivfcInfo.LevelHeaders[0].HashValidity.GetValidityString()}:",
					ivfcInfo.MasterHash);
			}

			PrintItem(sb, colLen, $"{prefix}Magic:", ivfcInfo.Magic);
			PrintItem(sb, colLen, $"{prefix}Version:", ivfcInfo.Version);

			if (type == IntegrityStorageType.Save)
			{
				PrintItem(sb, colLen, $"{prefix}Salt Seed:", ivfcInfo.SaltSource);
			}

			var levelCount = Math.Max(ivfcInfo.NumLevels - 1, 0);
			if (type == IntegrityStorageType.Save)
			{
				levelCount = 4;
			}

			var offsetLen = type == IntegrityStorageType.Save ? 16 : 12;

			for (var i = 0; i < levelCount; i++)
			{
				var level = ivfcInfo.LevelHeaders[i];
				long hashOffset = 0;

				if (i != 0)
				{
					hashOffset = ivfcInfo.LevelHeaders[i - 1].Offset;
				}

				sb.AppendLine($"{prefix}Level {i}{level.HashValidity.GetValidityString()}:");
				PrintItem(sb, colLen, $"{prefix2}Data Offset:", $"0x{level.Offset.ToString($"x{offsetLen}")}");
				PrintItem(sb, colLen, $"{prefix2}Data Size:", $"0x{level.Size.ToString($"x{offsetLen}")}");
				PrintItem(sb, colLen, $"{prefix2}Hash Offset:", $"0x{hashOffset.ToString($"x{offsetLen}")}");
				PrintItem(sb, colLen, $"{prefix2}Hash BlockSize:", $"0x{1 << level.BlockSizePower:x8}");
			}
		}

		public static string PrintXci(this Xci xci)
		{
			const int colLen = 36;

			var sb = new StringBuilder();
			sb.AppendLine();

			sb.AppendLine("XCI:");

			PrintItem(sb, colLen, "Magic:", xci.Header.Magic);
			PrintItem(sb, colLen, $"Header Signature{xci.Header.SignatureValidity.GetValidityString()}:", xci.Header.Signature);
			PrintItem(sb, colLen, $"Header Hash{xci.Header.PartitionFsHeaderValidity.GetValidityString()}:", xci.Header.RootPartitionHeaderHash);
			PrintItem(sb, colLen, "Cartridge Type:", GetCartridgeType(xci.Header.RomSize));
			PrintItem(sb, colLen, "Cartridge Size:", $"0x{Util.MediaToReal(xci.Header.ValidDataEndPage + 1):x12}");
			PrintItem(sb, colLen, "Header IV:", xci.Header.AesCbcIv);

			PrintPartition(sb, colLen, xci.OpenPartition(XciPartitionType.Root), XciPartitionType.Root);

			foreach (XciPartitionType type in Enum.GetValues(typeof(XciPartitionType)))
			{
				if (type == XciPartitionType.Root || !xci.HasPartition(type)) continue;

				XciPartition partition = xci.OpenPartition(type);
				PrintPartition(sb, colLen, partition, type);
			}

			return sb.ToString();
		}

		private static void PrintPartition(StringBuilder sb, int colLen, XciPartition partition, XciPartitionType type)
		{
			const int fileNameLen = 57;

			sb.AppendLine($"{type.ToString()} Partition:{partition.HashValidity.GetValidityString()}");
			PrintItem(sb, colLen, "    Magic:", partition.Header.Magic);
			PrintItem(sb, colLen, "    Offset:", $"{partition.Offset:x12}");
			PrintItem(sb, colLen, "    Number of files:", partition.Files.Length);

			string name = type.GetFileName();

			if (partition.Files.Length > 0 && partition.Files.Length < 100)
			{
				for (int i = 0; i < partition.Files.Length; i++)
				{
					PartitionFileEntry file = partition.Files[i];

					string label = i == 0 ? "    Files:" : "";
					string offsets = $"{file.Offset:x12}-{file.Offset + file.Size:x12}{file.HashValidity.GetValidityString()}";
					string data = $"{name}:/{file.Name}".PadRight(fileNameLen) + offsets;

					PrintItem(sb, colLen, label, data);
				}
			}
		}

		private static string GetDisplayName(string name)
		{
			switch (name)
			{
				case "rootpt": return "Root";
				case "update": return "Update";
				case "normal": return "Normal";
				case "secure": return "Secure";
				case "logo": return "Logo";
				default: return name;
			}
		}

		private static string GetCartridgeType(RomSize size)
		{
			switch (size)
			{
				case RomSize.Size1Gb: return "1GB";
				case RomSize.Size2Gb: return "2GB";
				case RomSize.Size4Gb: return "4GB";
				case RomSize.Size8Gb: return "8GB";
				case RomSize.Size16Gb: return "16GB";
				case RomSize.Size32Gb: return "32GB";
				default: return string.Empty;
			}
		}
	}
}
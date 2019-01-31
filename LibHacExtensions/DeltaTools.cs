using LibHac.IO;

namespace nsZip.LibHacExtensions
{
	public static class DeltaTools
	{
		public const string Ndv0Magic = "NDV0";
		public const string Tdv0Magic = "TDV0";
		public static readonly byte[] LCA3Macic = {0x4c, 0x43, 0x41, 0x33};

		public static int ReadInt(FileReader reader, int bytes)
		{
			switch (bytes)
			{
				case 1: return reader.ReadUInt8();
				case 2: return reader.ReadUInt16();
				case 3: return reader.ReadUInt24();
				case 4: return reader.ReadInt32();
				default: return 0;
			}
		}
	}

	internal class DeltaFragmentSegment
	{
		public long SourceOffset { get; set; }
		public int Size { get; set; }
		public bool IsInOriginal { get; set; }
	}

	public class DeltaFragmentHeader
	{
		public DeltaFragmentHeader(IFile header)
		{
			var reader = new FileReader(header);

			Magic = reader.ReadAscii(4);
			OriginalSize = reader.ReadInt64(8);
			NewSize = reader.ReadInt64();
			FragmentHeaderSize = reader.ReadInt64();
			FragmentBodySize = reader.ReadInt64();
		}

		public string Magic { get; }
		public long OriginalSize { get; }
		public long NewSize { get; }
		public long FragmentHeaderSize { get; }
		public long FragmentBodySize { get; }
	}
}
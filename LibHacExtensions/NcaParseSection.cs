using LibHac;
using LibHac.Fs.NcaUtils;

namespace nsZip.LibHacExtensions
{
	public static class NcaParseSection
	{
		public static NcaFsHeader ParseSection(NcaHeader Header, int index)
		{
			var entry = Header.SectionEntries[index];
			var header = Header.FsHeaders[index];
			if (entry.MediaStartOffset == 0)
			{
				return null;
			}

			var sect = new NcaFsHeader();

			sect.SectionNum = index;
			sect.Offset = Utils.MediaToReal(entry.MediaStartOffset);
			sect.Size = Utils.MediaToReal(entry.MediaEndOffset) - sect.Offset;
			sect.Header = header;
			sect.Type = header.Type;

			return sect;
		}
	}
}
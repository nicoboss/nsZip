using LibHac;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nsZip
{
	public static class TitleKeyTools
	{
		public static void ExtractKey(Stream TicketFile, string filename, Keyset keyset, Output Out)
		{
			var titleKey = new byte[0x10];
			TicketFile.Seek(0x180, SeekOrigin.Begin);
			TicketFile.Read(titleKey, 0, 0x10);
			var ticketNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
			if (!ticketNameWithoutExtension.TryToBytes(out var rightsId))
			{
				throw new InvalidDataException(
					$"Invalid rights ID \"{ticketNameWithoutExtension}\" as ticket file name");
			}

			keyset.TitleKeys[rightsId] = titleKey;
			Out.Log($"titleKey: {Utils.BytesToString(rightsId)},{Utils.BytesToString(titleKey)}\r\n");
		}
	}
}

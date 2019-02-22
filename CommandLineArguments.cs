using CommandLine;

namespace nsZip
{
	internal class CommandLineArguments
	{
		[Option('i', "input", Required = false, HelpText = "NSP, XCI, NSPZ, XCIZ input file to compress/decompress")]
		public bool InputFile { get; set; }

		[Option('l', "level", Required = false, HelpText = "Compression level [1-19] (default: 18)")]
		public bool CompressionLevel { get; set; }

		[Option('b', "bs", Required = false, HelpText = "Block Size in bytes (default: 262144)")]
		public bool BlockSize { get; set; }
	}
}
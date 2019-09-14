using CommandLine;

namespace nsZip
{
	internal class Options
	{
		[Option('i', "input", Required = true, HelpText = "NSP, XCI, NSPZ, XCIZ input file to compress/decompress")]
		public string InputFile { get; set; }

		[Option('o', "output", Required = false, Default = "./out/", HelpText = "Output Folder (default: ./out/")]
		public string OutputFolderPath { get; set; }

		[Option('t', "temp", Required = false, Default = "./temp/", HelpText = "Temp Folder (default: ./temp/")]
		public string TempFolderPath { get; set; }

		[Option('l', "level", Required = false, Default = 18, HelpText = "Compression level [1-22] (default: 18)")]
		public int ZstdLevel { get; set; }

		[Option('b', "bs", Required = false, Default = 262144, HelpText = "Block Size in bytes (default: 262144)")]
		public int BlockSize { get; set; }

        [Option("mt", Required = false, Default = 0, HelpText = "Number of threads to use for compression (default: logical CPUs)")]
        public int MaxDegreeOfParallelism { get; set; }
    }
}
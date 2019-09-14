using System;
using System.IO;

namespace LibHac.IO
{
	public class FilePositionStorage : StorageBase
	{
		private IFile BaseFile { get; }
		private long Position = 0;

		public FilePositionStorage(IFile baseFile) 
			: this(baseFile, false)
		{
		}

		public FilePositionStorage(IFile baseFile, bool canAutoExpand)
		{
			BaseFile = baseFile;
			CanAutoExpand = canAutoExpand;
		}

		protected override void ReadImpl(Span<byte> destination, long offset)
		{
			Position = offset;
			BaseFile.Read(destination, offset);
			Position += destination.Length;
		}

		public void Read(Span<byte> destination)
		{
			BaseFile.Read(destination, Position);
			Position += destination.Length;
		}

		protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
		{
			Position = offset;
			ExtendFileSize(offset + source.Length);
			BaseFile.Write(source, offset);
			Position += source.Length;
		}

		public void Write(ReadOnlySpan<byte> source)
		{
			ExtendFileSize(Position + source.Length);
			BaseFile.Write(source, Position);
			Position += source.Length;
		}

		public override void Flush()
		{
			BaseFile.Flush();
		}

		public override long GetSize() => BaseFile.GetSize();

		public override void SetSize(long size)
		{
			BaseFile.SetSize(size);
		}

		public void ExtendFileSize(long newSize)
		{
			var fileSize = GetSize();
			if (newSize > fileSize)
			{
				BaseFile.SetSize(newSize);
			}
		}

		public void Seek(long value, SeekOrigin origin = SeekOrigin.Begin)
		{
			switch (origin)
			{
				case SeekOrigin.Begin:
					Position = value;
					break;
				case SeekOrigin.Current:
					Position += value;
					break;
				case SeekOrigin.End:
					Position = GetSize() - value;
					break;
				default:
					throw new NotImplementedException($"Unknown SeekOrigin: {origin}");
			}
		}

	}
}
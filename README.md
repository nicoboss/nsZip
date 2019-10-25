# nsZip

Work in progress file format for compressed Nintendo Switch games and a tool to compress/decompress them.

## Please note that nsZip currently only supports the depreciated NSZP but not the new homebrew compatible NSZ file format so I highly recommend to use https://github.com/nicoboss/nsz instead.


# NSZ file format:
## Header:
| Offset|Size |Description                    |
|-------|-----|-------------------------------|
|0x00   |0x05 |XOR-Encrypted magic ("nsZip")  |
|0x05   |0x05 |Random key to decrypt the magic|
|0x0A   |0x01 |Version (for now 0x00)         |
|0x0B   |0x01 |nsZip Type                     |

## Type 0:
| Offset         |Size             |Description               |
|----------------|-----------------|--------------------------|
|0x0C            |0x01             |Compression algorithm     |
|0x0D            |File size - 0x1D |Full compressed file      |
|File size - 0x10|0x10 (first half)|SHA256 of everything above|

## Type 1:
| Offset         |Size             |Description                        |
|----------------|-----------------|-----------------------------------|
|0x0C            |0x05             |bs = Decompressed Block Size       |
|0x11            |0x04             |Amount of Blocks                   |
|0x15 + x * y    |0x01             |Compression algorithm              |
|0x16 + x * y    |y - 1            |cbs = Compressed Block Size        |
|0x15 + (x+1) * y|sum(cbs)         |Concatenated compressed blocks     |
|File size - 0x10|0x10 (first half)|SHA256 header XOR SHA256 compressed|

`y = ceil(log2(bs)/8) + 1`

**Note:** The compressed block isn't allowed to be larger than the decompressed data - please use compression algorithm 0x00 (None) in that case or cbs might overflow!


## Type 2:
| Offset         |Size             |Description                        |
|----------------|-----------------|-----------------------------------|
|0x0C            |0x04             |Amount of Blocks                   |
|0x10            |0x01             |s = Size of size parameters        |
|0x11 + x * y    |0x01             |Compression algorithm              |
|0x12 + x * y    |s                |bs = Decompressed Block Size       |
|0x12 + x * y + s|s                |cbs = Compressed Block Size        |
|0x12 + (x+1) * y|sum(cbs)         |Concatenated compressed blocks     |
|File size - 0x10|0x10 (first half)|SHA256 header XOR SHA256 compressed|

`y = 2 * s + 1`

## Compression algorithms:
|Value|Algorithm|Recommended Parameters       |
|-----|---------|-----------------------------|
|0x00 |None     |None - Just use memcpy       |
|0x01 |Zstandard|CompressionLevel = 19        |
|0x02 |lzma     |Dic=1536, WordS=273, cLevel=9|

# nsZip

Work in progress file format for compressed Nintendo Switch games and a tool to compress/decompress them.

# NSZ file format:
## Header:
| Offset|Size |Description           |
|-------|-----|----------------------|
|0x00   |0x05 |Magic ("nsZip")       |
|0x05   |0x01 |Version (for now 0x00)|
|0x06   |0x01 |nsZip Type            |

## Type 0:
| Offset|Size            |Description          |
|-------|----------------|---------------------|
|0x07   |0x01            |Compression algorithm|
|0x08   |File size - 0x08|Full compressed file |

## Type 1:
| Offset         |Size    |Description                   |
|----------------|--------|------------------------------|
|0x07            |0x05    |bs = Decompressed Block Size  |
|0x0C + x * y    |0x01    |Compression algorithm         |
|0x0D + x * y    |y - 1   |cbs = Compressed Block Size   |
|0x0C + (x+1) * y|sum(cbs)|Concatenated compressed blocks|

$y = \left\lceil\dfrac{\log_2(bs)}{8}\right\rceil+1$

## Type 2:
| Offset         |Size    |Description                   |
|----------------|--------|------------------------------|
|0x07            |0x01    |s = Size of size parameters   |
|0x08 + x * y    |0x01    |Compression algorithm         |
|0x09 + x * y    |s       |bs = Decompressed Block Size  |
|0x09 + x * y + s|s       |cbs = Compressed Block Size   |
|0x0C + (x+1) * y|sum(cbs)|Concatenated compressed blocks|

$y = 2 * s + 1$

## Compression algorithms:
|Value|Algorithm|Recommended Parameters       |
|-----|---------|-----------------------------|
|0x00 |None     |bs = Decompressed Block Size |
|0x01 |Zstandard|CompressionLevel = 19        |
|0x02 |lzma     |Dic=1536, WordS=273, cLevel=9|

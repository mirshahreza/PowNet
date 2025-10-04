# StreamExtensions

Helpers for working with `Stream` objects: reading, writing, copying with progress, hashing, and safe disposal patterns.

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| ReadAllBytesAsync(this Stream s) | Read entire stream to byte[] |
| CopyToAsync(this Stream src, Stream dest, int bufferSize, IProgress<long>? progress) | Copy with progress updates |
| ComputeHashAsync(this Stream s, HashAlgorithm algo) | Return hex/base64 hash |
| ToMemoryStreamAsync(this Stream s) | Materialize to rewound MemoryStream |
| WriteStringAsync(this Stream s, string text, Encoding? enc=null) | Convenience writer |
| ReadStringAsync(this Stream s, Encoding? enc=null) | Convenience reader |

(Validate actual implementation.)

---
## Example
```csharp
await using var fs = File.OpenRead(path);
byte[] data = await fs.ReadAllBytesAsync();
string sha256 = await fs.ComputeHashAsync(SHA256.Create());
```

---
## Guidance
- Rewind stream (`s.Position = 0`) before hashing if stream previously read.
- Use pooled buffers (ArrayPool) for large / frequent copies (optimization idea).

---
## Limitations
- Hash helpers may allocate intermediate arrays—optimize for very large streams.

# IOExtensions

File and directory helper extensions providing safer read/write operations, path normalization, and convenience wrappers for common IO tasks.

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| ReadAllTextSafe(path) | Read file returning empty string on not found |
| TryReadBytes(path, out byte[] data) | Safe byte load with boolean success |
| WriteAllTextAtomic(path, content) | Write via temp file + atomic replace |
| EnsureDirectory(path) | Create directory if missing |
| CopyFileSafe(src, dest, overwrite) | Guarded copy with directory creation |
| DeleteIfExists(path) | Remove file ignoring not-found |

(Verify available methods in code.)

---
## Examples
```csharp
IOExtensions.EnsureDirectory(dataPath);
IOExtensions.WriteAllTextAtomic(Path.Combine(dataPath, "config.json"), json);
```

---
## Guidance
- Atomic writes reduce risk of partially written config files.
- Always validate untrusted file names with SecurityExtensions before writing.

---
## Limitations
- Does not abstract across remote storage providers (extend for S3/Azure Blob if needed).

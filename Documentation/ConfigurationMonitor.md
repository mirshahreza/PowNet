# ConfigurationMonitor

Runtime watcher that detects configuration changes and notifies subscribers so dependent components can react (reload caches, reconfigure services, etc.).

---
## Features
- Periodic polling or file system watcher (depending on implementation)
- Debounce logic to avoid duplicate notifications
- Subscription model (`OnChanged` events or delegates)
- Optional automatic re-bind into strongly typed config objects

---
## Usage
```csharp
ConfigurationMonitor.Start();
ConfigurationMonitor.Changed += (_, args) =>
{
    Console.WriteLine("Configuration changed: " + string.Join(',', args.ChangedKeys));
};
```

---
## Guidance
- Avoid heavy work directly in change handlers; queue tasks instead.
- When using file watching ensure container / cloud volume triggers events reliably (may need polling fallback).

---
## Limitations
- Rapid successive changes may coalesce into a single event.
- Does not (by default) validate new config values—invoke `ValidateConfiguration` manually.

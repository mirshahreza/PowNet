# ConfigurationExtensions
Binding ? Migration ? Template/Diff ???? ????????.

## ????
```csharp
var cfg = "Sample".BindConfiguration(new SampleConf());
var diff = ConfigurationExtensions.CompareConfigurations(oldCfg, newCfg);
```

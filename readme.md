# Decompiling
Decompiling AWSVPNClient.Core.dll with ILSpy
https://github.com/icsharpcode/AvaloniaILSpy

# Run while developing

```
dotnet run
```

# Creating finaly binary

```
dotnet publish -r linux-x64 -p:PublishSingleFile=true --self-contained false
```
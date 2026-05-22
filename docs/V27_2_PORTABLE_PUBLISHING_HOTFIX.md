# V27.2 - Portable publishing hotfix

This hotfix adds explicit scripts for portable distribution.

## Why `obj/Release/net8.0-windows` fails

`obj/` is an intermediate build directory. It is not a deployment directory. Executables found there are framework-dependent and can show:

```text
You need to install .NET Desktop Runtime to run this application.
```

Use `dotnet publish`, not `dotnet build`, for distribution.

## Recommended USB distribution mode

Run:

```powershell
.\publish-portable-folder-win-x64.ps1
```

Then copy the whole folder:

```text
artifacts\portable\SqlQueryGenerator-win-x64-folder
```

to the USB key and run:

```text
SqlQueryGenerator.App.exe
```

This mode embeds the .NET Desktop Runtime and does not require installation on the target PC.

## Single-file mode

Run:

```powershell
.\publish-portable-singlefile-win-x64.ps1
```

It creates:

```text
artifacts\portable\SqlQueryGenerator-win-x64-singlefile\SqlQueryGenerator.App.exe
```

This is easier to copy, but WPF single-file apps can extract native/runtime assets on first launch. If a locked-down workstation blocks extraction, use the folder mode.

## ZIP packaging

Run:

```powershell
.\package-portable-win-x64.ps1
```

It creates:

```text
artifacts\portable\SqlQueryGenerator-win-x64-portable.zip
```

Unzip it on the target machine or USB key.

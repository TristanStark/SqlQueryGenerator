# Publishing SQL Query Generator

The WPF app is configured with:

```xml
<OutputType>WinExe</OutputType>
<UseWPF>true</UseWPF>
<UseAppHost>true</UseAppHost>
```

## Framework-dependent executable

```powershell
.\publish-win-x64.ps1
```

Output:

```text
artifacts\publish\win-x64-framework-dependent\SqlQueryGenerator.App.exe
```

Requires Microsoft .NET 8 Desktop Runtime.

## Self-contained single-file executable

```powershell
.\publish-win-x64-self-contained.ps1
```

Output:

```text
artifacts\publish\win-x64-self-contained\SqlQueryGenerator.App.exe
```

This version embeds the .NET runtime. It is much larger, but easier to distribute.

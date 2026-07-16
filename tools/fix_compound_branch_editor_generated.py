from pathlib import Path

root = Path(__file__).resolve().parents[1]
path = root / "src" / "SqlQueryGenerator.App" / "ViewModels" / "MainViewModel.cs"
content = path.read_text(encoding="utf-8")
old = '''        AddCompoundBranchItem(_compoundQueryTemplate, "1", 0, null);
        for (int index = 0; index < _compoundQueryTemplate.SetOperations.Count; index++)
        {
            SetOperationDefinition operation = _compoundQueryTemplate.SetOperations[index];
            AddCompoundBranchItem(operation.Query, (index + 2).ToString(), 0, operation);
        }
'''
new = '''        AddCompoundBranchItem(_compoundQueryTemplate, "1", 0, null);
'''
if old in content:
    path.write_text(content.replace(old, new, 1), encoding="utf-8", newline="\n")
    print("Removed duplicate top-level compound branch enumeration.")
elif new in content:
    print("Duplicate compound branch enumeration is already fixed.")
else:
    raise RuntimeError("Compound branch enumeration block was not found.")

from pathlib import Path
import runpy

root = Path(__file__).resolve().parents[1]
marker = root / "src" / "SqlQueryGenerator.App" / "ViewModels" / "CompoundQueryBranchItemViewModel.cs"

if marker.exists():
    print("Compound branch editor patch is already applied.")
else:
    runpy.run_path(str(Path(__file__).resolve().parent / "apply_compound_branch_editor_patch.py"), run_name="__main__")

# V23 - TreeView binding fix

Rebuilt from v22.

Fixes WPF binding errors caused by generic TreeViewItem styles binding `IsExpanded` on leaf items:

- `ColumnItemViewModel`
- `RelationshipItemViewModel`

The fix adds a neutral `IsExpanded` property to leaf ViewModels. Visual behavior is unchanged; the property only prevents WPF from logging `BindingExpression path error: IsExpanded property not found`.

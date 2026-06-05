using SqlQueryGenerator.App.ViewModels;
using SqlQueryGenerator.Core.Heuristics;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace SqlQueryGenerator.App;

/// <summary>
/// Modal review window shown before importing DDL when likely backup tables are detected.
/// </summary>
public partial class SchemaImportReviewWindow : Window, INotifyPropertyChanged
{
    public SchemaImportReviewWindow(IEnumerable<BackupTableCandidate> candidates)
    {
        InitializeComponent();
        Candidates = new ObservableCollection<BackupTableCandidateSelectionItem>(
            candidates.Select(candidate => new BackupTableCandidateSelectionItem(candidate)));

        foreach (BackupTableCandidateSelectionItem candidate in Candidates)
        {
            candidate.PropertyChanged += Candidate_PropertyChanged;
        }

        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<BackupTableCandidateSelectionItem> Candidates { get; }

    public string SelectionSummary
    {
        get
        {
            int selectedCount = Candidates.Count(candidate => candidate.IsExcluded);
            return $"{Candidates.Count} candidat(s) détecté(s) · {selectedCount} exclu(s) pour l'import";
        }
    }

    public IReadOnlyList<string> ExcludedTableNames => Candidates
        .Where(candidate => candidate.IsExcluded)
        .Select(candidate => candidate.TableName)
        .ToArray();

    private void Candidate_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BackupTableCandidateSelectionItem.IsExcluded))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionSummary)));
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (BackupTableCandidateSelectionItem candidate in Candidates)
        {
            candidate.IsExcluded = true;
        }
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (BackupTableCandidateSelectionItem candidate in Candidates)
        {
            candidate.IsExcluded = false;
        }
    }

    private void ImportSelected_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void KeepAll_Click(object sender, RoutedEventArgs e)
    {
        ClearAll_Click(sender, e);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

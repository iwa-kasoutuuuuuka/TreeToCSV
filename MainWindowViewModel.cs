using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace TreeToCSV;

/// <summary>
/// メイン画面のViewModelです。
/// </summary>
public class MainWindowViewModel : INotifyPropertyChanged
{
    private string _selectedPath = string.Empty;
    private bool _quoteFields = true; // デフォルトでダブルクォーテーションで囲む
    private ObservableCollection<TreeNodeViewModel> _treeItems = new();

    public string SelectedPath
    {
        get => _selectedPath;
        set => SetProperty(ref _selectedPath, value);
    }

    public bool QuoteFields
    {
        get => _quoteFields;
        set => SetProperty(ref _quoteFields, value);
    }

    private bool _foldersOnly = false;
    public bool FoldersOnly
    {
        get => _foldersOnly;
        set => SetProperty(ref _foldersOnly, value);
    }

    public ObservableCollection<TreeNodeViewModel> TreeItems
    {
        get => _treeItems;
        set => SetProperty(ref _treeItems, value);
    }

    public ICommand BrowseCommand { get; }
    public ICommand ExportCsvCommand { get; }

    public MainWindowViewModel()
    {
        BrowseCommand = new RelayCommand(OnBrowse);
        ExportCsvCommand = new RelayCommand(OnExportCsv, CanExportCsv);
    }

    /// <summary>
    /// 対象フォルダの参照ボタンが押された時の処理。
    /// </summary>
    private void OnBrowse()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "対象フォルダを選択してください",
            InitialDirectory = string.IsNullOrEmpty(SelectedPath) ? "" : SelectedPath
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedPath = dialog.FolderName;
            ScanFolder(SelectedPath);
        }
    }

    /// <summary>
    /// フォルダ階層をスキャンしてツリー構造を構築します。
    /// </summary>
    internal void ScanFolder(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            MessageBox.Show("指定されたフォルダが存在しません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        TreeItems.Clear();
        ((RelayCommand)ExportCsvCommand).RaiseCanExecuteChanged();

        try
        {
            var rootDirInfo = new DirectoryInfo(rootPath);
            var rootNode = new TreeNodeViewModel
            {
                Name = rootDirInfo.Name,
                FullPath = Path.GetFullPath(rootDirInfo.FullName),
                IsDirectory = true,
                IsChecked = true,
                Parent = null
            };

            var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                rootNode.FullPath
            };

            // サブフォルダとファイルを再帰的に読み込む
            PopulateChildren(rootNode, visitedPaths, 1);

            TreeItems.Add(rootNode);
            ((RelayCommand)ExportCsvCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"フォルダのスキャン中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 指定されたノード配下の子要素（フォルダ・ファイル）を追加します。
    /// </summary>
    private void PopulateChildren(TreeNodeViewModel parentNode, HashSet<string> visitedPaths, int depth)
    {
        // スタックオーバーフローを防ぐための最大深度の制限
        if (depth > 100) return;

        try
        {
            var dirInfo = new DirectoryInfo(parentNode.FullPath);

            // サブフォルダの追加
            foreach (var subDir in dirInfo.GetDirectories())
            {
                string fullPath = Path.GetFullPath(subDir.FullName);

                // 循環参照を検知してスキップ
                if (visitedPaths.Contains(fullPath))
                {
                    continue;
                }

                var childNode = new TreeNodeViewModel
                {
                    Name = subDir.Name,
                    FullPath = fullPath,
                    IsDirectory = true,
                    IsChecked = true,
                    Parent = parentNode
                };
                parentNode.Children.Add(childNode);

                visitedPaths.Add(fullPath);
                PopulateChildren(childNode, visitedPaths, depth + 1); // 再帰スキャン
                visitedPaths.Remove(fullPath);
            }

            // ファイルの追加
            foreach (var file in dirInfo.GetFiles())
            {
                var childNode = new TreeNodeViewModel
                {
                    Name = file.Name,
                    FullPath = Path.GetFullPath(file.FullName),
                    IsDirectory = false,
                    IsChecked = true,
                    Parent = parentNode
                };
                parentNode.Children.Add(childNode);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // アクセス権限がないフォルダはスキップします
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error populating children: {ex.Message}");
        }
    }

    private bool CanExportCsv()
    {
        return TreeItems.Count > 0;
    }

    /// <summary>
    /// CSV出力ボタンが押された時の処理。
    /// </summary>
    private void OnExportCsv()
    {
        var dialog = new SaveFileDialog
        {
            Title = "CSVファイルの保存先を指定してください",
            Filter = "CSVファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*",
            DefaultExt = "csv",
            FileName = "FolderStructure.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ExportToCsvFile(dialog.FileName);
                MessageBox.Show("CSV出力が完了しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (IOException ioEx)
            {
                MessageBox.Show($"CSVファイルに書き込めません。ファイルが他のプログラム（Excelなど）で開かれている可能性があります。\n\n詳細: {ioEx.Message}", "書き込みエラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CSV出力中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// ツリーデータをCSVファイルに出力します。
    /// </summary>
    internal void ExportToCsvFile(string filePath)
    {
        // .NET Core / .NET 5+ で Shift_JIS を使用可能にするためのプロバイダは App.xaml.cs で登録済み
        var sjis = Encoding.GetEncoding("shift_jis");

        using (var writer = new StreamWriter(filePath, false, sjis))
        {
            if (TreeItems.Count > 0)
            {
                var rootNode = TreeItems[0];
                // ルートノードの配下からCSV出力を開始する（ルートノード直下の要素が深さ1となる）
                foreach (var child in rootNode.Children)
                {
                    WriteNodeToCsv(child, 1, writer);
                }
            }
        }
    }

    /// <summary>
    /// ノードをCSVに1行出力し、子要素を再帰的に走査します。
    /// </summary>
    private void WriteNodeToCsv(TreeNodeViewModel node, int depth, StreamWriter writer)
    {
        // チェックが外れているノードとその配下は出力しない
        if (node.IsChecked == false) return;

        // フォルダ名のみ出力（ファイル名除外）設定の場合、ファイル（IsDirectory == false）はスキップする
        if (!(FoldersOnly && !node.IsDirectory))
        {
            // ノード自体を出力
            var fields = new string[20];
            for (int i = 0; i < 20; i++)
            {
                fields[i] = string.Empty;
            }

            // 階層に応じて列（インデックス 0〜19）を設定
            int colIndex = Math.Min(depth, 20) - 1;
            
            // CSV Formula Injection (数式インジェクション) 対策を適用
            string name = EscapeFormulaInjection(node.Name);

            // RFC 4180に基づくダブルクォーテーションのエスケープ処理
            bool hasQuotes = name.Contains("\"");
            bool hasComma = name.Contains(",");
            bool hasNewLine = name.Contains("\r") || name.Contains("\n");

            if (hasQuotes)
            {
                name = name.Replace("\"", "\"\"");
            }

            // QuoteFieldsが有効、または特殊文字が含まれている場合はダブルクォーテーションで囲む
            if (QuoteFields || hasQuotes || hasComma || hasNewLine)
            {
                name = $"\"{name}\"";
            }

            fields[colIndex] = name;

            // カンマ区切りで書き出し
            writer.WriteLine(string.Join(",", fields));
        }

        // 子要素を再帰的に走査
        foreach (var child in node.Children)
        {
            WriteNodeToCsv(child, depth + 1, writer);
        }
    }

    /// <summary>
    /// CSV Formula Injection (数式インジェクション) 脆弱性を対策するため、
    /// 先頭が数式トリガー文字 (=, +, -, @) で始まる場合は、先頭にシングルクォーテーションを付与してテキスト化します。
    /// </summary>
    private string EscapeFormulaInjection(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        char first = text[0];
        if (first == '=' || first == '+' || first == '-' || first == '@')
        {
            return "'" + text;
        }
        return text;
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        ((RelayCommand)ExportCsvCommand).RaiseCanExecuteChanged();
        return true;
    }

    #endregion
}

/// <summary>
/// 簡易的なICommand実装用のRelayCommandクラスです。
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute == null || _canExecute();
    }

    public void Execute(object? parameter)
    {
        _execute();
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

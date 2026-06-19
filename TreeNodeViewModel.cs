using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TreeToCSV;

/// <summary>
/// ツリービューの各要素（フォルダまたはファイル）を表すViewModelです。
/// </summary>
public class TreeNodeViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _fullPath = string.Empty;
    private bool _isDirectory;
    private bool? _isChecked = true;
    private TreeNodeViewModel? _parent;
    private ObservableCollection<TreeNodeViewModel> _children = new();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string FullPath
    {
        get => _fullPath;
        set => SetProperty(ref _fullPath, value);
    }

    public bool IsDirectory
    {
        get => _isDirectory;
        set => SetProperty(ref _isDirectory, value);
    }

    public bool? IsChecked
    {
        get => _isChecked;
        set => SetIsChecked(value, true, true);
    }

    public TreeNodeViewModel? Parent
    {
        get => _parent;
        set => _parent = value;
    }

    public ObservableCollection<TreeNodeViewModel> Children
    {
        get => _children;
        set => SetProperty(ref _children, value);
    }

    /// <summary>
    /// チェック状態を更新し、必要に応じて親子に連動させます。
    /// </summary>
    private void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
    {
        if (_isChecked == value) return;

        _isChecked = value;
        OnPropertyChanged(nameof(IsChecked));

        // 子要素への伝播（親のチェック状態が変わった場合、子もすべてそれに合わせる）
        if (updateChildren && _isChecked.HasValue)
        {
            foreach (var child in Children)
            {
                child.SetIsChecked(_isChecked, true, false);
            }
        }

        // 親要素への伝播（子要素のチェック状態が変わった場合、親のチェック状態を再計算する）
        if (updateParent && Parent != null)
        {
            Parent.VerifyCheckState();
        }
    }

    /// <summary>
    /// 子要素の状態を検査し、自身のチェック状態を再計算します。
    /// </summary>
    public void VerifyCheckState()
    {
        bool? state = null;

        for (int i = 0; i < Children.Count; i++)
        {
            bool? current = Children[i].IsChecked;
            if (i == 0)
            {
                state = current;
            }
            else if (state != current)
            {
                state = null;
                break;
            }
        }

        SetIsChecked(state, false, true);
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
        return true;
    }

    #endregion
}

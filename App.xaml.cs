using System.Configuration;
using System.Data;
using System.Text;
using System.Windows;

namespace TreeToCSV;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // .NET で Shift_JIS (CP932) を使用可能にするためのエンコーディングプロバイダ登録
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}


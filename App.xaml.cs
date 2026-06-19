using System;
using System.IO;
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
        // 未処理例外ハンドラの登録
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        base.OnStartup(e);
        // .NET で Shift_JIS (CP932) を使用可能にするためのエンコーディングプロバイダ登録
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        e.Handled = true;
        Shutdown();
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex);
        }
    }

    private void LogException(Exception ex)
    {
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
            string message = $"[{DateTime.Now}] Unhandled Exception:\n{ex.ToString()}\n\n";
            File.AppendAllText(logPath, message);
            MessageBox.Show($"アプリケーションで予期しないエラーが発生しました。\n詳細を {logPath} に出力しました。\n\nエラー内容: {ex.Message}", "致命的なエラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            try
            {
                MessageBox.Show($"致命的なエラーが発生しました:\n{ex.Message}\n{ex.StackTrace}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // ignored
            }
        }
    }
}


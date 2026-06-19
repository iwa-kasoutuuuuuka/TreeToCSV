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

        // --test 引数が指定されている場合はセルフテストを実行して終了
        if (e.Args.Length > 0 && e.Args[0] == "--test")
        {
            RunSelfTest();
            Shutdown();
        }
    }

    private void RunSelfTest()
    {
        string testResultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_result.txt");
        var log = new StringBuilder();
        log.AppendLine("=== TreeToCSV 動作検証 & 脆弱性確認 セルフテスト ===");
        log.AppendLine($"テスト実行日時: {DateTime.Now}");

        string testBaseDir = Path.Combine(Path.GetTempPath(), "TreeToCSV_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testBaseDir);

        try
        {
            // 1. テスト用のディレクトリとファイルの作成
            log.AppendLine("[INFO] テストフォルダ階層を作成中...");
            
            string normalDir = Path.Combine(testBaseDir, "NormalDir");
            Directory.CreateDirectory(normalDir);
            
            string formulaDir = Path.Combine(testBaseDir, "=FormulaDir");
            Directory.CreateDirectory(formulaDir);

            string commaDir = Path.Combine(testBaseDir, "Comma,Dir");
            Directory.CreateDirectory(commaDir);

            File.WriteAllText(Path.Combine(normalDir, "NormalFile.txt"), "dummy content");
            File.WriteAllText(Path.Combine(normalDir, "+FormulaFile.txt"), "dummy content");

            // 2. 循環参照（ジャンクション）の作成 (StackOverflow検知用)
            string loopJunction = Path.Combine(testBaseDir, "LoopJunction");
            CreateJunction(testBaseDir, loopJunction);
            log.AppendLine("[INFO] 循環ジャンクション (LoopJunction -> 親フォルダ) を作成しました。");

            // 3. スキャン実行（無限再帰・StackOverflow回避の検証）
            var vm = new MainWindowViewModel();
            log.AppendLine("[INFO] フォルダをスキャン中 (循環リンクおよび深度の検証)...");
            vm.ScanFolder(testBaseDir);
            log.AppendLine("[SUCCESS] スキャン成功 (無限ループ・フリーズせずに正常終了)。");

            // 4. CSVインジェクションおよびエスケープの検証
            string csvPath1 = Path.Combine(testBaseDir, "normal_output.csv");
            vm.QuoteFields = false; // 自動囲みをテストするため一旦 false
            vm.FoldersOnly = false;
            log.AppendLine("[INFO] CSVファイルを出力中 (全要素)...");
            vm.ExportToCsvFile(csvPath1);

            var sjis = Encoding.GetEncoding("shift_jis");
            string csvContent1 = File.ReadAllText(csvPath1, sjis);
            log.AppendLine($"[INFO] 生成されたCSVの内容:\n{csvContent1}");

            // インジェクションエスケープ検証
            if (!csvContent1.Contains("'=FormulaDir"))
                throw new Exception("フォルダ名の数式インジェクション対策（先頭のシングルクォーテーション付与）が機能していません。");
            if (!csvContent1.Contains("'+FormulaFile.txt"))
                throw new Exception("ファイル名の数式インジェクション対策（先頭のシングルクォーテーション付与）が機能していません。");

            // カンマエスケープ検証
            if (!csvContent1.Contains("\"Comma,Dir\""))
                throw new Exception("カンマを含むフォルダ名が自動的にダブルクォーテーションで囲まれていません。");

            log.AppendLine("[SUCCESS] CSVインジェクション対策およびカンマエスケープ検証に合格。");

            // 5. フォルダ名のみ出力（ファイル名除外）オプションの検証
            string csvPath2 = Path.Combine(testBaseDir, "folders_only_output.csv");
            vm.FoldersOnly = true;
            log.AppendLine("[INFO] CSVファイルを出力中 (フォルダのみ出力設定)...");
            vm.ExportToCsvFile(csvPath2);

            string csvContent2 = File.ReadAllText(csvPath2, sjis);
            log.AppendLine($"[INFO] フォルダのみ出力CSVの内容:\n{csvContent2}");

            if (csvContent2.Contains("NormalFile.txt") || csvContent2.Contains("FormulaFile.txt") || csvContent2.Contains("Quote\"File.txt"))
                throw new Exception("FoldersOnlyオプションが有効であるにも関わらず、ファイル名が出力されています。");

            log.AppendLine("[SUCCESS] フォルダ名のみ出力（ファイル名除外）オプションの検証に合格。");
            log.AppendLine("\n【総合評価】 すべての動作検証、デバッグ、および脆弱性（循環リンク、CSVインジェクションバイパス）の確認テストに正常に合格しました。安全です。");
        }
        catch (Exception ex)
        {
            log.AppendLine($"\n【ERROR】 テスト中にエラーが発生しました:\n{ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            // クリーンアップ
            try
            {
                DeleteJunction(Path.Combine(testBaseDir, "LoopJunction"));
                Directory.Delete(testBaseDir, true);
            }
            catch { }

            File.WriteAllText(testResultPath, log.ToString(), Encoding.UTF8);
        }
    }

    private void CreateJunction(string targetPath, string junctionPath)
    {
        var proc = new System.Diagnostics.Process();
        proc.StartInfo.FileName = "cmd.exe";
        proc.StartInfo.Arguments = $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"";
        proc.StartInfo.CreateNoWindow = true;
        proc.StartInfo.UseShellExecute = false;
        proc.Start();
        proc.WaitForExit();
    }

    private void DeleteJunction(string junctionPath)
    {
        if (Directory.Exists(junctionPath))
        {
            Directory.Delete(junctionPath);
        }
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


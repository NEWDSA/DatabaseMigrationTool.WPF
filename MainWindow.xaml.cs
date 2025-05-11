using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DatabaseMigrationTool.Core.Models;
using DatabaseMigrationTool.Core.Services;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace DatabaseMigrationTool.WPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IDatabaseMigrationService _migrationService;
    private readonly IProgress<MigrationProgress> _progress;

    public MainWindow() : this(App.ServiceProvider.GetRequiredService<IDatabaseMigrationService>())
    {
    }

    public MainWindow(IDatabaseMigrationService migrationService)
    {
        InitializeComponent();
        _migrationService = migrationService;
        _progress = new Progress<MigrationProgress>(UpdateProgress);

        // 设置默认端口
        SourcePortTextBox.Text = "3306";
        TargetPortTextBox.Text = "1433";

        // 绑定事件处理程序
        TestSourceConnectionButton.Click += TestSourceConnection_Click;
        TestTargetConnectionButton.Click += TestTargetConnection_Click;
        StartMigrationButton.Click += StartMigration_Click;
        SourceDbTypeCombo.SelectionChanged += SourceDbType_SelectionChanged;
        TargetDbTypeCombo.SelectionChanged += TargetDbType_SelectionChanged;
        SourceAuthTypeCombo.SelectionChanged += SourceAuthTypeCombo_SelectionChanged;
        TargetAuthTypeCombo.SelectionChanged += TargetAuthTypeCombo_SelectionChanged;

        // 初始化显示
        UpdateSourceAuthPanel();
        UpdateTargetAuthPanel();
    }

    private void SourceDbType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SourceDbTypeCombo.SelectedItem is ComboBoxItem selectedItem)
        {
            SourcePortTextBox.Text = selectedItem.Content.ToString() == "MySQL" ? "3306" : "1433";
            // 联动目标类型
            if (selectedItem.Content.ToString() == "SQL Server")
            {
                if (TargetDbTypeCombo.SelectedIndex != 0)
                    TargetDbTypeCombo.SelectedIndex = 0;
            }
            else if (selectedItem.Content.ToString() == "MySQL")
            {
                if (TargetDbTypeCombo.SelectedIndex != 1)
                    TargetDbTypeCombo.SelectedIndex = 1;
            }
            // 验证方式联动
            if (selectedItem.Content.ToString() == "MySQL")
            {
                SourceAuthTypeCombo.SelectedIndex = 1; // 账号密码
                SourceAuthTypeCombo.IsEnabled = false;
            }
            else
            {
                SourceAuthTypeCombo.IsEnabled = true;
            }
        }
    }

    private void TargetDbType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TargetDbTypeCombo.SelectedItem is ComboBoxItem selectedItem)
        {
            TargetPortTextBox.Text = selectedItem.Content.ToString() == "MySQL" ? "3306" : "1433";
            // 联动源类型
            if (selectedItem.Content.ToString() == "SQL Server")
            {
                if (SourceDbTypeCombo.SelectedIndex != 0)
                    SourceDbTypeCombo.SelectedIndex = 0;
            }
            else if (selectedItem.Content.ToString() == "MySQL")
            {
                if (SourceDbTypeCombo.SelectedIndex != 1)
                    SourceDbTypeCombo.SelectedIndex = 1;
            }
            // 验证方式联动
            if (selectedItem.Content.ToString() == "MySQL")
            {
                TargetAuthTypeCombo.SelectedIndex = 1; // 账号密码
                TargetAuthTypeCombo.IsEnabled = false;
            }
            else
            {
                TargetAuthTypeCombo.IsEnabled = true;
            }
        }
    }

    private void SourceAuthTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSourceAuthPanel();
    }

    private void TargetAuthTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateTargetAuthPanel();
    }

    private void UpdateSourceAuthPanel()
    {
        if (SourceAuthTypeCombo.SelectedIndex == 0) // Windows身份验证
        {
            SourceSqlAuthPanel.Visibility = Visibility.Collapsed;
            SourceUserPassPanel.Visibility = Visibility.Collapsed;
            SourceDatabaseTextBox.Visibility = Visibility.Visible;
            SourceServerTextBox.Visibility = Visibility.Visible;
        }
        else // SQL Server身份验证
        {
            SourceSqlAuthPanel.Visibility = Visibility.Visible;
            SourceUserPassPanel.Visibility = Visibility.Visible;
            SourceDatabaseTextBox.Visibility = Visibility.Visible;
            SourceServerTextBox.Visibility = Visibility.Visible;
        }
    }

    private void UpdateTargetAuthPanel()
    {
        if (TargetAuthTypeCombo.SelectedIndex == 0)
        {
            TargetSqlAuthPanel.Visibility = Visibility.Collapsed;
            TargetUserPassPanel.Visibility = Visibility.Collapsed;
            TargetDatabaseTextBox.Visibility = Visibility.Visible;
            TargetServerTextBox.Visibility = Visibility.Visible;
        }
        else
        {
            TargetSqlAuthPanel.Visibility = Visibility.Visible;
            TargetUserPassPanel.Visibility = Visibility.Visible;
            TargetDatabaseTextBox.Visibility = Visibility.Visible;
            TargetServerTextBox.Visibility = Visibility.Visible;
        }
    }

    private async void TestSourceConnection_Click(object sender, RoutedEventArgs e)
    {
        // 必填项校验
        if (SourceDbTypeCombo.SelectedItem == null || string.IsNullOrWhiteSpace(SourceServerTextBox.Text) || string.IsNullOrWhiteSpace(SourceDatabaseTextBox.Text))
        {
            MessageBox.Show("请填写完整的数据库类型、服务器地址和数据库名！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            var config = GetSourceConnectionConfig();
            var result = await _migrationService.TestConnectionAsync(config);
            
            if (result)
            {
                AppendLog("源数据库连接测试成功！", Colors.Green);
            }
            else
            {
                AppendLog("源数据库连接测试失败！", Colors.Red);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"源数据库连接测试出错：{ex.Message}", Colors.Red);
        }
    }

    private async void TestTargetConnection_Click(object sender, RoutedEventArgs e)
    {
        // 必填项校验
        if (TargetDbTypeCombo.SelectedItem == null || string.IsNullOrWhiteSpace(TargetServerTextBox.Text) || string.IsNullOrWhiteSpace(TargetDatabaseTextBox.Text))
        {
            MessageBox.Show("请填写完整的数据库类型、服务器地址和数据库名！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            var config = GetTargetConnectionConfig();
            var result = await _migrationService.TestConnectionAsync(config);
            
            if (result)
            {
                AppendLog("目标数据库连接测试成功！", Colors.Green);
            }
            else
            {
                AppendLog("目标数据库连接测试失败！", Colors.Red);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"目标数据库连接测试出错：{ex.Message}", Colors.Red);
        }
    }

    private async void StartMigration_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = new MigrationConfig
            {
                SourceConfig = GetSourceConnectionConfig(),
                TargetConfig = GetTargetConnectionConfig(),
                IncludeData = IncludeDataCheck.IsChecked ?? true,
                IncludeIndexes = IncludeIndexesCheck.IsChecked ?? true,
                IncludeConstraints = IncludeConstraintsCheck.IsChecked ?? true,
                IncludeTriggers = IncludeTriggersCheck.IsChecked ?? true,
                IncludeStoredProcedures = IncludeStoredProceduresCheck.IsChecked ?? true,
                IncludeFunctions = IncludeFunctionsCheck.IsChecked ?? true,
                IncludeViews = IncludeViewsCheck.IsChecked ?? true
            };

            // 选择输出路径
            var dialog = new SaveFileDialog
            {
                Filter = "SQL Server 数据库文件|*.mdf|所有文件|*.*",
                Title = "选择输出文件位置"
            };

            if (dialog.ShowDialog() == true)
            {
                config.OutputPath = dialog.FileName;
                StartMigrationButton.IsEnabled = false;
                AppendLog("开始迁移...", Colors.Blue);

                var result = await _migrationService.MigrateAsync(config, _progress);

                if (result.Success)
                {
                    AppendLog("迁移完成！", Colors.Green);
                    foreach (var warning in result.Warnings)
                    {
                        AppendLog($"警告：{warning}", Colors.Orange);
                    }
                }
                else
                {
                    AppendLog("迁移失败！", Colors.Red);
                    foreach (var error in result.Errors)
                    {
                        AppendLog($"错误：{error}", Colors.Red);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"迁移过程出错：{ex.Message}", Colors.Red);
        }
        finally
        {
            StartMigrationButton.IsEnabled = true;
        }
    }

    private void UpdateProgress(MigrationProgress progress)
    {
        Dispatcher.Invoke(() =>
        {
            MigrationProgressBar.Value = progress.ProgressPercentage;
            AppendLog(progress.Message, Colors.Blue);
        });
    }

    private void AppendLog(string message, Color color)
    {
        var paragraph = new Paragraph();
        var run = new Run($"[{DateTime.Now:HH:mm:ss}] {message}")
        {
            Foreground = new SolidColorBrush(color)
        };
        paragraph.Inlines.Add(run);
        LogTextBox.Document.Blocks.Add(paragraph);
        LogTextBox.ScrollToEnd();
    }

    private int GetPort(string dbType, string portText, bool integratedSecurity)
    {
        if (integratedSecurity && dbType.ToLower() == "sqlserver")
            return 0; // 0 表示不指定端口，使用实例名
        if (int.TryParse(portText, out var port))
            return port;
        return dbType.ToLower() == "mysql" ? 3306 : 1433;
    }

    private DatabaseConnectionConfig GetSourceConnectionConfig()
    {
        var dbType = ((ComboBoxItem)SourceDbTypeCombo.SelectedItem).Content.ToString()?.ToLower() ?? "mysql";
        bool integratedSecurity = SourceAuthTypeCombo.SelectedIndex == 0;
        return new DatabaseConnectionConfig
        {
            DatabaseType = dbType,
            Server = SourceServerTextBox.Text,
            Port = integratedSecurity ? 0 : GetPort(dbType, SourcePortTextBox.Text, false),
            DatabaseName = SourceDatabaseTextBox.Text,
            Username = integratedSecurity ? string.Empty : SourceUsernameTextBox.Text,
            Password = integratedSecurity ? string.Empty : SourcePasswordBox.Password,
            IntegratedSecurity = integratedSecurity
        };
    }

    private DatabaseConnectionConfig GetTargetConnectionConfig()
    {
        var dbType = ((ComboBoxItem)TargetDbTypeCombo.SelectedItem).Content.ToString()?.ToLower() ?? "mysql";
        bool integratedSecurity = TargetAuthTypeCombo.SelectedIndex == 0;
        string dbName = string.IsNullOrWhiteSpace(TargetDatabaseTextBox.Text)
            ? SourceDatabaseTextBox.Text
            : TargetDatabaseTextBox.Text;
        return new DatabaseConnectionConfig
        {
            DatabaseType = dbType,
            Server = TargetServerTextBox.Text,
            Port = integratedSecurity ? 0 : GetPort(dbType, TargetPortTextBox.Text, false),
            DatabaseName = dbName,
            Username = integratedSecurity ? string.Empty : TargetUsernameTextBox.Text,
            Password = integratedSecurity ? string.Empty : TargetPasswordBox.Password,
            IntegratedSecurity = integratedSecurity
        };
    }
}
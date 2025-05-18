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
using System.Collections.Generic;

namespace DatabaseMigrationTool.WPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IDatabaseMigrationService _migrationService;
    private readonly IProgress<MigrationProgress> _progress;
    private readonly List<string> DbTypes = new List<string> { "MySQL", "SQL Server" };
    private bool _isDbTypeSyncing = false;
    private int _lastChangedCombo = 0; // 1=Source, 2=Target

    public MainWindow() : this(App.ServiceProvider.GetRequiredService<IDatabaseMigrationService>())
    {
    }

    public MainWindow(IDatabaseMigrationService migrationService)
    {
        InitializeComponent();
        _migrationService = migrationService;
        _progress = new Progress<MigrationProgress>(UpdateProgress);
        // 初始化数据库类型下拉框
        InitDbTypeCombos();
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

    private void InitDbTypeCombos()
    {
        SourceDbTypeCombo.Items.Clear();
        TargetDbTypeCombo.Items.Clear();
        foreach (var type in DbTypes)
        {
            SourceDbTypeCombo.Items.Add(new ComboBoxItem { Content = type });
            TargetDbTypeCombo.Items.Add(new ComboBoxItem { Content = type });
        }
        SourceDbTypeCombo.SelectedIndex = 0;
        TargetDbTypeCombo.SelectedIndex = 1;
        // 主动触发一次SelectionChanged，确保控件状态和数据同步
        SourceDbType_SelectionChanged(SourceDbTypeCombo, null);
        TargetDbType_SelectionChanged(TargetDbTypeCombo, null);
    }

    private void SourceDbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isDbTypeSyncing) return;
        _isDbTypeSyncing = true;

        if (SourceDbTypeCombo.SelectedItem is ComboBoxItem selectedItem)
        {
            string dbType = selectedItem.Content.ToString();
            // 每次切换类型都重置端口
            SourcePortTextBox.Text = dbType == "MySQL" ? "3306" : "1433";
            var oppositeType = dbType == "MySQL" ? "SQL Server" : "MySQL";
            for (int i = 0; i < TargetDbTypeCombo.Items.Count; i++)
            {
                var item = TargetDbTypeCombo.Items[i] as ComboBoxItem;
                if (item.Content.ToString() == oppositeType)
                {
                    TargetDbTypeCombo.SelectedIndex = i;
                    // 控制TargetAuthTypeCombo的显示
                    TargetAuthTypeCombo.Visibility = Visibility.Collapsed;
                    TargetAuthTypeCombo.Height = 0;
                    TargetAuthTypeCombo.MinHeight = 0;
                    TargetAuthTypeCombo.Margin = new Thickness(0);
                    break;
                }
            }
            // 控制控件显示，但不重置输入框内容
            if (dbType == "MySQL")
            {
                SourceAuthTypeCombo.Visibility = Visibility.Collapsed;
                SourceAuthTypeCombo.Height = 0;
                SourceAuthTypeCombo.MinHeight = 0;
                SourceAuthTypeCombo.Margin = new Thickness(0);
                SourcePortTextBox.Visibility = Visibility.Visible;
                SourceUsernameTextBox.Visibility = Visibility.Visible;
                SourcePasswordBox.Visibility = Visibility.Visible;
                SourceServerTextBox.Visibility = Visibility.Visible;
                SourceDatabaseTextBox.Visibility = Visibility.Visible;
            }
            else // SQL Server
            {
                SourceAuthTypeCombo.Visibility = Visibility.Visible;
                SourceAuthTypeCombo.Height = double.NaN;
                SourceAuthTypeCombo.MinHeight = 0;
                SourceAuthTypeCombo.Margin = new Thickness(0,8,0,8);
                UpdateSourceAuthPanel();
            }
        }
        _isDbTypeSyncing = false;
    }

    private void TargetDbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isDbTypeSyncing) return;
        _isDbTypeSyncing = true;

        if (TargetDbTypeCombo.SelectedItem is ComboBoxItem selectedItem)
        {
            string dbType = selectedItem.Content.ToString();
            // 每次切换类型都重置端口
            TargetPortTextBox.Text = dbType == "MySQL" ? "3306" : "1433";
            var oppositeType = dbType == "MySQL" ? "SQL Server" : "MySQL";
            for (int i = 0; i < SourceDbTypeCombo.Items.Count; i++)
            {
                var item = SourceDbTypeCombo.Items[i] as ComboBoxItem;
                if (item.Content.ToString() == oppositeType)
                {
                    SourceDbTypeCombo.SelectedIndex = i;
                    break;
                }
            }
            // 控制TargetAuthTypeCombo的显示/隐藏
            if (dbType == "MySQL")
            {
                TargetAuthTypeCombo.Visibility = Visibility.Collapsed;
                TargetAuthTypeCombo.Height = 0;
                TargetAuthTypeCombo.MinHeight = 0;
                TargetAuthTypeCombo.Margin = new Thickness(0);
                // MySQL不支持集成安全，强制设为False
                if (TargetAuthTypeCombo is ComboBox combo)
                {
                    combo.SelectedIndex = 1; // 1为SQL Server身份认证
                }
                // 用户名密码控件可见
                TargetUsernameTextBox.Visibility = Visibility.Visible;
                TargetPasswordBox.Visibility = Visibility.Visible;
            }
            else
            {
                TargetAuthTypeCombo.Visibility = Visibility.Visible;
                TargetAuthTypeCombo.Height = double.NaN;
                TargetAuthTypeCombo.MinHeight = 0;
                TargetAuthTypeCombo.Margin = new Thickness(0, 8, 0, 8);
            }
            UpdateTargetAuthPanel();
        }
        _isDbTypeSyncing = false;
    }

    private void SourceAuthTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 切换到Windows身份认证时自动清空用户名和密码
        if (SourceAuthTypeCombo.SelectedIndex == 0)
        {
            SourceUsernameTextBox.Text = string.Empty;
            SourcePasswordBox.Password = string.Empty;
        }
        UpdateSourceAuthPanel();
    }

    private void TargetAuthTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 切换到Windows身份认证时自动清空用户名和密码
        if (TargetAuthTypeCombo.SelectedIndex == 0)
        {
            TargetUsernameTextBox.Text = string.Empty;
            TargetPasswordBox.Password = string.Empty;
        }
        UpdateTargetAuthPanel();
    }

    private void UpdateSourceAuthPanel()
    {
        if (SourceDbTypeCombo.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content.ToString() == "MySQL")
        {
            SourceAuthTypeCombo.Visibility = Visibility.Collapsed;
            SourceAuthTypeCombo.Height = 0;
            SourceAuthTypeCombo.MinHeight = 0;
            SourceAuthTypeCombo.Margin = new Thickness(0);
            SourcePortTextBox.Visibility = Visibility.Visible;
            SourceUsernameTextBox.Visibility = Visibility.Visible;
            SourcePasswordBox.Visibility = Visibility.Visible;
            SourceServerTextBox.Visibility = Visibility.Visible;
            SourceDatabaseTextBox.Visibility = Visibility.Visible;
            return;
        }
        SourceAuthTypeCombo.Visibility = Visibility.Visible;
        SourceAuthTypeCombo.Height = double.NaN;
        SourceAuthTypeCombo.MinHeight = 0;
        SourceAuthTypeCombo.Margin = new Thickness(0,8,0,8);
        if (SourceAuthTypeCombo.SelectedIndex == 0) // Windows身份验证
        {
            SourcePortTextBox.Visibility = Visibility.Collapsed;
            SourceUsernameTextBox.Visibility = Visibility.Collapsed;
            SourcePasswordBox.Visibility = Visibility.Collapsed;
            SourceDatabaseTextBox.Visibility = Visibility.Visible;
            SourceServerTextBox.Visibility = Visibility.Visible;
        }
        else // SQL Server身份验证
        {
            SourcePortTextBox.Visibility = Visibility.Visible;
            SourceUsernameTextBox.Visibility = Visibility.Visible;
            SourcePasswordBox.Visibility = Visibility.Visible;
            SourceDatabaseTextBox.Visibility = Visibility.Visible;
            SourceServerTextBox.Visibility = Visibility.Visible;
        }
    }

    private void UpdateTargetAuthPanel()
    {
        if (TargetDbTypeCombo.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content.ToString() == "MySQL")
        {
            TargetPortTextBox.Visibility = Visibility.Visible;
            TargetUsernameTextBox.Visibility = Visibility.Visible;
            TargetPasswordBox.Visibility = Visibility.Visible;
            TargetServerTextBox.Visibility = Visibility.Visible;
            TargetDatabaseTextBox.Visibility = Visibility.Visible;
            return;
        }
        // SQL Server时，用户名和密码也应可见
        TargetPortTextBox.Visibility = Visibility.Visible;
        TargetUsernameTextBox.Visibility = Visibility.Visible;
        TargetPasswordBox.Visibility = Visibility.Visible;
        TargetServerTextBox.Visibility = Visibility.Visible;
        TargetDatabaseTextBox.Visibility = Visibility.Visible;
    }

    private async void TestSourceConnection_Click(object sender, RoutedEventArgs e)
    {
        FocusManager.SetFocusedElement(this, (IInputElement)this); // 让窗口获得焦点，确保输入框值刷新
        // 必填项校验
        if (SourceDbTypeCombo.SelectedItem == null || string.IsNullOrWhiteSpace(SourceServerTextBox.Text) || string.IsNullOrWhiteSpace(SourceDatabaseTextBox.Text))
        {
            MessageBox.Show("请填写完整的数据库类型、服务器地址和数据库名！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            var config = GetSourceConnectionConfig();
            // 弹窗显示连接信息
            MessageBox.Show($"类型: {config.DatabaseType}\n服务器: {config.Server}\n端口: {config.Port}\n库名: {config.DatabaseName}\n用户名: {config.Username}\n集成安全: {config.IntegratedSecurity}", "源数据库连接信息", MessageBoxButton.OK, MessageBoxImage.Information);
            var result = await _migrationService.TestConnectionAsync(config);
            AppendLog("TestConnectionAsync返回：" + result, Colors.Blue);
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
            // 弹窗显示连接信息
            MessageBox.Show($"类型: {config.DatabaseType}\n服务器: {config.Server}\n端口: {config.Port}\n库名: {config.DatabaseName}\n用户名: {config.Username}\n集成安全: {config.IntegratedSecurity}", "目标数据库连接信息", MessageBoxButton.OK, MessageBoxImage.Information);
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

    private async void ArrowMigrateButton_Click(object sender, RoutedEventArgs e)
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

            ArrowMigrateButton.IsEnabled = false;
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
        catch (Exception ex)
        {
            AppendLog($"迁移过程出错：{ex.Message}", Colors.Red);
        }
        finally
        {
            ArrowMigrateButton.IsEnabled = true;
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
        int port = 0;
        if (dbType == "mysql")
        {
            port = GetPort(dbType, SourcePortTextBox.Text, false);
            integratedSecurity = false;

        }
        // SQL Server类型时port始终为0，由后端拼接Server,Port
        return new DatabaseConnectionConfig
        {
            DatabaseType = dbType,
            Server = SourceServerTextBox.Text,
            Port = port,
            DatabaseName = SourceDatabaseTextBox.Text,
            Username = integratedSecurity ? string.Empty : SourceUsernameTextBox.Text,
            Password = integratedSecurity ? string.Empty : SourcePasswordBox.Password,
            IntegratedSecurity = integratedSecurity
        };
    }

    private DatabaseConnectionConfig GetTargetConnectionConfig()
    {
        var dbType = ((ComboBoxItem)TargetDbTypeCombo.SelectedItem).Content.ToString()?.ToLower() ?? "mysql";
        // MySQL类型时集成安全始终为false
        bool integratedSecurity = dbType == "mysql" ? false : TargetAuthTypeCombo.SelectedIndex == 0;
        int port = 0;
        if (dbType == "mysql")
        {
            port = GetPort(dbType, TargetPortTextBox.Text, false);
            integratedSecurity = false;
        }
        string dbName = string.IsNullOrWhiteSpace(TargetDatabaseTextBox.Text)
            ? SourceDatabaseTextBox.Text
            : TargetDatabaseTextBox.Text;
        return new DatabaseConnectionConfig
        {
            DatabaseType = dbType,
            Server = TargetServerTextBox.Text,
            Port = port,
            DatabaseName = dbName,
            Username = integratedSecurity ? string.Empty : TargetUsernameTextBox.Text,
            Password = integratedSecurity ? string.Empty : TargetPasswordBox.Password,
            IntegratedSecurity = integratedSecurity
        };
    }
}
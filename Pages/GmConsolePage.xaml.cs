using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AzerothCoreManager.Controls;
using AzerothCoreManager.Services;

namespace AzerothCoreManager.Pages;

public partial class GmConsolePage : Page
{
    private readonly SoapService _soap = new();
    private readonly ConfigService _config = new();
    private readonly List<string> _history = new();
    private readonly List<string> _favorites = new();
    private int _historyIndex = -1;
    private bool _connected;
    private string _soapHost = "127.0.0.1";
    private int _soapPort = 7878;
    private string _soapUser = "";
    private string _soapPass = "";

    private static readonly Dictionary<string, List<(string Cmd, string Desc)>> CommandCatalog = new()
    {
        ["Server"] = new()
        {
            ("server info", "Show server version and uptime"),
            ("server shutdown 60", "Shutdown server in 60 seconds"),
            ("server restart 60", "Restart server in 60 seconds"),
            ("server set motd Welcome!", "Set message of the day"),
        },
        ["Account"] = new()
        {
            ("account create user pass", "Create a new account"),
            ("account delete user", "Delete an account"),
            ("account set gmlevel user 3", "Set GM level (0-3)"),
            ("account set password user newpass newpass", "Change account password"),
            ("account onlinelist", "List online accounts"),
        },
        ["GM"] = new()
        {
            ("gm on", "Enable GM mode"),
            ("gm off", "Disable GM mode"),
            ("gm list", "List online GMs"),
            ("announce Hello everyone!", "Send server-wide announcement"),
            ("ban account user 7d reason", "Ban account for 7 days"),
            ("unban account user", "Unban an account"),
            ("kick user", "Kick a player from the server"),
            ("mute user 60", "Mute a player for 60 minutes"),
        },
        ["Player"] = new()
        {
            (".additem 12345", "Add item by ID"),
            (".tele stormwind", "Teleport to Stormwind"),
            (".modify speed 2", "Set movement speed multiplier"),
            (".revive", "Revive targeted player"),
            (".quest complete 12345", "Complete a quest"),
            (".respawn", "Respawn nearest creatures"),
        },
        ["Debug"] = new()
        {
            (".debug zonestats", "Show zone statistics"),
            (".debug play cinematic 1", "Play a cinematic"),
        },
    };

    public GmConsolePage()
    {
        InitializeComponent();
        PopulateCommandButtons();
    }

    private void PopulateCommandButtons()
    {
        CommandPanel.Children.Clear();
        foreach (var (category, commands) in CommandCatalog)
        {
            foreach (var (cmd, desc) in commands)
            {
                var btn = new GmCommandButton();
                btn.SetCommand(cmd, category, desc);
                btn.CommandClicked += OnCommandButtonClicked;
                btn.Margin = new Thickness(0, 0, 6, 6);
                CommandPanel.Children.Add(btn);
            }
        }
    }

    private void OnCommandButtonClicked(string command)
    {
        CommandInput.Text = command;
        _ = SendCommandAsync(command);
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = App.Settings.Load();
        var serverPath = settings.ServerPath;

        if (string.IsNullOrEmpty(serverPath) || !Directory.Exists(serverPath))
        {
            AppendOutput("Server path not configured. Please run Setup first.");
            return;
        }

        var confPath = Path.Combine(serverPath, "configs", "worldserver.conf");
        if (!File.Exists(confPath))
        {
            AppendOutput($"worldserver.conf not found at {confPath}");
            return;
        }

        var (enabled, ip, port) = _config.ReadSoapConfig(confPath);
        if (!enabled)
        {
            AppendOutput("SOAP is not enabled in worldserver.conf (SOAP.Enabled = 0)");
            return;
        }

        _soapHost = ip;
        _soapPort = port;
        _soapUser = settings.SoapUsername;
        _soapPass = settings.SoapPassword;

        if (string.IsNullOrEmpty(_soapUser) || string.IsNullOrEmpty(_soapPass))
        {
            AppendOutput("SOAP credentials not set. Create a SOAP admin account first.");
            return;
        }

        AppendOutput($"Connecting to {_soapHost}:{_soapPort} as {_soapUser}...");
        var ok = await _soap.TestConnectionAsync(_soapHost, _soapPort, _soapUser, _soapPass);

        if (ok)
        {
            _connected = true;
            SetConnectionStatus(true);
            SoapUserText.Text = $"Connected as: {_soapUser}";
            AppendOutput("Connected successfully.");
        }
        else
        {
            AppendOutput("Connection failed. Is the worldserver running?");
        }
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        _connected = false;
        SetConnectionStatus(false);
        SoapUserText.Text = "";
        AppendOutput("Disconnected.");
    }

    private void SetConnectionStatus(bool connected)
    {
        if (connected)
        {
            ConnectionDot.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x00));
            ConnectionStatus.Text = "Connected";
            ConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x00));
            ConnectButton.IsEnabled = false;
            DisconnectButton.IsEnabled = true;
        }
        else
        {
            ConnectionDot.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            ConnectionStatus.Text = "Not connected";
            ConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var cmd = CommandInput.Text.Trim();
        if (string.IsNullOrEmpty(cmd)) return;
        await SendCommandAsync(cmd);
    }

    private async Task SendCommandAsync(string command)
    {
        if (!_connected)
        {
            AppendOutput("Not connected. Click Connect first.");
            return;
        }

        AppendOutput($"> {command}");
        CommandInput.Clear();

        _history.Add(command);
        _historyIndex = _history.Count;

        var (success, response) = await _soap.SendCommandAsync(
            _soapHost, _soapPort, _soapUser, _soapPass, command);

        if (success)
            AppendOutput(response);
        else
            AppendOutput($"ERROR: {response}");
    }

    private void CommandInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendButton_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (_history.Count > 0 && _historyIndex > 0)
            {
                _historyIndex--;
                CommandInput.Text = _history[_historyIndex];
                CommandInput.CaretIndex = CommandInput.Text.Length;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (_historyIndex < _history.Count - 1)
            {
                _historyIndex++;
                CommandInput.Text = _history[_historyIndex];
            }
            else
            {
                _historyIndex = _history.Count;
                CommandInput.Text = "";
            }
            CommandInput.CaretIndex = CommandInput.Text.Length;
            e.Handled = true;
        }
    }

    private void FavButton_Click(object sender, RoutedEventArgs e)
    {
        var cmd = CommandInput.Text.Trim();
        if (string.IsNullOrEmpty(cmd)) return;

        if (!_favorites.Contains(cmd))
        {
            _favorites.Add(cmd);
            AppendOutput($"Added to favorites: {cmd}");
        }
        else
        {
            _favorites.Remove(cmd);
            AppendOutput($"Removed from favorites: {cmd}");
        }
    }

    private void ClearOutputButton_Click(object sender, RoutedEventArgs e)
    {
        OutputBox.Clear();
    }

    private void AppendOutput(string text)
    {
        Dispatcher.Invoke(() =>
        {
            OutputBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            OutputBox.ScrollToEnd();
        });
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        WikiLinkService.OpenHelp("gm_console");
    }
}

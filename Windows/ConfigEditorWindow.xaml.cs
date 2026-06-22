using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;
using AzerothCoreManager.Services;

namespace AzerothCoreManager.Windows;

public partial class ConfigEditorWindow : FluentWindow
{
    private readonly ConfigService _config = new();
    private string? _currentFilePath;

    public ConfigEditorWindow()
    {
        InitializeComponent();
        LoadFile("worldserver.conf");
    }

    private void LoadFile(string fileName)
    {
        var settings = App.Settings.Load();
        var serverPath = settings.ServerPath;
        if (string.IsNullOrEmpty(serverPath))
        {
            EditorBox.Text = "# Server path not configured. Please set it in Settings.";
            return;
        }

        var configsDir = Path.Combine(serverPath, "configs");
        _currentFilePath = Path.Combine(configsDir, fileName);

        if (!File.Exists(_currentFilePath))
        {
            EditorBox.Text = $"# File not found: {_currentFilePath}\n# Run Setup first to generate config files.";
            StatusText.Text = "File not found";
            return;
        }

        var lines = _config.ReadRawLines(_currentFilePath);
        EditorBox.Text = string.Join(Environment.NewLine, lines);
        StatusText.Text = $"Loaded: {fileName} ({lines.Length} lines)";
    }

    private void FileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileCombo.SelectedItem is ComboBoxItem item && item.Tag is string fileName)
            LoadFile(fileName);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (FileCombo.SelectedItem is ComboBoxItem item && item.Tag is string fileName)
            LoadFile(fileName);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFilePath == null || !File.Exists(_currentFilePath))
        {
            StatusText.Text = "No file loaded";
            return;
        }

        try
        {
            var newLines = EditorBox.Text.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
            var tmp = _currentFilePath + ".tmp";
            File.WriteAllLines(tmp, newLines);
            File.Move(tmp, _currentFilePath, overwrite: true);
            StatusText.Text = "Saved successfully.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Save failed: {ex.Message}";
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) DoSearch();
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => DoSearch();

    private void DoSearch()
    {
        var term = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(term)) return;

        var text = EditorBox.Text;
        var idx = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            EditorBox.Focus();
            EditorBox.Select(idx, term.Length);
            var lineIdx = text[..idx].Count(c => c == '\n');
            EditorBox.ScrollToLine(lineIdx);
            StatusText.Text = $"Found at line {lineIdx + 1}";
        }
        else
        {
            StatusText.Text = "Not found";
        }
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        WikiLinkService.OpenHelp("config_editor");
    }
}

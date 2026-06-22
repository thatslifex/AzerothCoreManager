using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AzerothCoreManager.Controls;

public partial class GmCommandButton : UserControl
{
    public event Action<string>? CommandClicked;

    private string _command = "";
    private string _category = "";

    public GmCommandButton()
    {
        InitializeComponent();
    }

    public void SetCommand(string command, string category, string? description = null)
    {
        _command = command;
        _category = category;
        CommandLabel.Text = command;
        CategoryTag.Text = $"[{category}]";

        if (description != null)
            CmdButton.ToolTip = description;

        CategoryTag.Foreground = category switch
        {
            "Server" => new SolidColorBrush(Color.FromRgb(0xFF, 0x88, 0x00)),
            "Account" => new SolidColorBrush(Color.FromRgb(0x00, 0xAA, 0xFF)),
            "GM" => new SolidColorBrush(Color.FromRgb(0xAA, 0x00, 0xFF)),
            "Player" => new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x66)),
            "Debug" => new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x00)),
            _ => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
        };
    }

    private void CmdButton_Click(object sender, RoutedEventArgs e)
    {
        CommandClicked?.Invoke(_command);
    }
}

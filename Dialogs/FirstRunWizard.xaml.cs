using System.Windows;
using Wpf.Ui.Controls;

namespace AzerothCoreManager.Dialogs;

public partial class FirstRunWizard : FluentWindow
{
    private int _currentStep = 0;
    private readonly string[] _stepTitles =
    [
        "Step 1/6: System Check",
        "Step 2/6: Installing Prerequisites",
        "Step 3/6: Source Code",
        "Step 4/6: Build",
        "Step 5/6: Deploy & Configure",
        "Step 6/6: Client Data"
    ];

    private readonly string[] _stepDescriptions =
    [
        "Checking your system for required tools (Git, CMake, Visual Studio, MySQL, OpenSSL, Boost, .NET, VC Redist, GitHub Desktop).",
        "Downloading and silently installing missing prerequisites. This may take several minutes.",
        "Cloning the AzerothCore source code from GitHub. This downloads ~500 MB of data.",
        "Configuring CMake and building the server binaries. This may take 10-30 minutes depending on your system.",
        "Deploying build output to the server directory, creating configuration files, and initializing the MySQL databases.",
        "Downloading client data files (dbc, maps, vmaps, mmaps). This downloads ~1.1 GB of data."
    ];

    public FirstRunWizard()
    {
        InitializeComponent();
        UpdateStep();
    }

    private void UpdateStep()
    {
        StepTitle.Text = _stepTitles[_currentStep];
        StepDescription.Text = _stepDescriptions[_currentStep];

        // Update step indicators
        var indicators = new char[6];
        for (int i = 0; i < 6; i++)
            indicators[i] = i == _currentStep ? '●' : '○';
        StepIndicator.Text = string.Join(" ", indicators);

        // Update button states
        BackButton.IsEnabled = _currentStep > 0;
        NextButton.Content = _currentStep == 5 ? "Finish" : "Next";
        SkipButton.Visibility = _currentStep == 5 ? Visibility.Collapsed : Visibility.Visible;

        // Update prerequisites list for step 0
        if (_currentStep == 0)
        {
            PrerequisitesList.ItemsSource = new[]
            {
                "Git for Windows",
                "CMake 3.27+",
                "Visual Studio 2022 (C++ workload)",
                "MySQL 8.0+",
                "OpenSSL 3.x (Win64)",
                "Boost 1.78+",
                ".NET SDK",
                "VC Redist (Visual C++ Redistributable)",
                "GitHub Desktop (optional)"
            };
        }
        else
        {
            PrerequisitesList.ItemsSource = null;
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 5)
        {
            // Finish — close wizard
            DialogResult = true;
            Close();
            return;
        }

        _currentStep++;
        UpdateStep();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 0)
        {
            _currentStep--;
            UpdateStep();
        }
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep < 5)
        {
            _currentStep++;
            UpdateStep();
        }
    }
}

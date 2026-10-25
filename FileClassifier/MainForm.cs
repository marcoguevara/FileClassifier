namespace FileClassifier;

internal sealed class MainForm : Form
{
    private readonly TextBox _folderPathTextBox = new();
    private readonly Button _browseButton = new();
    private readonly Button _previewButton = new();
    private readonly Button _organizeButton = new();
    private readonly Button _cancelButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _statusLabel = new();
    private readonly TextBox _logTextBox = new();
    private readonly TreeView _previewTreeView = new();

    private CancellationTokenSource? _cancellationTokenSource;
    private OrganizationPlan? _currentPlan;
    private bool _closeAfterCancellation;
    private bool _allowClose;

    public MainForm(string? initialFolder)
    {
        Text = "FileClassifier - File Organizer";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1000, 720);
        MinimumSize = new Size(800, 600);
        AutoScaleMode = AutoScaleMode.Font;
        AllowDrop = true;
        DragEnter += Folder_DragEnter;
        DragDrop += Folder_DragDrop;
        FormClosing += MainForm_FormClosing;

        BuildLayout();
        _folderPathTextBox.Text = initialFolder ?? string.Empty;
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 8
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 7; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            AutoSize = true,
            Text = "File Organizer",
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };
        var descriptionLabel = new Label
        {
            AutoSize = true,
            Text = "Organize files from this folder and its subfolders into year and month folders.",
            Margin = new Padding(0, 0, 0, 16)
        };
        var folderLabel = new Label
        {
            AutoSize = true,
            Text = "Folder to organize:",
            Margin = new Padding(0, 0, 0, 6)
        };

        var folderLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            AutoSize = true
        };
        folderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        _folderPathTextBox.Dock = DockStyle.Fill;
        _folderPathTextBox.Margin = new Padding(0, 0, 8, 0);
        _folderPathTextBox.AllowDrop = true;
        _folderPathTextBox.DragEnter += Folder_DragEnter;
        _folderPathTextBox.DragDrop += Folder_DragDrop;
        _folderPathTextBox.TextChanged += FolderPathTextBox_TextChanged;
        _browseButton.Text = "Browse...";
        _browseButton.Dock = DockStyle.Fill;
        _browseButton.Click += BrowseButton_Click;
        folderLayout.Controls.Add(_folderPathTextBox, 0, 0);
        folderLayout.Controls.Add(_browseButton, 1, 0);

        var actionsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };
        _previewButton.Text = "Preview";
        _previewButton.AutoSize = true;
        _previewButton.MinimumSize = new Size(100, 34);
        _previewButton.Click += PreviewButton_Click;
        _organizeButton.Text = "Organize Files";
        _organizeButton.AutoSize = true;
        _organizeButton.MinimumSize = new Size(150, 34);
        _organizeButton.Click += OrganizeButton_Click;
        _cancelButton.Text = "Cancel";
        _cancelButton.AutoSize = true;
        _cancelButton.MinimumSize = new Size(100, 34);
        _cancelButton.Enabled = false;
        _cancelButton.Click += CancelButton_Click;
        actionsLayout.Controls.Add(_previewButton);
        actionsLayout.Controls.Add(_organizeButton);
        actionsLayout.Controls.Add(_cancelButton);

        _statusLabel.AutoSize = true;
        _statusLabel.Text = "Select a folder to get started.";
        _statusLabel.Margin = new Padding(0, 2, 0, 6);

        _progressBar.Dock = DockStyle.Top;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Height = 22;
        _progressBar.Margin = new Padding(0, 0, 0, 12);

        var logGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Activity",
            Padding = new Padding(8)
        };
        _logTextBox.Dock = DockStyle.Fill;
        _logTextBox.Multiline = true;
        _logTextBox.ReadOnly = true;
        _logTextBox.ScrollBars = ScrollBars.Vertical;
        _logTextBox.WordWrap = false;
        _logTextBox.Font = new Font(FontFamily.GenericMonospace, 9);
        logGroup.Controls.Add(_logTextBox);

        var previewGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Planned folder tree",
            Padding = new Padding(8)
        };
        _previewTreeView.Dock = DockStyle.Fill;
        _previewTreeView.HideSelection = false;
        previewGroup.Controls.Add(_previewTreeView);

        var previewSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(900, 500),
            Orientation = Orientation.Vertical,
            SplitterDistance = 450,
            Panel1MinSize = 250,
            Panel2MinSize = 250
        };
        previewSplitContainer.Panel1.Controls.Add(logGroup);
        previewSplitContainer.Panel2.Controls.Add(previewGroup);

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(descriptionLabel, 0, 1);
        layout.Controls.Add(folderLabel, 0, 2);
        layout.Controls.Add(folderLayout, 0, 3);
        layout.Controls.Add(actionsLayout, 0, 4);
        layout.Controls.Add(_statusLabel, 0, 5);
        layout.Controls.Add(_progressBar, 0, 6);
        layout.Controls.Add(previewSplitContainer, 0, 7);
        Controls.Add(layout);

        AcceptButton = _organizeButton;
        CancelButton = _cancelButton;
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the folder you want to organize.",
            ShowNewFolderButton = false
        };

        if (Directory.Exists(_folderPathTextBox.Text))
        {
            dialog.SelectedPath = _folderPathTextBox.Text;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _folderPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private async void PreviewButton_Click(object? sender, EventArgs e)
    {
        if (!TryGetSourcePath(out var sourcePath))
        {
            return;
        }

        _logTextBox.Clear();
        _progressBar.Style = ProgressBarStyle.Marquee;
        _statusLabel.Text = "Building preview...";
        SetBusy(true);
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var plan = await FileOrganizer.BuildPlanAsync(
                sourcePath,
                new Progress<OrganizationProgress>(UpdateProgress),
                AppendLog,
                _cancellationTokenSource.Token);

            _currentPlan = plan.Cancelled ? null : plan;
            DisplayPlan(plan);
        }
        catch (OperationCanceledException)
        {
            _currentPlan = null;
            _statusLabel.Text = "Preview cancelled.";
            AppendLog(_statusLabel.Text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowOperationError(exception);
        }
        finally
        {
            FinishOperation();
        }
    }

    private async void OrganizeButton_Click(object? sender, EventArgs e)
    {
        if (!TryGetSourcePath(out var sourcePath))
        {
            return;
        }

        _logTextBox.Clear();
        _progressBar.Style = ProgressBarStyle.Marquee;
        _statusLabel.Text = "Preparing files...";
        SetBusy(true);
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var progress = new Progress<OrganizationProgress>(UpdateProgress);
            var plan = _currentPlan is not null &&
                       string.Equals(_currentPlan.RootPath, Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase)
                ? _currentPlan
                : await FileOrganizer.BuildPlanAsync(
                    sourcePath,
                    progress,
                    AppendLog,
                    _cancellationTokenSource.Token);

            _currentPlan = plan.Cancelled ? null : plan;
            DisplayPlan(plan);
            if (plan.Cancelled)
            {
                return;
            }

            var result = await FileOrganizer.ExecutePlanAsync(
                plan,
                progress,
                AppendLog,
                _cancellationTokenSource.Token);

            _currentPlan = null;
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = result.Total == 0
                ? 0
                : (int)((long)result.Completed * 100 / result.Total);
            _previewTreeView.Nodes.Clear();

            _statusLabel.Text = result.Cancelled
                ? $"Operation cancelled. Moved: {result.Moved}; errors: {result.Failed}; empty folders removed: {result.RemovedDirectories}."
                : $"Complete. Moved: {result.Moved}; errors: {result.Failed}; empty folders removed: {result.RemovedDirectories}.";
            AppendLog(_statusLabel.Text);
        }
        catch (OperationCanceledException)
        {
            _currentPlan = null;
            _progressBar.Style = ProgressBarStyle.Continuous;
            _statusLabel.Text = "Operation cancelled.";
            AppendLog(_statusLabel.Text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _currentPlan = null;
            ShowOperationError(exception);
        }
        finally
        {
            FinishOperation();
        }
    }

    private bool TryGetSourcePath(out string sourcePath)
    {
        sourcePath = _folderPathTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(sourcePath) && Directory.Exists(sourcePath))
        {
            return true;
        }

        MessageBox.Show(
            this,
            "Select a valid folder.",
            "Invalid Folder",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }

    private void DisplayPlan(OrganizationPlan plan)
    {
        _previewTreeView.BeginUpdate();
        _previewTreeView.Nodes.Clear();

        var rootNode = new TreeNode(plan.RootPath);
        var yearNodes = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
        var monthNodes = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in plan.Files.OrderBy(file => file.DestinationPath, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(plan.RootPath, file.DestinationPath);
            var pathParts = relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length < 3)
            {
                continue;
            }

            if (!yearNodes.TryGetValue(pathParts[0], out var yearNode))
            {
                yearNode = rootNode.Nodes.Add(pathParts[0], pathParts[0]);
                yearNodes.Add(pathParts[0], yearNode);
            }

            var monthKey = Path.Combine(pathParts[0], pathParts[1]);
            if (!monthNodes.TryGetValue(monthKey, out var monthNode))
            {
                monthNode = yearNode.Nodes.Add(pathParts[1], pathParts[1]);
                monthNodes.Add(monthKey, monthNode);
            }

            monthNode.Nodes.Add(pathParts[^1]);
        }

        if (plan.Files.Count == 0)
        {
            rootNode.Nodes.Add("(No files to organize)");
        }

        _previewTreeView.Nodes.Add(rootNode);
        rootNode.Expand();
        foreach (TreeNode yearNode in rootNode.Nodes)
        {
            yearNode.Expand();
            foreach (TreeNode monthNode in yearNode.Nodes)
            {
                monthNode.Expand();
            }
        }
        _previewTreeView.EndUpdate();

        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Value = plan.Total == 0
            ? 0
            : (int)((long)Math.Min(plan.Total, plan.Files.Count) * 100 / plan.Total);
        _statusLabel.Text = $"Preview ready. {plan.Files.Count} files planned; {plan.Failed} could not be planned.";
        if (plan.Cancelled)
        {
            _statusLabel.Text = "Preview cancelled.";
        }
    }

    private void ShowOperationError(Exception exception)
    {
        _progressBar.Style = ProgressBarStyle.Continuous;
        _statusLabel.Text = "Could not complete the operation.";
        AppendLog(exception.Message);
        MessageBox.Show(this, exception.Message, "Operation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void FinishOperation()
    {
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        SetBusy(false);

        if (_closeAfterCancellation)
        {
            _allowClose = true;
            Close();
        }
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        _cancelButton.Enabled = false;
        _statusLabel.Text = "Cancelling...";
    }

    private void UpdateProgress(OrganizationProgress progress)
    {
        _statusLabel.Text = progress.Total == 0
            ? progress.Stage
            : $"{progress.Stage} {progress.Completed}/{progress.Total}: {Path.GetFileName(progress.CurrentFile)}";

        if (progress.Total == 0)
        {
            _progressBar.Style = ProgressBarStyle.Marquee;
            return;
        }

        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Value = (int)((long)progress.Completed * 100 / progress.Total);
    }

    private void AppendLog(string message)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(AppendLog, message);
            return;
        }

        _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void SetBusy(bool isBusy)
    {
        _folderPathTextBox.Enabled = !isBusy;
        _browseButton.Enabled = !isBusy;
        _previewButton.Enabled = !isBusy;
        _organizeButton.Enabled = !isBusy;
        _cancelButton.Enabled = isBusy;
    }

    private void FolderPathTextBox_TextChanged(object? sender, EventArgs e)
    {
        _currentPlan = null;
        _previewTreeView.Nodes.Clear();
        _statusLabel.Text = "Select Preview to view the planned folder tree.";
        _progressBar.Value = 0;
        _progressBar.Style = ProgressBarStyle.Continuous;
    }

    private void Folder_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void Folder_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths &&
            Directory.Exists(paths[0]))
        {
            _folderPathTextBox.Text = paths[0];
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_cancellationTokenSource is not null && !_allowClose)
        {
            e.Cancel = true;
            _closeAfterCancellation = true;
            _cancellationTokenSource.Cancel();
            _statusLabel.Text = "Cancelling before closing...";
        }
    }
}

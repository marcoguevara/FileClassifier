namespace FileClassifier;

internal sealed class MainForm : Form
{
    private readonly TextBox _folderPathTextBox = new();
    private readonly Button _browseButton = new();
    private readonly Button _organizeButton = new();
    private readonly Button _cancelButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _statusLabel = new();
    private readonly TextBox _logTextBox = new();

    private CancellationTokenSource? _cancellationTokenSource;
    private bool _closeAfterCancellation;
    private bool _allowClose;

    public MainForm(string? initialFolder)
    {
        Text = "FileClassifier - Organizador de archivos";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 620);
        MinimumSize = new Size(700, 500);
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
            RowCount = 9
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 8; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            AutoSize = true,
            Text = "Organizador de archivos",
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };
        var descriptionLabel = new Label
        {
            AutoSize = true,
            Text = "Ordena archivos de la carpeta y sus subcarpetas en carpetas por año y mes.",
            Margin = new Padding(0, 0, 0, 16)
        };
        var folderLabel = new Label
        {
            AutoSize = true,
            Text = "Carpeta que se organizará:",
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
        _browseButton.Text = "Examinar...";
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
        _organizeButton.Text = "Organizar archivos";
        _organizeButton.AutoSize = true;
        _organizeButton.MinimumSize = new Size(150, 34);
        _organizeButton.Click += OrganizeButton_Click;
        _cancelButton.Text = "Cancelar";
        _cancelButton.AutoSize = true;
        _cancelButton.MinimumSize = new Size(100, 34);
        _cancelButton.Enabled = false;
        _cancelButton.Click += CancelButton_Click;
        actionsLayout.Controls.Add(_organizeButton);
        actionsLayout.Controls.Add(_cancelButton);

        _statusLabel.AutoSize = true;
        _statusLabel.Text = "Selecciona una carpeta para comenzar.";
        _statusLabel.Margin = new Padding(0, 2, 0, 6);

        _progressBar.Dock = DockStyle.Top;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Height = 22;
        _progressBar.Margin = new Padding(0, 0, 0, 12);

        var logLabel = new Label
        {
            AutoSize = true,
            Text = "Actividad:",
            Margin = new Padding(0, 0, 0, 6)
        };
        _logTextBox.Dock = DockStyle.Fill;
        _logTextBox.Multiline = true;
        _logTextBox.ReadOnly = true;
        _logTextBox.ScrollBars = ScrollBars.Vertical;
        _logTextBox.WordWrap = false;
        _logTextBox.Font = new Font(FontFamily.GenericMonospace, 9);

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(descriptionLabel, 0, 1);
        layout.Controls.Add(folderLabel, 0, 2);
        layout.Controls.Add(folderLayout, 0, 3);
        layout.Controls.Add(actionsLayout, 0, 4);
        layout.Controls.Add(_statusLabel, 0, 5);
        layout.Controls.Add(_progressBar, 0, 6);
        layout.Controls.Add(logLabel, 0, 7);
        layout.Controls.Add(_logTextBox, 0, 8);
        Controls.Add(layout);

        AcceptButton = _organizeButton;
        CancelButton = _cancelButton;
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona la carpeta que quieres organizar.",
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

    private async void OrganizeButton_Click(object? sender, EventArgs e)
    {
        var sourcePath = _folderPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(sourcePath) || !Directory.Exists(sourcePath))
        {
            MessageBox.Show(
                this,
                "Selecciona una carpeta válida.",
                "Carpeta no válida",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _logTextBox.Clear();
        _progressBar.Style = ProgressBarStyle.Marquee;
        _statusLabel.Text = "Buscando archivos...";
        SetBusy(true);
        _cancellationTokenSource = new CancellationTokenSource();

        var progress = new Progress<OrganizationProgress>(UpdateProgress);
        try
        {
            var result = await FileOrganizer.OrganizeFolderAsync(
                sourcePath,
                progress,
                AppendLog,
                _cancellationTokenSource.Token);

            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = result.Total == 0
                ? 0
                : (int)((long)result.Completed * 100 / result.Total);

            if (result.Cancelled)
            {
                _statusLabel.Text =
                    $"Proceso cancelado. Movidos: {result.Moved}; errores: {result.Failed}.";
            }
            else if (result.Total == 0)
            {
                _statusLabel.Text = "No hay archivos pendientes de organizar.";
            }
            else
            {
                _statusLabel.Text =
                    $"Terminado. Movidos: {result.Moved}; errores: {result.Failed}.";
            }

            AppendLog(_statusLabel.Text);
        }
        catch (OperationCanceledException)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
            _statusLabel.Text = "Proceso cancelado.";
            AppendLog(_statusLabel.Text);
        }
        catch (IOException exception)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
            _statusLabel.Text = "No se pudo completar la organización.";
            AppendLog(exception.Message);
            MessageBox.Show(this, exception.Message, "Error de acceso", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (UnauthorizedAccessException exception)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
            _statusLabel.Text = "No se pudo acceder a la carpeta.";
            AppendLog(exception.Message);
            MessageBox.Show(this, exception.Message, "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (ArgumentException exception)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
            _statusLabel.Text = "La ruta de la carpeta no es válida.";
            AppendLog(exception.Message);
            MessageBox.Show(this, exception.Message, "Ruta no válida", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            SetBusy(false);

            if (_closeAfterCancellation)
            {
                _allowClose = true;
                Close();
            }
        }
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        _cancelButton.Enabled = false;
        _statusLabel.Text = "Cancelando...";
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
        _organizeButton.Enabled = !isBusy;
        _cancelButton.Enabled = isBusy;
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
            _statusLabel.Text = "Cancelando antes de cerrar...";
        }
    }
}

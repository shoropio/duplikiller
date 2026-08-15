using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using DupliKiller.App.Helpers;
using DupliKiller.Core.Logging;
using DupliKiller.Core.Models;
using DupliKiller.Core.Services;
using DupliKiller.Core.Utils;
using Microsoft.Win32;

namespace DupliKiller.App.Views.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly IFileScanner _scanner;
    private readonly IFileActionService _fileActionService;
    private readonly IExportService _exportService;
    private CancellationTokenSource? _cts;
    private bool _suppressSelectionNotifications;
    private DateTime _scanStartTime;
    private int _lastProgressReported;
    private readonly TimeSpan _progressThrottle = TimeSpan.FromMilliseconds(150);
    private ScanMode _selectedScanMode = ScanMode.Deep;

    public MainViewModel()
    {
        var hashService = new HashService();
        var systemProtector = new SystemProtector();
        _scanner = new FileScanner(hashService, systemProtector);
        _fileActionService = new FileActionService();
        _exportService = new ExportService();

        var dispatch = System.Windows.Application.Current.Dispatcher;
        _scanner.StatusChanged += status => dispatch.InvokeAsync(() => StatusMessage = status);
        _scanner.FileDiscovered += (count, path) => dispatch.InvokeAsync(() =>
        {
            FilesScanned = count;
            CurrentFile = path;
            ProgressCurrent = count;
        });
        _scanner.FileHashed += item => dispatch.InvokeAsync(() => CurrentFile = item.Path);
        _scanner.ProgressUpdated += (current, total) => dispatch.InvokeAsync(() =>
        {
            var now = DateTime.UtcNow;
            if (now - _scanStartTime < _progressThrottle && current < _lastProgressReported + 10)
                return;

            ProgressCurrent = current;
            ProgressMax = total;
            _lastProgressReported = current;
            _scanStartTime = now;
            IsProgressIndeterminate = false;
        });
        _scanner.PhaseChanged += (phase, message) => dispatch.InvokeAsync(() =>
        {
            CurrentPhase = phase;
            CurrentPhaseMessage = message;
            IsProgressIndeterminate = phase == ScanPhase.Discovering || phase == ScanPhase.Grouping;
        });

        StartScanCommand = new RelayCommand(_ => { _ = StartScan(); }, _ => !IsScanning && ScanPaths.Count > 0);
        CancelScanCommand = new RelayCommand(_ => CancelScan(), _ => IsScanning);
        AddPathCommand = new RelayCommand(_ => AddPath());
        RemovePathCommand = new RelayCommand(p => RemovePath(p?.ToString()));
        ExportResultsCommand = new RelayCommand(p => ExportResults(p?.ToString() ?? "CSV"));

        SelectAllInGroupCommand = new RelayCommand(param => SelectAllInGroup(param?.ToString()));
        DeselectAllInGroupCommand = new RelayCommand(param => DeselectAllInGroup(param?.ToString()));
        SelectAllInZeroByteCommand = new RelayCommand(_ => SetSelection(ZeroByteFiles, true));
        DeselectAllInZeroByteCommand = new RelayCommand(_ => SetSelection(ZeroByteFiles, false));
        SelectAllInCorruptedCommand = new RelayCommand(_ => SetSelection(CorruptedFiles, true));
        DeselectAllInCorruptedCommand = new RelayCommand(_ => SetSelection(CorruptedFiles, false));
        DeleteToRecycleBinCommand = new RelayCommand(_ => ExecuteDeleteToRecycleBin(), _ => HasSelectedFiles);
        DeletePermanentlyCommand = new RelayCommand(_ => ExecuteDeletePermanently(), _ => HasSelectedFiles);
        CompressBackupCommand = new RelayCommand(_ => { _ = ExecuteCompressBackup(); }, _ => HasSelectedFiles);
        MoveFilesCommand = new RelayCommand(_ => ExecuteMoveFiles(), _ => HasSelectedFiles);
        OpenLogCommand = new RelayCommand(_ => OpenLog());
        OpenFileLocationCommand = new RelayCommand(p => OpenFileLocation(p?.ToString()));
        PreviewFileCommand = new RelayCommand(p => PreviewFile(p?.ToString()));
        DeleteSingleFileCommand = new RelayCommand(p => DeleteSingleFile(p?.ToString()));

        SelectAllGlobalCommand = new RelayCommand(_ => SelectAllGlobal());
        DeselectAllGlobalCommand = new RelayCommand(_ => DeselectAllGlobal());
        InvertSelectionCommand = new RelayCommand(_ => InvertSelection());
        SortResultsCommand = new RelayCommand(param => ApplySort(param?.ToString()));
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
        OpenAboutCommand = new RelayCommand(_ => OpenAbout());
        ToggleViewCommand = new RelayCommand(_ => IsCompactView = !IsCompactView);

        Results.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(HasNoResults));
        };
        ZeroByteFiles.CollectionChanged += (_, _) => NotifyProblemStatsChanged();
        CorruptedFiles.CollectionChanged += (_, _) => NotifyProblemStatsChanged();

        ScanPaths.CollectionChanged += (_, _) => SaveConfig();

        var config = AppConfig.Load();
        _isLightTheme = config.IsLightTheme;
        if (config.IsLightTheme)
            ThemeManager.SetTheme(true);
        foreach (var path in config.ScanPaths)
        {
            if (!string.IsNullOrEmpty(path))
                ScanPaths.Add(path);
        }
        foreach (var ext in config.ExcludeExtensions)
        {
            if (!string.IsNullOrEmpty(ext))
                ExcludeExtensions.Add(ext);
        }
        _selectedScanMode = config.SelectedScanMode == "Profundo" ? ScanMode.Deep : ScanMode.Standard;
        OnPropertyChanged(nameof(SelectedScanModeText));
    }

    private string _statusMessage = "Listo";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(ScanSummary)); }
    }

    private string _currentFile = "";
    public string CurrentFile
    {
        get => _currentFile;
        set { _currentFile = value; OnPropertyChanged(); }
    }

    private ScanPhase _currentPhase = ScanPhase.Completed;
    public ScanPhase CurrentPhase
    {
        get => _currentPhase;
        set { _currentPhase = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentPhaseText)); }
    }

    private string _currentPhaseMessage = "Listo";
    public string CurrentPhaseMessage
    {
        get => _currentPhaseMessage;
        set { _currentPhaseMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(ScanSummary)); }
    }

    public string CurrentPhaseText => CurrentPhase switch
    {
        ScanPhase.Discovering => "Descubriendo archivos",
        ScanPhase.Grouping => "Agrupando por tamaño",
        ScanPhase.QuickHashing => "Hash rápido",
        ScanPhase.FullHashing => "Hash completo",
        ScanPhase.Comparing => "Comparando binarios",
        ScanPhase.Completed => "Completado",
        ScanPhase.Cancelled => "Cancelado",
        ScanPhase.Failed => "Fallido",
        _ => "Listo"
    };

    private int _progressCurrent;
    public int ProgressCurrent
    {
        get => _progressCurrent;
        set
        {
            _progressCurrent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressPercentage));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(ScanPerformanceSummary));
        }
    }

    private int _progressMax;
    public int ProgressMax
    {
        get => _progressMax;
        set
        {
            _progressMax = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressPercentage));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(ScanPerformanceSummary));
        }
    }

    public int ProgressPercentage => ProgressMax > 0 ? (int)((double)ProgressCurrent / ProgressMax * 100) : 0;

    public string ProgressText => ProgressMax > 0
        ? $"{ProgressCurrent:N0} / {ProgressMax:N0}"
        : ProgressCurrent > 0 ? $"{ProgressCurrent:N0} archivos" : "";

    public string ScanSummary => IsScanning
        ? $"{CurrentPhaseText} • {CurrentPhaseMessage}"
        : $"{CurrentPhaseText} • {StatusMessage}";

    public string ScanPerformanceSummary
    {
        get
        {
            if (!IsScanning && ProgressCurrent <= 0) return "Esperando inicio";
            var elapsed = DateTime.UtcNow - _scanStartTime;
            var seconds = Math.Max(1, elapsed.TotalSeconds);
            var filesPerSecond = ProgressCurrent / seconds;
            return $"{filesPerSecond:F1} archivos/s • {elapsed:hh\\:mm\\:ss}";
        }
    }

    public string ResultsSummary => $"{DuplicateGroups} grupos • {DuplicatesFound} duplicados • {FriendlyReclaimableSpace}" +
        (HasProblemFiles ? $" • {ProblemFilesCount} problemas" : "");

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            _isScanning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScanSummary));
            OnPropertyChanged(nameof(ScanPerformanceSummary));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private bool _isProgressIndeterminate;
    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        set { _isProgressIndeterminate = value; OnPropertyChanged(); }
    }

    private long _filesScanned;
    public long FilesScanned
    {
        get => _filesScanned;
        set { _filesScanned = value; OnPropertyChanged(); }
    }

    private int _duplicateGroups;
    public int DuplicateGroups
    {
        get => _duplicateGroups;
        set { _duplicateGroups = value; OnPropertyChanged(); }
    }

    private int _duplicatesFound;
    public int DuplicatesFound
    {
        get => _duplicatesFound;
        set { _duplicatesFound = value; OnPropertyChanged(); }
    }

    private long _reclaimableSpace;
    public long ReclaimableSpace
    {
        get => _reclaimableSpace;
        set { _reclaimableSpace = value; OnPropertyChanged(); OnPropertyChanged(nameof(FriendlyReclaimableSpace)); }
    }

    public string FriendlyReclaimableSpace
    {
        get
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            double size = ReclaimableSpace;
            int index = 0;
            while (size >= 1024 && index < suffixes.Length - 1)
            {
                size /= 1024;
                index++;
            }
            return $"{size:F2} {suffixes[index]}";
        }
    }

    private int _selectedFilesCount;
    public int SelectedFilesCount
    {
        get => _selectedFilesCount;
        set { _selectedFilesCount = value; OnPropertyChanged(); }
    }

    private long _selectedReclaimableSpace;
    public long SelectedReclaimableSpace
    {
        get => _selectedReclaimableSpace;
        set { _selectedReclaimableSpace = value; OnPropertyChanged(); OnPropertyChanged(nameof(FriendlySelectedReclaimableSpace)); }
    }

    public string FriendlySelectedReclaimableSpace
    {
        get
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            double size = SelectedReclaimableSpace;
            int index = 0;
            while (size >= 1024 && index < suffixes.Length - 1)
            {
                size /= 1024;
                index++;
            }
            return $"{size:F2} {suffixes[index]}";
        }
    }

    private bool _hasSelectedFiles;
    public bool HasSelectedFiles
    {
        get => _hasSelectedFiles;
        set { _hasSelectedFiles = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    private bool _isLightTheme;
    public bool IsLightTheme
    {
        get => _isLightTheme;
        set { _isLightTheme = value; OnPropertyChanged(); }
    }

    private bool _isCompactView;
    public bool IsCompactView
    {
        get => _isCompactView;
        set
        {
            _isCompactView = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDetailedView));
            OnPropertyChanged(nameof(ViewModeText));
        }
    }

    public bool IsDetailedView => !_isCompactView;

    public string ViewModeText => _isCompactView ? "Vista compacta" : "Vista detallada";

    private string _selectedSortOption = "Tamaño (mayor)";
    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (_selectedSortOption != value)
            {
                _selectedSortOption = value;
                OnPropertyChanged();
                ApplySort(value);
            }
        }
    }

    public List<string> SortOptions { get; } = new()
    {
        "Tamaño (mayor)",
        "Tamaño (menor)",
        "Nombre (A-Z)",
        "Nombre (Z-A)",
        "Fecha (nuevo)",
        "Fecha (antiguo)"
    };

    public List<string> ScanModeOptions { get; } = new() { "Estándar", "Profundo" };

    public ScanMode SelectedScanMode
    {
        get => _selectedScanMode;
        set { _selectedScanMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedScanModeText)); }
    }

    public string SelectedScanModeText
    {
        get => SelectedScanMode == ScanMode.Deep ? "Profundo" : "Estándar";
        set
        {
            SelectedScanMode = value == "Profundo" ? ScanMode.Deep : ScanMode.Standard;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> ScanPaths { get; } = new();
    public ObservableCollection<string> ExcludeExtensions { get; } = new() { ".sys", ".dll" };
    public ObservableCollection<DuplicateGroup> Results { get; } = new();
    public ObservableCollection<FileItem> ZeroByteFiles { get; } = new();
    public ObservableCollection<FileItem> CorruptedFiles { get; } = new();

    public bool HasResults => Results.Count > 0 || ZeroByteFiles.Count > 0 || CorruptedFiles.Count > 0;
    public bool HasNoResults => Results.Count == 0 && ZeroByteFiles.Count == 0 && CorruptedFiles.Count == 0;

    public int ZeroByteCount => ZeroByteFiles.Count;
    public int CorruptedCount => CorruptedFiles.Count;
    public bool HasZeroByteFiles => ZeroByteFiles.Count > 0;
    public bool HasCorruptedFiles => CorruptedFiles.Count > 0;
    public bool HasProblemFiles => ZeroByteFiles.Count > 0 || CorruptedFiles.Count > 0;
    public int ProblemFilesCount => ZeroByteFiles.Count + CorruptedFiles.Count;

    public string ProblemSummary => $"Vacíos: {ZeroByteCount} · Dañados: {CorruptedCount} · Marcados para eliminar";

    public ICommand StartScanCommand { get; }
    public ICommand CancelScanCommand { get; }
    public ICommand AddPathCommand { get; }
    public ICommand RemovePathCommand { get; }
    public ICommand ExportResultsCommand { get; }
    public ICommand SelectAllInGroupCommand { get; }
    public ICommand DeselectAllInGroupCommand { get; }
    public ICommand SelectAllInZeroByteCommand { get; }
    public ICommand DeselectAllInZeroByteCommand { get; }
    public ICommand SelectAllInCorruptedCommand { get; }
    public ICommand DeselectAllInCorruptedCommand { get; }
    public ICommand DeleteToRecycleBinCommand { get; }
    public ICommand DeletePermanentlyCommand { get; }
    public ICommand CompressBackupCommand { get; }
    public ICommand MoveFilesCommand { get; }
    public ICommand OpenLogCommand { get; }
    public ICommand OpenFileLocationCommand { get; }
    public ICommand PreviewFileCommand { get; }
    public ICommand DeleteSingleFileCommand { get; }
    public ICommand SelectAllGlobalCommand { get; }
    public ICommand DeselectAllGlobalCommand { get; }
    public ICommand InvertSelectionCommand { get; }
    public ICommand SortResultsCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand OpenAboutCommand { get; }
    public ICommand ToggleViewCommand { get; }

    private void AddPath()
    {
        var folder = PickFolder("Seleccionar Carpeta");
        if (folder != null && !ScanPaths.Contains(folder))
        {
            ScanPaths.Add(folder);
        }
    }

    private void RemovePath(string? path)
    {
        if (!string.IsNullOrEmpty(path))
            ScanPaths.Remove(path);
    }

    private string? PickFolder(string title = "Seleccionar Carpeta")
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = title,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        var result = dialog.ShowDialog();
        if (result == System.Windows.Forms.DialogResult.OK || result == System.Windows.Forms.DialogResult.Yes)
        {
            return dialog.SelectedPath;
        }
        return null;
    }

    private async Task StartScan()
    {
        if (IsScanning || ScanPaths.Count == 0) return;

        IsScanning = true;
        CurrentPhase = ScanPhase.Discovering;
        CurrentPhaseMessage = "Iniciando escaneo";
        ProgressCurrent = 0;
        ProgressMax = 100;
        IsProgressIndeterminate = true;
        _scanStartTime = DateTime.UtcNow;
        _lastProgressReported = 0;
        UnsubscribeAll();
        Results.Clear();
        ZeroByteFiles.Clear();
        CorruptedFiles.Clear();
        _cts = new CancellationTokenSource();
        Logger.Info($"--- Scan started. Paths: {string.Join("; ", ScanPaths)} ---");
        SaveConfig();

        var progress = new Progress<ScanStatsUpdate>(update =>
        {
            FilesScanned = update.FilesScanned;
            DuplicatesFound = update.DuplicatesCount;
            DuplicateGroups = update.GroupsCount;
            ReclaimableSpace = update.ReclaimableSpace;
            OnPropertyChanged(nameof(ScanPerformanceSummary));
        });

        try
        {
            var result = await Task.Run(async () =>
                await _scanner.RunScanAsync(
                    ScanPaths.ToList(),
                    new List<string>(),
                    ExcludeExtensions.ToList(),
                    true,
                    true,
                    0,
                    0,
                    null,
                    null,
                    "SHA256",
                    _cts.Token,
                    progress,
                    SelectedScanMode));

            var groups = result.DuplicateGroups;
            foreach (var group in groups)
            {
                for (int i = 0; i < group.Files.Count; i++)
                    group.Files[i] = new FileItemViewModel(group.Files[i]);
                foreach (var file in group.Files)
                {
                    file.PropertyChanged += OnFileItemPropertyChanged;
                }
                Results.Add(group);
            }

            var zeroByteItems = result.ZeroByteFiles.Select(f => (FileItem)new FileItemViewModel(f)).ToList();
            var corruptedItems = result.CorruptedFiles.Select(f => (FileItem)new FileItemViewModel(f)).ToList();

            foreach (var file in zeroByteItems)
                file.PropertyChanged += OnFileItemPropertyChanged;
            foreach (var file in corruptedItems)
                file.PropertyChanged += OnFileItemPropertyChanged;

            _suppressSelectionNotifications = true;
            foreach (var group in groups)
            {
                for (int i = 1; i < group.Files.Count; i++)
                    group.Files[i].IsSelected = true;
            }
            foreach (var file in zeroByteItems)
                file.IsSelected = true;
            foreach (var file in corruptedItems)
                file.IsSelected = true;
            _suppressSelectionNotifications = false;

            foreach (var file in zeroByteItems)
                ZeroByteFiles.Add(file);
            foreach (var file in corruptedItems)
                CorruptedFiles.Add(file);

            RefreshSelectionStats();
            RenumberGroups();
            ThumbnailService.ClearCache();
            LoadThumbnails();

            StatusMessage = $"Escaneo completo. {groups.Count} grupos duplicados" +
                (result.HasProblemFiles
                    ? $", {result.ZeroByteFiles.Count} vacíos y {result.CorruptedFiles.Count} dañados marcados para eliminar."
                    : ".");
            CurrentPhase = ScanPhase.Completed;
            CurrentPhaseMessage = "Escaneo completado";
            Logger.Info($"Scan completed: {groups.Count} groups, {DuplicatesFound} duplicates, {FriendlyReclaimableSpace} reclaimable, " +
                $"{result.ZeroByteFiles.Count} zero-byte, {result.CorruptedFiles.Count} corrupted.");
        }
        catch (OperationCanceledException)
        {
            Logger.Info("Scan cancelled by user.");
            StatusMessage = "Escaneo cancelado.";
            CurrentPhase = ScanPhase.Cancelled;
            CurrentPhaseMessage = "Cancelado por el usuario";
        }
        catch (Exception ex)
        {
            Logger.Error($"Scan failed: {ex.Message}");
            StatusMessage = $"Error: {ex.Message}";
            CurrentPhase = ScanPhase.Failed;
            CurrentPhaseMessage = "Error en el escaneo";
        }
        finally
        {
            IsScanning = false;
            IsProgressIndeterminate = false;
            RefreshSelectionStats();
            OnPropertyChanged(nameof(ScanSummary));
            OnPropertyChanged(nameof(ScanPerformanceSummary));
        }
    }

    private void CancelScan()
    {
        _cts?.Cancel();
    }

    private void SelectAllInGroup(string? hash)
    {
        if (hash == null) return;
        var group = Results.FirstOrDefault(g => g.Hash == hash);
        if (group == null) return;
        SetSelection(group.Files, true);
    }

    private void DeselectAllInGroup(string? hash)
    {
        if (hash == null) return;
        var group = Results.FirstOrDefault(g => g.Hash == hash);
        if (group == null) return;
        SetSelection(group.Files, false);
    }

    private void SelectAllGlobal()
    {
        SetSelection(AllFiles(), true);
    }

    private void DeselectAllGlobal()
    {
        SetSelection(AllFiles(), false);
    }

    private void InvertSelection()
    {
        _suppressSelectionNotifications = true;
        foreach (var file in AllFiles())
            file.IsSelected = !file.IsSelected;
        _suppressSelectionNotifications = false;
        RefreshSelectionStats();
    }

    private IEnumerable<FileItem> AllFiles() =>
        Results.SelectMany(g => g.Files).Concat(ZeroByteFiles).Concat(CorruptedFiles);

    private void ApplySort(string? option)
    {
        if (string.IsNullOrEmpty(option) || Results.Count == 0) return;

        SelectedSortOption = option;

        var sortedItems = option switch
        {
            "Tamaño (mayor)" => Results.OrderByDescending(g => g.Files.Max(f => f.Size)).ToList(),
            "Tamaño (menor)" => Results.OrderBy(g => g.Files.Max(f => f.Size)).ToList(),
            "Nombre (A-Z)" => Results.OrderBy(g => g.Files.First().Name).ToList(),
            "Nombre (Z-A)" => Results.OrderByDescending(g => g.Files.First().Name).ToList(),
            "Fecha (nuevo)" => Results.OrderByDescending(g => g.Files.Max(f => f.LastWriteTime)).ToList(),
            "Fecha (antiguo)" => Results.OrderBy(g => g.Files.Max(f => f.LastWriteTime)).ToList(),
            _ => Results.ToList()
        };

        UnsubscribeAll();
        Results.Clear();
        foreach (var group in sortedItems)
        {
            foreach (var file in group.Files)
            {
                file.PropertyChanged += OnFileItemPropertyChanged;
            }
            Results.Add(group);
        }
        foreach (var file in ZeroByteFiles)
            file.PropertyChanged += OnFileItemPropertyChanged;
        foreach (var file in CorruptedFiles)
            file.PropertyChanged += OnFileItemPropertyChanged;
        RenumberGroups();
        RefreshSelectionStats();
    }

    private void ToggleTheme()
    {
        IsLightTheme = !IsLightTheme;
        ThemeManager.SetTheme(IsLightTheme);
        SaveConfig();
    }

    private void OpenAbout()
    {
        var about = new Views.AboutWindow
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        about.ShowDialog();
    }

    private void OnFileItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileItem.IsSelected) && sender is FileItem item && !_suppressSelectionNotifications)
        {
            if (item.IsSelected)
            {
                SelectedFilesCount++;
                SelectedReclaimableSpace += item.Size;
            }
            else
            {
                SelectedFilesCount--;
                SelectedReclaimableSpace -= item.Size;
            }
            HasSelectedFiles = SelectedFilesCount > 0;
        }
    }

    private void RefreshSelectionStats()
    {
        int count = 0;
        long space = 0;
        foreach (var file in AllFiles())
        {
            if (file.IsSelected)
            {
                count++;
                space += file.Size;
            }
        }
        SelectedFilesCount = count;
        SelectedReclaimableSpace = space;
        HasSelectedFiles = count > 0;
    }

    private void RenumberGroups()
    {
        int n = 1;
        foreach (var group in Results)
            group.GroupNumber = n++;
    }

    private void SetSelection(IEnumerable<FileItem> items, bool selected)
    {
        _suppressSelectionNotifications = true;
        foreach (var item in items)
            item.IsSelected = selected;
        _suppressSelectionNotifications = false;
        RefreshSelectionStats();
    }

    private void ExecuteDeleteToRecycleBin()
    {
        var selected = GetSelectedFiles();
        if (selected.Count == 0) return;

        var result = System.Windows.MessageBox.Show(
            $"¿Mover {selected.Count} archivo(s) seleccionado(s) a la Papelera de Reciclaje?",
            "Confirmar eliminación",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        int ok = 0, fail = 0;
        foreach (var file in selected)
        {
            if (_fileActionService.DeleteToRecycleBin(file.Path))
                ok++;
            else
                fail++;
        }

        CleanupAfterAction(selected);
        StatusMessage = $"{ok} archivos movidos a la Papelera de Reciclaje" + (fail > 0 ? $", {fail} fallaron" : "");
        Logger.Info($"Recycle Bin action: {ok} ok, {fail} failed");
    }

    private void ExecuteDeletePermanently()
    {
        var selected = GetSelectedFiles();
        if (selected.Count == 0) return;

        var result = System.Windows.MessageBox.Show(
            "¿Está seguro de eliminar permanentemente los archivos seleccionados?\n\nEsta acción no se puede deshacer.",
            "Confirmar eliminación permanente",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        int ok = 0, fail = 0;
        foreach (var file in selected)
        {
            if (_fileActionService.DeletePermanently(file.Path))
                ok++;
            else
                fail++;
        }

        CleanupAfterAction(selected);
        StatusMessage = $"{ok} archivos eliminados permanentemente" + (fail > 0 ? $", {fail} fallaron" : "");
        Logger.Info($"Permanent delete action: {ok} ok, {fail} failed");
    }

    private async Task ExecuteCompressBackup()
    {
        var selected = GetSelectedFiles();
        if (selected.Count == 0) return;

        var backupDir = PickFolder("Seleccionar carpeta de respaldo");
        if (backupDir == null) return;

        int ok = 0, fail = 0;
        foreach (var file in selected)
        {
            if (await _fileActionService.CompressAndBackupAsync(file.Path, backupDir))
                ok++;
            else
                fail++;
        }

        StatusMessage = $"{ok} archivos comprimidos y respaldados en {backupDir}" + (fail > 0 ? $", {fail} fallaron" : "");
        Logger.Info($"Compress & Backup action: {ok} ok, {fail} failed -> {backupDir}");
    }

    private void ExecuteMoveFiles()
    {
        var selected = GetSelectedFiles();
        if (selected.Count == 0) return;

        var destDir = PickFolder("Seleccionar carpeta de destino");
        if (destDir == null) return;

        int ok = 0, fail = 0;
        foreach (var file in selected)
        {
            if (_fileActionService.MoveFile(file.Path, destDir))
                ok++;
            else
                fail++;
        }

        CleanupAfterAction(selected);
        StatusMessage = $"{ok} archivos movidos a {destDir}" + (fail > 0 ? $", {fail} fallaron" : "");
        Logger.Info($"Move action: {ok} ok, {fail} failed -> {destDir}");
    }

    private void OpenLog()
    {
        var logPath = Logger.GetLogPath();
        if (System.IO.File.Exists(logPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = logPath,
                UseShellExecute = true
            });
        }
        else
        {
            StatusMessage = $"Archivo de log no encontrado: {logPath}";
        }
    }

    private void OpenFileLocation(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) return;
        var dir = System.IO.Path.GetDirectoryName(filePath);
        if (dir == null) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{filePath}\"",
            UseShellExecute = true
        });
    }

    private void PreviewFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true
        });
    }

    private void DeleteSingleFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        var item = AllFiles().FirstOrDefault(f => f.Path == filePath);
        if (item == null) return;
        if (_fileActionService.DeleteToRecycleBin(item.Path))
        {
            item.PropertyChanged -= OnFileItemPropertyChanged;
            var group = Results.FirstOrDefault(g => g.Files.Contains(item));
            if (group != null)
            {
                group.Files.Remove(item);
                if (group.Files.Count < 2) Results.Remove(group);
                else group.NotifyStatsChanged();
            }
            else if (ZeroByteFiles.Contains(item))
            {
                ZeroByteFiles.Remove(item);
            }
            else if (CorruptedFiles.Contains(item))
            {
                CorruptedFiles.Remove(item);
            }
            RenumberGroups();
            RefreshSelectionStats();
            RecalculateScanStats();
            StatusMessage = $"Eliminado: {item.Name}";
            Logger.Info($"Deleted single file: {item.Path}");
        }
    }

    private List<FileItem> GetSelectedFiles()
    {
        return AllFiles()
            .Where(f => f.IsSelected)
            .ToList();
    }

    private void CleanupAfterAction(List<FileItem> processedFiles)
    {
        var processedSet = new HashSet<string>(processedFiles.Select(f => f.Id));

        foreach (var group in Results.ToList())
        {
            for (int i = group.Files.Count - 1; i >= 0; i--)
            {
                if (processedSet.Contains(group.Files[i].Id))
                {
                    group.Files[i].PropertyChanged -= OnFileItemPropertyChanged;
                    group.Files.RemoveAt(i);
                }
            }

            if (group.Files.Count < 2)
            {
                Results.Remove(group);
            }
            else
            {
                group.NotifyStatsChanged();
            }
        }

        RemoveProcessed(ZeroByteFiles, processedSet);
        RemoveProcessed(CorruptedFiles, processedSet);

        RenumberGroups();
        RefreshSelectionStats();
        RecalculateScanStats();
    }

    private void RecalculateScanStats()
    {
        DuplicateGroups = Results.Count;
        DuplicatesFound = Results.Sum(g => g.Files.Count - 1);
        ReclaimableSpace = Results.Sum(g => g.ReclaimableSpace);
    }

    private void RemoveProcessed(ObservableCollection<FileItem> collection, HashSet<string> processedSet)
    {
        for (int i = collection.Count - 1; i >= 0; i--)
        {
            if (processedSet.Contains(collection[i].Id))
            {
                collection[i].PropertyChanged -= OnFileItemPropertyChanged;
                collection.RemoveAt(i);
            }
        }
    }

    private void LoadThumbnails()
    {
        var imageItems = AllFiles().Where(f => ThumbnailService.IsImagePath(f.Path)).ToList();
        if (imageItems.Count == 0) return;

        var dispatcher = System.Windows.Application.Current.Dispatcher;
        Task.Run(() =>
        {
            Parallel.ForEach(imageItems, item =>
            {
                if (item is not FileItemViewModel vm) return;
                var thumb = ThumbnailService.GetThumbnail(item.Path);
                if (thumb != null)
                    dispatcher.InvokeAsync(() => vm.Thumbnail = thumb);
            });
        });
    }

    private void NotifyProblemStatsChanged()
    {
        OnPropertyChanged(nameof(ZeroByteCount));
        OnPropertyChanged(nameof(CorruptedCount));
        OnPropertyChanged(nameof(HasZeroByteFiles));
        OnPropertyChanged(nameof(HasCorruptedFiles));
        OnPropertyChanged(nameof(HasProblemFiles));
        OnPropertyChanged(nameof(HasNoResults));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ProblemFilesCount));
        OnPropertyChanged(nameof(ProblemSummary));
        OnPropertyChanged(nameof(ResultsSummary));
    }

    private void UnsubscribeAll()
    {
        foreach (var group in Results)
        {
            foreach (var file in group.Files)
            {
                file.PropertyChanged -= OnFileItemPropertyChanged;
            }
        }
        foreach (var file in ZeroByteFiles)
            file.PropertyChanged -= OnFileItemPropertyChanged;
        foreach (var file in CorruptedFiles)
            file.PropertyChanged -= OnFileItemPropertyChanged;
    }

    private void SaveConfig()
    {
        var config = new AppConfig
        {
            ScanPaths = ScanPaths.ToList(),
            IsLightTheme = IsLightTheme,
            ExcludeExtensions = ExcludeExtensions.ToList(),
            SelectedScanMode = SelectedScanModeText
        };
        config.Save();
    }

    private void ExportResults(string format)
    {
        if (Results.Count == 0) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = format switch
            {
                "CSV" => "Archivos CSV (*.csv)|*.csv",
                "JSON" => "Archivos JSON (*.json)|*.json",
                "XML" => "Archivos XML (*.xml)|*.xml",
                _ => "Archivos TXT (*.txt)|*.txt"
            },
            FileName = $"ReporteDuplicados.{format.ToLower()}"
        };

        if (dialog.ShowDialog() == true)
        {
            switch (format)
            {
                case "CSV":
                    _exportService.ExportToCsv(dialog.FileName, Results.ToList());
                    break;
                case "JSON":
                    _exportService.ExportToJson(dialog.FileName, Results.ToList());
                    break;
                case "XML":
                    _exportService.ExportToXml(dialog.FileName, Results.ToList());
                    break;
                default:
                    _exportService.ExportToTxt(dialog.FileName, Results.ToList());
                    break;
            }

            StatusMessage = $"Reporte exportado a {dialog.FileName}";
            Logger.Info($"Exported to {dialog.FileName} (format: {format})");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
}

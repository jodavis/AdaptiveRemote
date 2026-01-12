using AdaptiveRemote.Models;

namespace AdaptiveRemote.Services.Lifecycle;

internal class LifecycleViewController : ILifecycleViewController
{
    private readonly List<Activity> _activities;
    private readonly object _lock = new();

    public LifecycleViewController(LifecycleView viewModel)
    {
        _activities =
        [
            new(this, string.Empty)
        ];

        ViewModel = viewModel;
        ViewModel.ShowErrorCommand = new ActionCommand(() => ViewModel.ShowError = true);
        ViewModel.CloseErrorCommand = new ActionCommand(() => ViewModel.ShowError = false);
    }

    public LifecycleView ViewModel { get; }

    public ILifecycleActivity StartTask(string description)
    {
        Activity activity = new(this, description);
        lock (_lock)
        {
            _activities.Insert(1, activity);
        }
        UpdateTaskName();
        return activity;
    }

    public void SetPhase(LifecyclePhase phase)
    {
        ViewModel.CurrentPhase = phase;
        _activities[0].Description = DescriptionFor(phase);
        UpdateTaskName();
    }

    public void SetFatalError(Exception error)
    {
        _activities[0].SetFatalError(error);
    }

    private static string DescriptionFor(LifecyclePhase phase)
        => phase switch
        {
            LifecyclePhase.Starting => Phrases.Startup_StartingApplication,
            LifecyclePhase.Building => Phrases.Startup_BuildingServiceGraph,
            LifecyclePhase.SettingUp => Phrases.Startup_StartingServices,
            LifecyclePhase.Stopping => Phrases.Cleanup_StoppingApplication,
            LifecyclePhase.CleaningUp => Phrases.Cleanup_CleaningUpApplication,
            _ => string.Empty,
        };

    private void UpdateTaskName()
    {
        lock (_lock)
        {
            Activity topActivity = _activities.Where(x => x.FatalError is not null).LastOrDefault()
                ?? _activities[_activities.Count - 1];

            if (topActivity.FatalError is not null)
            {
                string exceptionText = topActivity.FatalError.ToString();
                int newLineIndex = exceptionText.IndexOf('\n');
                if (newLineIndex != -1)
                {
                    ViewModel.FatalError = exceptionText.Substring(0, newLineIndex);
                    ViewModel.FatalErrorDetail = exceptionText.Substring(newLineIndex + 1);
                }
                else
                {
                    ViewModel.FatalError = exceptionText;
                    ViewModel.FatalErrorDetail = null;
                }
            }
            else if (!string.IsNullOrWhiteSpace(topActivity.Description))
            {
                ViewModel.TaskName = Phrases.Ellipsis(topActivity.Description);
            }
            else
            {
                ViewModel.TaskName = string.Empty;
            }

            if (ViewModel.FatalError is not null)
            {
                ViewModel.CurrentPhase = LifecyclePhase.FatalError;
                ViewModel.ShowBrowser = false;
                ViewModel.ShowButtonPanel = true;
            }
        }
    }

    private class Activity : ILifecycleActivity
    {
        private readonly LifecycleViewController _owner;
        private string _description;

        internal Activity(LifecycleViewController owner, string initialDescription)
        {
            _owner = owner;
            _description = initialDescription;
        }

        public void SetFatalError(Exception error)
        {
            if (FatalError is null)
            {
                Description = Phrases.ErrorWhile(Description);
                FatalError = error;
                _owner?.UpdateTaskName();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (FatalError is null)
                {
                    _description = value;
                    _owner?.UpdateTaskName();
                }
            }
        }

        public Exception? FatalError { get; private set; }

        public void Dispose()
        {
            if (FatalError is null)
            {
                _owner._activities.Remove(this);
            }
            _owner.UpdateTaskName();
        }
    }
}

using System.ComponentModel; using System.Runtime.CompilerServices;
namespace Michaelhouse.Mobile.ViewModels;
public abstract class BaseViewModel : INotifyPropertyChanged { bool busy; public bool IsBusy { get => busy; set { busy=value; PropertyChanged?.Invoke(this,new(nameof(IsBusy))); } } public event PropertyChangedEventHandler? PropertyChanged; }

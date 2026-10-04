using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NalogParserWPF.Helpers;

namespace NalogParserWPF.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        private RelayCommand? _closeCommand;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? CloseRequested;

        public RelayCommand CloseCommand => _closeCommand ??= new RelayCommand(_ => OnCloseRequested());

        protected void OnCloseRequested() => CloseRequested?.Invoke(this, EventArgs.Empty);

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}

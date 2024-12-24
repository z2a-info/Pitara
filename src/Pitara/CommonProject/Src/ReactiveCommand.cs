﻿using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;


namespace CommonProject.Src
{
    public interface IReactiveCommand
    {
        IObservable<object> CommandExecutedStream { get; }
    }

    public class ReactiveCommand<T1, T2> : ICommand, IReactiveCommand
    {
        private Func<T1, bool> canExecuteMethod;
        private Subject<object> commandExecutedSubject = new Subject<object>();

        public ReactiveCommand()
        {
            this.canExecuteMethod = (x) => { return true; };
        }

        public ReactiveCommand(Func<T1, bool> canExecuteMethod)
        {
            this.canExecuteMethod = canExecuteMethod;
        }

        public bool CanExecute(T1 parameter)
        {
            if (canExecuteMethod == null) return true;
            return canExecuteMethod(parameter);
        }

        public void Execute(T2 parameter)
        {
            commandExecutedSubject.OnNext(parameter);
        }

        public bool CanExecute(object parameter)
        {
            return CanExecute((T1)parameter);
        }

        public void Execute(object parameter)
        {
            Execute((T2)parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add
            {
                if (canExecuteMethod != null)
                {
                    CommandManager.RequerySuggested += value;
                }
            }

            remove
            {
                if (canExecuteMethod != null)
                {
                    CommandManager.RequerySuggested -= value;
                }
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public IObservable<object> CommandExecutedStream
        {
            get { return this.commandExecutedSubject.AsObservable(); }
        }
    }
}

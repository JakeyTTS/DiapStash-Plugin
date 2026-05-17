using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace DiapStash_Plugin
{
    // FIXED: Added a custom converter targeting parameter bounds mapping ranges to handle dynamic 3 vs 5 element scales rules
    public class TargetVariableToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string currentVariable && parameter is string limitType)
            {
                // Messy filters index ranges strictly capped to maximum value of 3 (hides level 4 and 5 controls)
                if (limitType == "Wetness" && currentVariable == "Messy")
                {
                    return Visibility.Collapsed;
                }
                return Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class TtsClause : INotifyPropertyChanged
    {
        private string _logicalOperator = "IF";
        private string _targetVariable = "Leak";
        private string _conditionType = "Equals";
        private string _targetValue = "YES";
        private string _outputMessage = "";

        public string LogicalOperator
        {
            get => _logicalOperator;
            set
            {
                if (_logicalOperator != value)
                {
                    _logicalOperator = value;
                    OnPropertyChanged();

                    if (_logicalOperator == "AND")
                    {
                        OutputMessage = string.Empty;
                    }
                }
            }
        }

        public string TargetVariable
        {
            get => _targetVariable;
            set
            {
                if (_targetVariable != value)
                {
                    _targetVariable = value;
                    OnPropertyChanged();

                    if (_targetVariable == "Leak" || _targetVariable == "Blowout")
                    {
                        ConditionType = "Equals";
                        TargetValue = "YES";
                    }
                    else if (_targetVariable == "Status")
                    {
                        ConditionType = "Equals";
                        TargetValue = "Active";
                    }
                    else
                    {
                        ConditionType = "Equals";
                        TargetValue = "1";
                    }
                    OnPropertyChanged(nameof(IsNumericControl));
                    OnPropertyChanged(nameof(IsTextControl));
                    OnPropertyChanged(nameof(IsStatusControl));
                }
            }
        }

        public string ConditionType
        {
            get => _conditionType;
            set { _conditionType = value; OnPropertyChanged(); }
        }

        public string TargetValue
        {
            get => _targetValue;
            set { _targetValue = value; OnPropertyChanged(); }
        }

        public string OutputMessage
        {
            get => _outputMessage;
            set { _outputMessage = value; OnPropertyChanged(); }
        }

        public bool IsNumericControl => TargetVariable == "Wetness" || TargetVariable == "Messy";
        public bool IsTextControl => TargetVariable == "Leak" || TargetVariable == "Blowout";
        public bool IsStatusControl => TargetVariable == "Status";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class TtsComplexRuleCard : INotifyPropertyChanged
    {
        private string _cardName = "unnamed_rule";
        public ObservableCollection<TtsClause> Clauses { get; set; } = new ObservableCollection<TtsClause>();

        public string CardName
        {
            get => _cardName;
            set { _cardName = value; OnPropertyChanged(); }
        }

        public TtsComplexRuleCard()
        {
            Clauses.Add(new TtsClause { LogicalOperator = "IF" });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
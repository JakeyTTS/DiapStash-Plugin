using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace DiapStash_Plugin
{
    public class TargetVariableToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string currentVariable && parameter is string limitType)
            {
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

        [System.Text.Json.Serialization.JsonIgnore]
        public TtsComplexRuleCard? ParentCard { get; set; }

        public string LogicalOperator
        {
            get => _logicalOperator;
            set
            {
                if (_logicalOperator != value)
                {
                    _logicalOperator = value;
                    OnPropertyChanged();

                    ParentCard?.NotifyAllClausesVisibilityChanged();
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

        public bool IsConditionalClause => LogicalOperator != "ELSE";
        public bool IsNumericControl => IsConditionalClause && (TargetVariable == "Wetness" || TargetVariable == "Messy");
        public bool IsTextControl => IsConditionalClause && (TargetVariable == "Leak" || TargetVariable == "Blowout");
        public bool IsStatusControl => IsConditionalClause && (TargetVariable == "Status");

        public bool IsLastInLogicalBlock
        {
            get
            {
                if (ParentCard == null) return true;
                int myIndex = ParentCard.Clauses.IndexOf(this);

                if (myIndex == ParentCard.Clauses.Count - 1) return true;

                string nextOp = ParentCard.Clauses[myIndex + 1].LogicalOperator;
                if (nextOp == "IF" || nextOp == "ELSE IF" || nextOp == "ELSE") return true;

                return false;
            }
        }

        public void RefreshVisibilityProperties()
        {
            OnPropertyChanged(nameof(IsConditionalClause));
            OnPropertyChanged(nameof(IsNumericControl));
            OnPropertyChanged(nameof(IsTextControl));
            OnPropertyChanged(nameof(IsStatusControl));
            OnPropertyChanged(nameof(IsLastInLogicalBlock)); 
        }

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
            var ifClause = new TtsClause { LogicalOperator = "IF", TargetVariable = "Leak", TargetValue = "YES", ParentCard = this };
            var elseClause = new TtsClause { LogicalOperator = "ELSE", OutputMessage = "Default state fallback.", ParentCard = this };

            Clauses.Add(ifClause);
            Clauses.Add(elseClause);

            Clauses.CollectionChanged += (s, e) =>
            {
                if (Clauses != null)
                {
                    foreach (var clause in Clauses) clause.ParentCard = this;
                    NotifyAllClausesVisibilityChanged();
                }
            };
        }

        public void NotifyAllClausesVisibilityChanged()
        {
            foreach (var clause in Clauses)
            {
                clause.RefreshVisibilityProperties();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
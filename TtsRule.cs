using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DiapStash_Plugin
{
    public class TtsClause : INotifyPropertyChanged
    {
        private string _logicalOperator = "IF"; // IF, AND, ELSE IF
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

                    // FIXED: Al pasar a ser un operador de continuidad 'AND', vaciamos el texto para evitar redundancias
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
        private string _cardName = "Unnamed Rule Block";
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
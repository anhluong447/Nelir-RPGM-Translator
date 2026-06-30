using System;
using System.Collections.Generic;

namespace Nelir.Models
{
    public class AiSuggestionResult
    {
        public List<AiSuggestionOption> Options { get; set; } = new();
        public string TerminologyNotes { get; set; } = string.Empty;
        public string ModelUsed { get; set; } = string.Empty;
    }

    public class AiSuggestionOption
    {
        public string TranslatedText { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
    }

    public class AiSuggestionException : Exception
    {
        public AiSuggestionException(string message) : base(message) { }
        public AiSuggestionException(string message, Exception? innerException) : base(message, innerException) { }
    }
}

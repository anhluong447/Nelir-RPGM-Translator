using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Nelir.Views
{
    internal class GlossaryCacheItem
    {
        public Regex? Regex { get; }

        public GlossaryCacheItem(Dictionary<string, string> dict)
        {
            var keys = dict.Keys
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .OrderByDescending(k => k.Length)
                .Select(Regex.Escape)
                .ToList();

            if (keys.Count > 0)
            {
                string pattern = "(" + string.Join("|", keys) + ")";
                try
                {
                    Regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                }
                catch
                {
                    Regex = null;
                }
            }
        }
    }

    public class GlossaryTextBlock : TextBlock
    {
        private static readonly ConditionalWeakTable<Dictionary<string, string>, GlossaryCacheItem> RegexCache = new();

        public static readonly DependencyProperty OriginalTextProperty =
            DependencyProperty.Register(nameof(OriginalText), typeof(string), typeof(GlossaryTextBlock),
                new PropertyMetadata(string.Empty, OnPropertiesChanged));

        public static readonly DependencyProperty GlossaryProperty =
            DependencyProperty.Register(nameof(Glossary), typeof(Dictionary<string, string>), typeof(GlossaryTextBlock),
                new PropertyMetadata(null, OnPropertiesChanged));

        public string OriginalText
        {
            get => (string)GetValue(OriginalTextProperty);
            set => SetValue(OriginalTextProperty, value);
        }

        public Dictionary<string, string>? Glossary
        {
            get => (Dictionary<string, string>?)GetValue(GlossaryProperty);
            set => SetValue(GlossaryProperty, value);
        }

        private static void OnPropertiesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GlossaryTextBlock textBlock)
            {
                textBlock.UpdateHighlighting();
            }
        }

        private void UpdateHighlighting()
        {
            Inlines.Clear();
            string text = OriginalText;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var glossary = Glossary;
            if (glossary == null || glossary.Count == 0)
            {
                Inlines.Add(new Run(text));
                return;
            }

            var cacheItem = RegexCache.GetValue(glossary, dict => new GlossaryCacheItem(dict));
            var regex = cacheItem.Regex;
            if (regex == null)
            {
                Inlines.Add(new Run(text));
                return;
            }

            var matches = regex.Matches(text);
            int lastIndex = 0;

            foreach (Match match in matches)
            {
                // Plain text segment before the matched term
                if (match.Index > lastIndex)
                {
                    Inlines.Add(new Run(text.Substring(lastIndex, match.Index - lastIndex)));
                }

                string matchedTerm = match.Value;
                string translation = string.Empty;

                // Lookup translation in dictionary
                if (glossary.TryGetValue(matchedTerm, out string? t))
                {
                    translation = t;
                }
                else
                {
                    // Fallback to case-insensitive lookup
                    var key = glossary.Keys.FirstOrDefault(k => k.Equals(matchedTerm, StringComparison.OrdinalIgnoreCase));
                    if (key != null)
                    {
                        translation = glossary[key];
                    }
                }

                // Create highlighted run for the matched term
                var run = new Run(matchedTerm)
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3CD")), // Warm sand yellow accent
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#856404")), // Dark brownish gold text
                    FontWeight = FontWeights.SemiBold
                };

                // Create a matching tooltip with formatting
                var toolTip = new ToolTip
                {
                    Content = $"Nghĩa: {translation}",
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3CD")),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#856404")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEBAA")),
                    Padding = new Thickness(8, 6, 8, 6),
                    FontSize = 12
                };
                ToolTipService.SetToolTip(run, toolTip);

                Inlines.Add(run);
                lastIndex = match.Index + match.Length;
            }

            // Remaining plain text segment
            if (lastIndex < text.Length)
            {
                Inlines.Add(new Run(text.Substring(lastIndex)));
            }
        }
    }
}

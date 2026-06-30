using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nelir.Models;

namespace Nelir.Services
{
    public class RpgmParser
    {
        private static readonly Regex SpeakerRegex = new(@"\\nc<([^>]*)>(.*)", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex BustLoadRegex = new(@"\$bust\((\d+)\)\.loadBitmap\([^,]+,\s*'([^'\[]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly CharacterRegistryService? _characterRegistry;

        public RpgmParser(CharacterRegistryService? characterRegistry = null)
        {
            _characterRegistry = characterRegistry;
        }

        public List<TranslationRow> ParseFile(string filePath)
        {
            var rows = new List<TranslationRow>();
            var fileName = Path.GetFileName(filePath);

            try
            {
                if (!File.Exists(filePath))
                {
                    return rows;
                }

                string fileContent = File.ReadAllText(filePath);
                using var json = JsonDocument.Parse(fileContent);

                bool isCommonEvents = fileName.Equals("CommonEvents.json", StringComparison.OrdinalIgnoreCase);
                JsonElement eventsArray;

                if (isCommonEvents)
                {
                    // CommonEvents.json is a root-level array of events
                    if (json.RootElement.ValueKind != JsonValueKind.Array)
                    {
                        return rows;
                    }
                    eventsArray = json.RootElement;
                }
                else
                {
                    // MapXXX.json files have a root object containing an "events" array
                    if (json.RootElement.ValueKind != JsonValueKind.Object || 
                        !json.RootElement.TryGetProperty("events", out var evProp) || 
                        evProp.ValueKind != JsonValueKind.Array)
                    {
                        return rows;
                    }
                    eventsArray = evProp;
                }

                int globalIndex = 1;

                foreach (JsonElement element in eventsArray.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }

                    if (!element.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.Number)
                    {
                        continue;
                    }
                    int eventId = idProp.GetInt32();

                    string eventName = string.Empty;
                    if (element.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                    {
                        eventName = nameProp.GetString() ?? string.Empty;
                    }

                    // SectionHeader for event
                    rows.Add(new TranslationRow
                    {
                        RowIndex = globalIndex++,
                        RowType = RowType.SectionHeader,
                        SourceFile = fileName,
                        EventId = eventId,
                        EventName = eventName,
                        RawText = isCommonEvents ? $"── Common Event [{eventId}] {eventName} ──" : $"── Event [{eventId}] {eventName} ──",
                        PageIndex = -1,
                        CommandIndex = -1,
                        SubIndex = -1
                    });

                    if (isCommonEvents)
                    {
                        // Common event list is directly inside the event object
                        if (element.TryGetProperty("list", out var listProp) && listProp.ValueKind == JsonValueKind.Array)
                        {
                            ParseCommandList(listProp, rows, fileName, eventId, eventName, 0, ref globalIndex);
                        }
                    }
                    else
                    {
                        // Map event has "pages"
                        if (element.TryGetProperty("pages", out var pagesProp) && pagesProp.ValueKind == JsonValueKind.Array)
                        {
                            int pageCount = pagesProp.GetArrayLength();
                            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                            {
                                var page = pagesProp[pageIndex];
                                
                                if (pageCount > 1)
                                {
                                    // SectionHeader for pages if multiple pages exist
                                    rows.Add(new TranslationRow
                                    {
                                        RowIndex = globalIndex++,
                                        RowType = RowType.SectionHeader,
                                        SourceFile = fileName,
                                        EventId = eventId,
                                        EventName = eventName,
                                        RawText = $"   Page {pageIndex + 1}",
                                        PageIndex = pageIndex,
                                        CommandIndex = -1,
                                        SubIndex = -1
                                    });
                                }

                                if (page.TryGetProperty("list", out var listProp) && listProp.ValueKind == JsonValueKind.Array)
                                {
                                    ParseCommandList(listProp, rows, fileName, eventId, eventName, pageIndex, ref globalIndex);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback / Log
                Console.WriteLine($"Error parsing RPGMaker file {fileName}: {ex.Message}");
                throw;
            }

            return rows;
        }

        public RpgmParseResult ParseFileResult(string filePath)
        {
            var result = new RpgmParseResult
            {
                FileName = Path.GetFileName(filePath),
                FilePath = filePath
            };

            try
            {
                result.Rows = ParseFile(filePath);
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private void ParseCommandList(JsonElement list, List<TranslationRow> rows, string fileName, int eventId, string eventName, int pageIndex, ref int globalIndex)
        {
            int listLength = list.GetArrayLength();
            int i = 0;
            var bustSlots = new Dictionary<int, string>();
            string currentSpeaker = string.Empty;

            while (i < listLength)
            {
                var command = list[i];
                if (command.ValueKind != JsonValueKind.Object || !command.TryGetProperty("code", out var codeProp))
                {
                    i++;
                    continue;
                }

                int code = codeProp.GetInt32();

                if (code == 355 || code == 655)
                {
                    // Script call — parse bust loadBitmap
                    if (command.TryGetProperty("parameters", out var scriptParams) &&
                        scriptParams.ValueKind == JsonValueKind.Array &&
                        scriptParams.GetArrayLength() > 0)
                    {
                        var scriptText = scriptParams[0].GetString() ?? string.Empty;
                        var bustMatch = BustLoadRegex.Match(scriptText);
                        if (bustMatch.Success &&
                            int.TryParse(bustMatch.Groups[1].Value, out int slot))
                        {
                            var bustId = bustMatch.Groups[2].Value.Trim();
                            bustSlots[slot] = bustId;

                            // Nếu registry có entry cho bustId này → update current speaker
                            if (_characterRegistry != null)
                            {
                                var resolved = _characterRegistry.Resolve(bustId);
                                // Chỉ update currentSpeaker nếu resolve ra được tên thật
                                // (tức là registry có entry, không phải fallback về bustId)
                                if (_characterRegistry.Registry.ContainsKey(bustId))
                                    currentSpeaker = resolved;
                            }
                        }
                    }
                    i++;
                    continue;
                }

                if (code == 101)
                {
                    // Dialog header (Show Message)
                    var dialogLines = new List<string>();
                    string speaker = string.Empty;
                    bool hasSpeakerTag = false;
                    int commandIndex = i;
                    i++;

                    // Collect consecutive 401 nodes
                    while (i < listLength)
                    {
                        var nextCommand = list[i];
                        if (nextCommand.ValueKind == JsonValueKind.Object && 
                            nextCommand.TryGetProperty("code", out var nextCodeProp) && 
                            nextCodeProp.GetInt32() == 401)
                        {
                            if (nextCommand.TryGetProperty("parameters", out var nextParams) && 
                                nextParams.ValueKind == JsonValueKind.Array && 
                                nextParams.GetArrayLength() > 0)
                            {
                                string line = nextParams[0].GetString() ?? string.Empty;
                                var match = SpeakerRegex.Match(line);
                                if (match.Success)
                                {
                                    hasSpeakerTag = true;
                                    speaker = match.Groups[1].Value;
                                    line = match.Groups[2].Value.Trim();
                                    if (!string.IsNullOrEmpty(line))
                                    {
                                        dialogLines.Add(line);
                                    }
                                }
                                else
                                {
                                    dialogLines.Add(line);
                                }
                            }
                            i++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (dialogLines.Count > 0 || hasSpeakerTag)
                    {
                        // Fallback: dùng bust-based speaker nếu \nc<> không có gì
                        if (string.IsNullOrEmpty(speaker) && !string.IsNullOrEmpty(currentSpeaker))
                        {
                            speaker = currentSpeaker;
                            hasSpeakerTag = true;
                        }

                        rows.Add(new TranslationRow
                        {
                            RowIndex = globalIndex++,
                            RowType = RowType.Dialog,
                            SourceFile = fileName,
                            EventId = eventId,
                            EventName = eventName,
                            PageIndex = pageIndex,
                            CommandIndex = commandIndex,
                            SubIndex = 0,
                            Speaker = speaker,
                            HasSpeakerTag = hasSpeakerTag,
                            RawText = string.Join("\n", dialogLines)
                        });
                    }
                }
                else if (code == 401)
                {
                    // Orphan 401 dialog (no preceding 101)
                    if (command.TryGetProperty("parameters", out var commandParams) && 
                        commandParams.ValueKind == JsonValueKind.Array && 
                        commandParams.GetArrayLength() > 0)
                    {
                        string line = commandParams[0].GetString() ?? string.Empty;
                        string speaker = string.Empty;
                        bool hasSpeakerTag = false;
                        var match = SpeakerRegex.Match(line);
                        if (match.Success)
                        {
                            hasSpeakerTag = true;
                            speaker = match.Groups[1].Value;
                            line = match.Groups[2].Value.Trim();
                        }

                        // Fallback: dùng bust-based speaker nếu \nc<> không có gì
                        if (string.IsNullOrEmpty(speaker) && !string.IsNullOrEmpty(currentSpeaker))
                        {
                            speaker = currentSpeaker;
                            hasSpeakerTag = true;
                        }

                        rows.Add(new TranslationRow
                        {
                            RowIndex = globalIndex++,
                            RowType = RowType.Dialog,
                            SourceFile = fileName,
                            EventId = eventId,
                            EventName = eventName,
                            PageIndex = pageIndex,
                            CommandIndex = i,
                            SubIndex = 0,
                            Speaker = speaker,
                            HasSpeakerTag = hasSpeakerTag,
                            RawText = line
                        });
                    }
                    i++;
                }
                else if (code == 102)
                {
                    // Choices (Show Choices)
                    if (command.TryGetProperty("parameters", out var commandParams) && 
                        commandParams.ValueKind == JsonValueKind.Array && 
                        commandParams.GetArrayLength() > 0)
                    {
                        var choicesArray = commandParams[0];
                        if (choicesArray.ValueKind == JsonValueKind.Array)
                        {
                            int choiceCount = choicesArray.GetArrayLength();
                            for (int choiceIdx = 0; choiceIdx < choiceCount; choiceIdx++)
                            {
                                string choiceText = choicesArray[choiceIdx].GetString() ?? string.Empty;
                                rows.Add(new TranslationRow
                                {
                                    RowIndex = globalIndex++,
                                    RowType = RowType.Choice,
                                    SourceFile = fileName,
                                    EventId = eventId,
                                    EventName = eventName,
                                    PageIndex = pageIndex,
                                    CommandIndex = i,
                                    SubIndex = choiceIdx,
                                    RawText = $"[CHOICE {choiceIdx + 1}] {choiceText}"
                                });
                            }
                        }
                    }
                    i++;
                }
                else if (code == 108)
                {
                    // Comment block
                    if (command.TryGetProperty("parameters", out var commandParams) && 
                        commandParams.ValueKind == JsonValueKind.Array && 
                        commandParams.GetArrayLength() > 0)
                    {
                        string commentText = commandParams[0].GetString() ?? string.Empty;
                        int commentIndex = i;
                        i++;

                        // Collect consecutive 408 nodes
                        while (i < listLength)
                        {
                            var nextCommand = list[i];
                            if (nextCommand.ValueKind == JsonValueKind.Object && 
                                nextCommand.TryGetProperty("code", out var nextCodeProp) && 
                                nextCodeProp.GetInt32() == 408)
                            {
                                if (nextCommand.TryGetProperty("parameters", out var nextParams) && 
                                    nextParams.ValueKind == JsonValueKind.Array && 
                                    nextParams.GetArrayLength() > 0)
                                {
                                    commentText += "\n" + (nextParams[0].GetString() ?? string.Empty);
                                }
                                i++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        rows.Add(new TranslationRow
                        {
                            RowIndex = globalIndex++,
                            RowType = RowType.Comment,
                            SourceFile = fileName,
                            EventId = eventId,
                            EventName = eventName,
                            PageIndex = pageIndex,
                            CommandIndex = commentIndex,
                            SubIndex = 0,
                            RawText = commentText
                        });
                    }
                    else
                    {
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }
        }
    }

    public class RpgmParseResult
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public List<TranslationRow> Rows { get; set; } = new();
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using NolirRpgmTranslator.Models;

namespace NolirRpgmTranslator.Services
{
    public class RpgmParser
    {
        private static readonly Regex SpeakerRegex = new(@"\\nc<([^>]*)>(.*)", RegexOptions.Compiled | RegexOptions.Singleline);

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
            }

            return rows;
        }

        private void ParseCommandList(JsonElement list, List<TranslationRow> rows, string fileName, int eventId, string eventName, int pageIndex, ref int globalIndex)
        {
            int listLength = list.GetArrayLength();
            int i = 0;

            while (i < listLength)
            {
                var command = list[i];
                if (command.ValueKind != JsonValueKind.Object || !command.TryGetProperty("code", out var codeProp))
                {
                    i++;
                    continue;
                }

                int code = codeProp.GetInt32();

                if (code == 101)
                {
                    // Dialog header (Show Message)
                    var dialogLines = new List<string>();
                    string speaker = string.Empty;
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
                                    speaker = match.Groups[1].Value;
                                    line = match.Groups[2].Value.Trim();
                                }
                                dialogLines.Add(line);
                            }
                            i++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (dialogLines.Count > 0)
                    {
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
                        var match = SpeakerRegex.Match(line);
                        if (match.Success)
                        {
                            speaker = match.Groups[1].Value;
                            line = match.Groups[2].Value.Trim();
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
}

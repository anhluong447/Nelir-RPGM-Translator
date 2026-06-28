using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nelir.Models;

namespace Nelir.Services
{
    public class ExportService
    {
        // 1. Export flat translation dictionary: Key -> TranslationText
        public void ExportFlatJson(ProjectState projectState, string outputPath)
        {
            var dict = new Dictionary<string, string>();
            foreach (var row in projectState.AllRows)
            {
                if (row.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(row.TranslationText))
                {
                    dict[row.UniqueKey] = row.TranslationText;
                }
            }

            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string content = JsonSerializer.Serialize(dict, options);
            
            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(outputPath, content);
        }

        // 1.1 Export flat translation dictionary for a single file: Key -> TranslationText
        public void ExportFileFlatJson(ProjectState projectState, string fileName, string outputPath)
        {
            var dict = new Dictionary<string, string>();
            foreach (var row in projectState.AllRows)
            {
                if (row.SourceFile == fileName && row.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(row.TranslationText))
                {
                    dict[row.UniqueKey] = row.TranslationText;
                }
            }

            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string content = JsonSerializer.Serialize(dict, options);
            
            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(outputPath, content);
        }

        // 2. Export write-back to original RPGMaker JSON files
        public int ExportToGameFiles(ProjectState projectState, string outputFolderPath)
        {
            int filesWritten = 0;
            Directory.CreateDirectory(outputFolderPath);

            foreach (string file in projectState.LoadedFiles)
            {
                // Only consider rows for this file that are editable and have a translation
                var fileRows = projectState.AllRows
                    .Where(r => r.SourceFile == file && r.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(r.TranslationText))
                    .OrderBy(r => r.EventId)
                    .ThenBy(r => r.PageIndex)
                    .ThenByDescending(r => r.CommandIndex)
                    .ThenByDescending(r => r.SubIndex)
                    .ToList();

                if (fileRows.Count == 0)
                {
                    // If no translations for this file, we don't need to write a copy
                    continue;
                }

                try
                {
                    string originalFilePath = Path.Combine(projectState.DataFolderPath, file);
                    if (!File.Exists(originalFilePath))
                    {
                        continue;
                    }

                    string jsonContent = File.ReadAllText(originalFilePath);
                    var doc = JsonNode.Parse(jsonContent);
                    if (doc == null)
                    {
                        continue;
                    }

                    bool isCommonEvents = file.Equals("CommonEvents.json", StringComparison.OrdinalIgnoreCase);

                    foreach (var row in fileRows)
                    {
                        JsonNode? eventNode = null;

                        // Find the event node in the JSON by matching ID
                        if (isCommonEvents)
                        {
                            var commonEventsArray = doc.AsArray();
                            foreach (var ev in commonEventsArray)
                            {
                                if (ev != null && ev["id"]?.GetValue<int>() == row.EventId)
                                {
                                    eventNode = ev;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            var eventsArray = doc["events"]?.AsArray();
                            if (eventsArray != null)
                            {
                                foreach (var ev in eventsArray)
                                {
                                    if (ev != null && ev["id"]?.GetValue<int>() == row.EventId)
                                    {
                                        eventNode = ev;
                                        break;
                                    }
                                }
                            }
                        }

                        if (eventNode == null)
                        {
                            continue;
                        }

                        // Retrieve the commands list array
                        JsonArray? listArray = null;
                        if (isCommonEvents)
                        {
                            listArray = eventNode["list"]?.AsArray();
                        }
                        else
                        {
                            var pagesArray = eventNode["pages"]?.AsArray();
                            var pageNode = pagesArray?[row.PageIndex];
                            listArray = pageNode?["list"]?.AsArray();
                        }

                        if (listArray == null || row.CommandIndex >= listArray.Count)
                        {
                            continue;
                        }

                        if (row.RowType == RowType.Dialog)
                        {
                            var lines = row.TranslationText.Replace("\r\n", "\n").Split('\n');
                            int indent = listArray[row.CommandIndex]?["indent"]?.GetValue<int>() ?? 0;

                            // Find and collect all consecutive 401 nodes after CommandIndex
                            int start401 = row.CommandIndex + 1;
                            int count401 = 0;
                            while (start401 + count401 < listArray.Count && 
                                   listArray[start401 + count401]?["code"]?.GetValue<int>() == 401)
                            {
                                count401++;
                            }

                            // Remove existing 401 nodes
                            for (int k = 0; k < count401; k++)
                            {
                                listArray.RemoveAt(start401);
                            }

                            // Insert new 401 nodes with translation lines
                            for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
                            {
                                string lineText = lines[lineIdx];
                                if (lineIdx == 0 && row.HasSpeakerTag)
                                {
                                    lineText = $"\\nc<{row.Speaker}>{lineText}";
                                }

                                var new401 = new JsonObject
                                {
                                    ["code"] = 401,
                                    ["indent"] = indent,
                                    ["parameters"] = new JsonArray { lineText }
                                };

                                listArray.Insert(start401 + lineIdx, new401);
                            }
                        }
                        else if (row.RowType == RowType.Choice)
                        {
                            var commandNode = listArray[row.CommandIndex];
                            var parametersArray = commandNode?["parameters"]?.AsArray();
                            if (parametersArray != null && parametersArray.Count > 0)
                            {
                                var choicesArray = parametersArray[0]?.AsArray();
                                if (choicesArray != null && row.SubIndex < choicesArray.Count)
                                {
                                    choicesArray[row.SubIndex] = row.TranslationText;
                                }
                            }
                        }
                        else if (row.RowType == RowType.Comment)
                        {
                            var lines = row.TranslationText.Replace("\r\n", "\n").Split('\n');
                            int indent = listArray[row.CommandIndex]?["indent"]?.GetValue<int>() ?? 0;

                            // Overwrite the first comment line (108)
                            var commandNode = listArray[row.CommandIndex];
                            var parametersArray = commandNode?["parameters"]?.AsArray();
                            if (parametersArray != null && parametersArray.Count > 0 && lines.Length > 0)
                            {
                                parametersArray[0] = lines[0];
                            }

                            // Find and collect all consecutive 408 nodes after CommandIndex
                            int start408 = row.CommandIndex + 1;
                            int count408 = 0;
                            while (start408 + count408 < listArray.Count && 
                                   listArray[start408 + count408]?["code"]?.GetValue<int>() == 408)
                            {
                                count408++;
                            }

                            // Remove existing 408 nodes
                            for (int k = 0; k < count408; k++)
                            {
                                listArray.RemoveAt(start408);
                            }

                            // Insert new 408 nodes for subsequent lines of the comment
                            for (int lineIdx = 1; lineIdx < lines.Length; lineIdx++)
                            {
                                var new408 = new JsonObject
                                {
                                    ["code"] = 408,
                                    ["indent"] = indent,
                                    ["parameters"] = new JsonArray { lines[lineIdx] }
                                };
                                listArray.Insert(start408 + lineIdx - 1, new408);
                            }
                        }
                    }

                    // Save the modified document to the output folder path
                    string outputPath = Path.Combine(outputFolderPath, file);
                    var serializeOptions = new JsonSerializerOptions
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    File.WriteAllText(outputPath, doc.ToJsonString(serializeOptions));
                    filesWritten++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting write-back for file {file}: {ex.Message}");
                }
            }

            return filesWritten;
        }

        // 2.1 Export write-back to original RPGMaker JSON for a single file
        public void ExportFileStructured(ProjectState projectState, string fileName, string outputPath)
        {
            string originalFilePath = Path.Combine(projectState.DataFolderPath, fileName);
            if (!File.Exists(originalFilePath))
            {
                throw new FileNotFoundException($"Không tìm thấy tệp gốc {fileName} tại {projectState.DataFolderPath}");
            }

            var fileRows = projectState.AllRows
                .Where(r => r.SourceFile == fileName && r.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(r.TranslationText))
                .OrderBy(r => r.EventId)
                .ThenBy(r => r.PageIndex)
                .ThenByDescending(r => r.CommandIndex)
                .ThenByDescending(r => r.SubIndex)
                .ToList();

            string jsonContent = File.ReadAllText(originalFilePath);
            var doc = JsonNode.Parse(jsonContent);
            if (doc == null)
            {
                throw new InvalidOperationException($"Lỗi phân tích cú pháp JSON của tệp {fileName}");
            }

            bool isCommonEvents = fileName.Equals("CommonEvents.json", StringComparison.OrdinalIgnoreCase);

            foreach (var row in fileRows)
            {
                JsonNode? eventNode = null;

                // Find the event node in the JSON by matching ID
                if (isCommonEvents)
                {
                    var commonEventsArray = doc.AsArray();
                    foreach (var ev in commonEventsArray)
                    {
                        if (ev != null && ev["id"]?.GetValue<int>() == row.EventId)
                        {
                            eventNode = ev;
                            break;
                        }
                    }
                }
                else
                {
                    var eventsArray = doc["events"]?.AsArray();
                    if (eventsArray != null)
                    {
                        foreach (var ev in eventsArray)
                        {
                            if (ev != null && ev["id"]?.GetValue<int>() == row.EventId)
                            {
                                eventNode = ev;
                                break;
                            }
                        }
                    }
                }

                if (eventNode == null)
                {
                    continue;
                }

                // Retrieve the commands list array
                JsonArray? listArray = null;
                if (isCommonEvents)
                {
                    listArray = eventNode["list"]?.AsArray();
                }
                else
                {
                    var pagesArray = eventNode["pages"]?.AsArray();
                    var pageNode = pagesArray?[row.PageIndex];
                    listArray = pageNode?["list"]?.AsArray();
                }

                if (listArray == null || row.CommandIndex >= listArray.Count)
                {
                    continue;
                }

                if (row.RowType == RowType.Dialog)
                {
                    var lines = row.TranslationText.Replace("\r\n", "\n").Split('\n');
                    int indent = listArray[row.CommandIndex]?["indent"]?.GetValue<int>() ?? 0;

                    // Find and collect all consecutive 401 nodes after CommandIndex
                    int start401 = row.CommandIndex + 1;
                    int count401 = 0;
                    while (start401 + count401 < listArray.Count && 
                           listArray[start401 + count401]?["code"]?.GetValue<int>() == 401)
                    {
                        count401++;
                    }

                    // Remove existing 401 nodes
                    for (int k = 0; k < count401; k++)
                    {
                        listArray.RemoveAt(start401);
                    }

                    // Insert new 401 nodes with translation lines
                    for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
                    {
                        string lineText = lines[lineIdx];
                        if (lineIdx == 0 && row.HasSpeakerTag)
                        {
                            lineText = $"\\nc<{row.Speaker}>{lineText}";
                        }

                        var new401 = new JsonObject
                        {
                            ["code"] = 401,
                            ["indent"] = indent,
                            ["parameters"] = new JsonArray { lineText }
                        };

                        listArray.Insert(start401 + lineIdx, new401);
                    }
                }
                else if (row.RowType == RowType.Choice)
                {
                    var commandNode = listArray[row.CommandIndex];
                    var parametersArray = commandNode?["parameters"]?.AsArray();
                    if (parametersArray != null && parametersArray.Count > 0)
                    {
                        var choicesArray = parametersArray[0]?.AsArray();
                        if (choicesArray != null && row.SubIndex < choicesArray.Count)
                        {
                            choicesArray[row.SubIndex] = row.TranslationText;
                        }
                    }
                }
                else if (row.RowType == RowType.Comment)
                {
                    var lines = row.TranslationText.Replace("\r\n", "\n").Split('\n');
                    int indent = listArray[row.CommandIndex]?["indent"]?.GetValue<int>() ?? 0;

                    // Overwrite the first comment line (108)
                    var commandNode = listArray[row.CommandIndex];
                    var parametersArray = commandNode?["parameters"]?.AsArray();
                    if (parametersArray != null && parametersArray.Count > 0 && lines.Length > 0)
                    {
                        parametersArray[0] = lines[0];
                    }

                    // Find and collect all consecutive 408 nodes after CommandIndex
                    int start408 = row.CommandIndex + 1;
                    int count408 = 0;
                    while (start408 + count408 < listArray.Count && 
                           listArray[start408 + count408]?["code"]?.GetValue<int>() == 408)
                    {
                        count408++;
                    }

                    // Remove existing 408 nodes
                    for (int k = 0; k < count408; k++)
                    {
                        listArray.RemoveAt(start408);
                    }

                    // Insert new 408 nodes for subsequent lines of the comment
                    for (int lineIdx = 1; lineIdx < lines.Length; lineIdx++)
                    {
                        var new408 = new JsonObject
                        {
                            ["code"] = 408,
                            ["indent"] = indent,
                            ["parameters"] = new JsonArray { lines[lineIdx] }
                        };
                        listArray.Insert(start408 + lineIdx - 1, new408);
                    }
                }
            }

            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var serializeOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(outputPath, doc.ToJsonString(serializeOptions));
        }
    }
}



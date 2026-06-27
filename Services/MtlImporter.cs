using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nelir.Models;

namespace Nelir.Services
{
    public class MtlImporter
    {
        public int ImportMtl(string mtlFilePath, ProjectState projectState)
        {
            int mergeCount = 0;
            try
            {
                if (!File.Exists(mtlFilePath))
                {
                    return 0;
                }

                string content = File.ReadAllText(mtlFilePath);
                var mtlData = JsonSerializer.Deserialize<Dictionary<string, string>>(content);
                if (mtlData == null)
                {
                    return 0;
                }

                foreach (var kvp in mtlData)
                {
                    string uniqueKey = kvp.Key;
                    string translatedText = kvp.Value;

                    if (projectState.RowIndex.TryGetValue(uniqueKey, out var row))
                    {
                        row.MtlText = translatedText;
                        mergeCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing MTL file {mtlFilePath}: {ex.Message}");
            }

            return mergeCount;
        }

        public int ImportMtlFolder(string mtlFolderPath, ProjectState projectState)
        {
            int mergeCount = 0;
            try
            {
                if (!Directory.Exists(mtlFolderPath))
                {
                    return 0;
                }

                var files = Directory.GetFiles(mtlFolderPath, "*.json");
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string content = File.ReadAllText(file);

                    // 1. Try parsing as flat translation dictionary first
                    bool isFlatDict = false;
                    try
                    {
                        var mtlData = JsonSerializer.Deserialize<Dictionary<string, string>>(content);
                        if (mtlData != null && mtlData.Keys.Any(k => k.Contains("::")))
                        {
                            isFlatDict = true;
                            foreach (var kvp in mtlData)
                            {
                                if (projectState.RowIndex.TryGetValue(kvp.Key, out var row))
                                {
                                    row.MtlText = kvp.Value;
                                    mergeCount++;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Not a flat dict, proceed to parse as standard game json
                    }

                    if (isFlatDict)
                    {
                        continue;
                    }

                    // 2. Fallback: Parse as a translated RPGMaker game file
                    if (projectState.LoadedFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                    {
                        var parser = new RpgmParser();
                        var mtlRows = parser.ParseFile(file);
                        foreach (var mtlRow in mtlRows)
                        {
                            if (mtlRow.RowType != RowType.SectionHeader)
                            {
                                if (projectState.RowIndex.TryGetValue(mtlRow.UniqueKey, out var row))
                                {
                                    row.MtlText = mtlRow.RawText;
                                    mergeCount++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing MTL folder {mtlFolderPath}: {ex.Message}");
            }

            return mergeCount;
        }
    }
}

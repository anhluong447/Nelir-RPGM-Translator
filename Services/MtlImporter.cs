using System;
using System.Collections.Generic;
using System.IO;
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
    }
}


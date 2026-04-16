using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniClaude.Editor
{
    /// <summary>
    /// Interface for asset scanners that extract structured data from project files.
    /// Each implementation handles a specific asset type.
    /// </summary>
    public interface IAssetScanner
    {
        /// <summary>The kind of asset this scanner produces entries for.</summary>
        AssetKind Kind { get; }

        /// <summary>
        /// Returns true if this scanner can process the given asset path.
        /// </summary>
        /// <param name="assetPath">Relative asset path from project root.</param>
        /// <returns>True if this scanner should handle the file.</returns>
        bool CanScan(string assetPath);

        /// <summary>
        /// Scans a single asset file and returns a structured index entry.
        /// </summary>
        /// <param name="assetPath">Relative asset path from project root.</param>
        /// <returns>An IndexEntry with extracted data, or null if the file cannot be parsed.</returns>
        IndexEntry Scan(string assetPath);
    }

    /// <summary>
    /// Holds all registered asset scanners and dispatches scan requests
    /// to the appropriate scanner based on file path.
    /// </summary>
    public class ScannerRegistry
    {
        readonly List<IAssetScanner> _scanners = new();

        /// <summary>All registered scanners.</summary>
        public IReadOnlyList<IAssetScanner> Scanners => _scanners;

        /// <summary>
        /// Registers a scanner. Later registrations take priority for overlapping CanScan matches.
        /// </summary>
        /// <param name="scanner">The scanner to register.</param>
        public void Register(IAssetScanner scanner)
        {
            _scanners.Add(scanner);
        }

        /// <summary>
        /// Finds the first matching scanner and scans the asset.
        /// If the scanner throws, logs a warning and returns null.
        /// </summary>
        /// <param name="assetPath">Relative asset path from project root.</param>
        /// <returns>An IndexEntry, or null if no scanner matches or the scan fails.</returns>
        public IndexEntry Scan(string assetPath)
        {
            for (int i = _scanners.Count - 1; i >= 0; i--)
            {
                if (!_scanners[i].CanScan(assetPath))
                    continue;

                try
                {
                    return _scanners[i].Scan(assetPath);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UniClaude] Scanner failed for {assetPath}: {ex.Message}");
                    return null;
                }
            }

            return null;
        }
    }
}

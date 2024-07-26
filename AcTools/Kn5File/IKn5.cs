using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Kn5File {
    public interface IKn5 {
        [CanBeNull]
        string OriginalFilename { get; }

        IEnumerable<Kn5Node> Nodes { get; }

        Dictionary<string, Kn5Texture> Textures { get; set; }

        Dictionary<string, byte[]> TexturesData { get; set; }

        Dictionary<string, Kn5Material> Materials { get; set; }

        Kn5Node RootNode { get; set; }

        int NodesCount { get; }

        bool IsEditable { get; }

        [CanBeNull]
        Kn5Material GetMaterial(uint id);

        [CanBeNull]
        Kn5Node GetNode([NotNull] string path);

        [CanBeNull]
        Kn5Node FirstByName([CanBeNull] string name);

        int RemoveAllByName(Kn5Node node, string name);

        int RemoveAllByName(string name);

        [CanBeNull]
        Kn5Node FirstFiltered(Func<Kn5Node, bool> filter);

        void Save(Stream stream);

        void Save(string filename);

        void SaveRecyclingOriginal(string filename);

        void ExportCollada(string filename);

        void ExportFbx(string filename);

        void ConvertColladaToFbx(string colladaFilename, string fbxFilename);

        void ExportFbxWithIni(string fbxFilename);

        Task ExportFbxWithIniAsync(string fbxFilename, IProgress<string> progress = null, CancellationToken cancellation = default);

        bool IsWithoutTextures();

        void ExportTextures(string textureDir);

        Task ExportTexturesAsync(string textureDir, IProgress<string> progress = null, CancellationToken cancellation = default);

        void SetTexture([Localizable(false)] string textureName, string filename);

        [CanBeNull]
        string GetObjectPath([NotNull] Kn5Node node);

        [CanBeNull]
        string GetParentPath([NotNull] Kn5Node node);

        void Refresh();
    }
}
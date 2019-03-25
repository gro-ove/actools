using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Kn5File {
    public interface IKn5 {
        string OriginalFilename { get; }

        IEnumerable<Kn5Node> Nodes { get; }

        Dictionary<string, Kn5Texture> Textures { get; }

        Dictionary<string, byte[]> TexturesData { get; }

        Dictionary<string, Kn5Material> Materials { get; }

        Kn5Node RootNode { get; }

        int NodesCount { get; }

        bool IsEditable { get; }

        Kn5Material GetMaterial(uint id);

        Kn5Node GetNode([NotNull] string path);

        Kn5Node FirstByName(string name);

        int RemoveAllByName(Kn5Node node, string name);

        int RemoveAllByName(string name);

        Kn5Node FirstFiltered(Func<Kn5Node, bool> filter);

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
        string GetObjectPath(Kn5Node node);
    }
}